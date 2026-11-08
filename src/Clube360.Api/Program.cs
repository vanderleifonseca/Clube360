using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.RateLimiting;
using Clube360.Application;
using Clube360.Infrastructure;
using Clube360.Contracts;
using Clube360.Domain;
using Clube360.Api.Seguranca;
using Npgsql;
var builder=WebApplication.CreateBuilder(args);
if (builder.Configuration["Dados:Arquivo"] is null || builder.Configuration["Dados:ContasArquivo"] is null)
    CaminhosDadosLocais.Preparar(builder.Environment.ContentRootPath);
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=65536);
builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
var arquivoClubes = builder.Configuration["Dados:Arquivo"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "clubes.json");
var arquivoContas = builder.Configuration["Dados:ContasArquivo"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "contas.json");
var provedor = builder.Configuration["Dados:Provedor"] ?? "Json";
var usarPostgres = provedor.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase);
if (usarPostgres)
{
    var conexao = builder.Configuration.GetConnectionString("Clube360");
    if (string.IsNullOrWhiteSpace(conexao))
        throw new InvalidOperationException("Configure ConnectionStrings:Clube360 para usar o PostgreSQL.");

    builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(conexao));
    builder.Services.AddSingleton<ArmazenamentoPostgres>();
    builder.Services.AddSingleton<RepositorioClubesPostgres>();
    builder.Services.AddSingleton<IRepositorioClubes>(sp => sp.GetRequiredService<RepositorioClubesPostgres>());
    builder.Services.AddSingleton<IDadosDemonstracao>(sp => sp.GetRequiredService<RepositorioClubesPostgres>());
}
else if (provedor.Equals("Json", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton(_ => new RepositorioClubes(arquivoClubes));
    builder.Services.AddSingleton<IRepositorioClubes>(sp => sp.GetRequiredService<RepositorioClubes>());
    builder.Services.AddSingleton<IDadosDemonstracao>(sp => sp.GetRequiredService<RepositorioClubes>());
}
else
{
    throw new InvalidOperationException("Dados:Provedor deve ser Json ou PostgreSQL.");
}
builder.Services.AddSingleton<ConsultaDemonstracao>();
builder.Services.AddSingleton<GestaoClubes>();
builder.Services.AddSingleton<PortariaClube>();
builder.Services.AddSingleton<ContasLocais>();
builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken(o=>o.BearerTokenExpiration=TimeSpan.FromMinutes(20));
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o=>{
    o.RejectionStatusCode=429;
    o.AddPolicy("publico",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"local",
        _=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
    o.AddPolicy("login",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"local",
        _=>new FixedWindowRateLimiterOptions{PermitLimit=5,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
});
var app=builder.Build();
if (usarPostgres)
{
    app.Services.GetRequiredService<ArmazenamentoPostgres>().Inicializar(
        () => RepositorioClubes.CarregarEstado(arquivoClubes),
        () => ContasLocais.CarregarArquivo(arquivoContas));
    app.Logger.LogInformation("Persistência do Clube360 conectada ao PostgreSQL.");
}
app.Use(async(ctx,next)=>{
    try{await next();}
    catch(UnauthorizedAccessException){if(!ctx.Response.HasStarted){ctx.Response.StatusCode=403;await ctx.Response.WriteAsJsonAsync(new{mensagem="Acesso não permitido."});}}
    catch(ArgumentException e){if(!ctx.Response.HasStarted){ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new{mensagem=e.Message});}}
});
app.UseAuthentication();app.UseAuthorization();app.UseRateLimiter();
app.MapGet("/api/v1/status",()=>new{sistema="Clube360",versao="0.3.1",mensagem="API em funcionamento",demonstracao=app.Environment.IsDevelopment(),persistencia=usarPostgres?"PostgreSQL":"JSON",dataUtc=DateTimeOffset.UtcNow});
if(app.Environment.IsDevelopment()) {
    app.MapPost("/api/v1/login",async(LoginRequest r,ContasLocais contas,HttpContext ctx)=>{
        var conta=contas.Validar(r.Email,r.Senha);
        if(conta is null){ctx.Response.StatusCode=401;return;}
        var principal=new ClaimsPrincipal(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier,conta.Id),new(ClaimTypes.Name,conta.Nome),new(ClaimTypes.Role,conta.Perfil)
        ],BearerTokenDefaults.AuthenticationScheme));
        await ctx.SignInAsync(BearerTokenDefaults.AuthenticationScheme,principal);
    }).RequireRateLimiting("login");
    app.MapPost("/api/v1/eventos-publicos/{id:guid}/inscricoes",(Guid id,VisitanteRequest r,GestaoClubes g)=>{var v=g.InscreverVisitante(id,r);return new{v.Id,mensagem="Inscrição de demonstração registrada. A integração bancária ainda não está configurada."};}).RequireRateLimiting("publico");
    app.MapGet("/api/v1/eventos-publicos",(GestaoClubes g)=>g.Ler(s=>s.Eventos.Where(e=>e.Ativo&&e.Publico==PublicoEvento.Publico&&e.Fim>g.Agora).ToArray()));
    var api=app.MapGroup("/api/v1").RequireAuthorization();
    api.MapGet("/sessao",(HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        var c=Atual(ctx,contas);
        var clubes=g.Ler(s=>s.Clubes.Where(x=>c.Perfil=="SuperAdmin"||x.Id==c.ClubeId).Select(x=>new ClubeDto(x.Id,x.Nome)).ToArray());
        return new SessaoDto(c.Nome,c.Perfil,clubes);
    });
    api.MapGet("/clubes",(HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        var c=Atual(ctx,contas);
        if (c.Perfil == "Porteiro") throw new UnauthorizedAccessException();
        return g.Ler(s=>s.Clubes.Where(x=>c.Perfil=="SuperAdmin"||x.Id==c.ClubeId).ToArray());
    });
    api.MapPost("/clubes",(ClubeRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        if(Atual(ctx,contas).Perfil!="SuperAdmin")throw new UnauthorizedAccessException();return g.SalvarClube(null,r);
    });
    api.MapPut("/clubes/{clube:guid}",(Guid clube,ClubeRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        var c=Atual(ctx,contas);if(c.Perfil!="SuperAdmin"&&(c.Perfil!="Presidente"||c.ClubeId!=clube))throw new UnauthorizedAccessException();
        return g.SalvarClube(clube,r);
    });
    api.MapGet("/clubes/{clube:guid}/associados",(Guid clube,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirGestor(Atual(ctx,contas),clube);return g.Ler(s=>s.Associados.Where(x=>x.ClubeId==clube).ToArray());
    });
    api.MapPost("/clubes/{clube:guid}/associados",(Guid clube,AssociadoRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirGestor(Atual(ctx,contas),clube);return g.CriarPessoa(clube,r);
    });
    api.MapPut("/clubes/{clube:guid}/associados/{id:guid}",(Guid clube,Guid id,AssociadoRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirGestor(Atual(ctx,contas),clube);return g.AtualizarPessoa(clube,id,r);
    });
    api.MapGet("/clubes/{clube:guid}/meu-cadastro",(Guid clube,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        var id=ExigirSocio(Atual(ctx,contas),clube);
        return g.Ler(s=>new{associado=GestaoClubes.Pessoa(s,clube,id),dependentes=s.Associados.Where(x=>x.ClubeId==clube&&x.TitularId==id).ToArray()});
    });
    api.MapGet("/clubes/{clube:guid}/meu-extrato",(Guid clube,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        var id=ExigirSocio(Atual(ctx,contas),clube);return ExtratoAtual(g,clube,id);
    });
    api.MapGet("/clubes/{clube:guid}/titulares/{titular:guid}/extrato",(Guid clube,Guid titular,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirFinanceiro(Atual(ctx,contas),clube);return ExtratoAtual(g,clube,titular);
    });
    api.MapGet("/clubes/{clube:guid}/usuarios",(Guid clube,HttpContext ctx,ContasLocais contas)=> {
        ExigirGestor(Atual(ctx,contas),clube);return contas.Listar(clube);
    });
    api.MapPost("/clubes/{clube:guid}/usuarios",(Guid clube,NovoAcessoRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        var c=Atual(ctx,contas);ExigirGestor(c,clube);
        g.Ler(s=>GestaoClubes.ExigirClube(s,clube));
        if(r.AssociadoId is { } id)g.Ler(s=>GestaoClubes.Pessoa(s,clube,id));
        return contas.Criar(clube,r,c);
    });
    var portaria = api.MapGroup("/clubes/{clube:guid}/portaria");
    portaria.MapGet("", (Guid clube, DateOnly? dia, HttpContext ctx, ContasLocais contas, PortariaClube controle) =>
    {
        ExigirPortaria(Atual(ctx, contas), clube);
        return controle.Consultar(clube, dia);
    });
    portaria.MapPut("/visitas/{visita:guid}", (Guid clube, Guid visita, EntradaPortariaRequest r,
        HttpContext ctx, ContasLocais contas, PortariaClube controle) =>
    {
        var c = Atual(ctx, contas);
        ExigirPortaria(c, clube);
        return controle.RegistrarEntrada(clube, visita, r.AssociadoId, c.Id, c.Nome);
    });
    portaria.MapPost("/visitas/{visita:guid}/saida", (Guid clube, Guid visita,
        HttpContext ctx, ContasLocais contas, PortariaClube controle) =>
    {
        var c = Atual(ctx, contas);
        ExigirPortaria(c, clube);
        return controle.RegistrarSaida(clube, visita, c.Id, c.Nome);
    });
    api.MapGet("/clubes/{clube:guid}/espacos",(Guid clube,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirClube(Atual(ctx,contas),clube);return g.Ler(s=>s.Espacos.Where(x=>x.ClubeId==clube&&x.Ativo).ToArray());
    });
    api.MapPost("/clubes/{clube:guid}/espacos",(Guid clube,EspacoRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirGestor(Atual(ctx,contas),clube);return g.CriarEspaco(clube,r);
    });
    api.MapPut("/clubes/{clube:guid}/espacos/{id:guid}",(Guid clube,Guid id,EspacoRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirGestor(Atual(ctx,contas),clube);return g.AtualizarEspaco(clube,id,r);
    });
    api.MapGet("/clubes/{clube:guid}/eventos",(Guid clube,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirClube(Atual(ctx,contas),clube);return g.Ler(s=>s.Eventos.Where(x=>x.ClubeId==clube&&x.Ativo).OrderBy(x=>x.Inicio).ToArray());
    });
    api.MapPost("/clubes/{clube:guid}/eventos",(Guid clube,EventoRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirGestor(Atual(ctx,contas),clube);return g.CriarEvento(clube,r);
    });
    api.MapPost("/clubes/{clube:guid}/eventos/{id:guid}/inscricoes",(Guid clube,Guid id,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>
        g.Inscrever(clube,ExigirSocio(Atual(ctx,contas),clube),id));
    api.MapGet("/clubes/{clube:guid}/agenda",(Guid clube,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirClube(Atual(ctx,contas),clube);return g.Agenda(clube);
    });
    api.MapGet("/clubes/{clube:guid}/reservas",(Guid clube,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        var c=Atual(ctx,contas);ExigirClube(c,clube);
        return g.Ler(s=>s.Reservas.Where(x=>x.ClubeId==clube&&(Gestor(c,clube)||x.SolicitanteId==c.AssociadoId)).OrderBy(x=>x.Inicio).ToArray());
    });
    api.MapPost("/clubes/{clube:guid}/reservas",(Guid clube,ReservaRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>
        g.Reservar(clube,ExigirSocio(Atual(ctx,contas),clube),r));
    api.MapPost("/clubes/{clube:guid}/reservas/{id:guid}/cancelar",(Guid clube,Guid id,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        var c=Atual(ctx,contas);ExigirClube(c,clube);return g.CancelarReserva(clube,id,c.AssociadoId,Gestor(c,clube));
    });
    api.MapGet("/clubes/{clube:guid}/solicitacoes",(Guid clube,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        var c=Atual(ctx,contas);ExigirClube(c,clube);
        if(!Gestor(c,clube)&&c.Perfil!="Socio")throw new UnauthorizedAccessException();
        return g.Ler(s=>s.Solicitacoes.Where(x=>x.ClubeId==clube&&(Gestor(c,clube)||x.AssociadoId==c.AssociadoId)).ToArray());
    });
    api.MapPost("/clubes/{clube:guid}/solicitacoes",(Guid clube,SolicitacaoRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>
        g.CriarSolicitacao(clube,ExigirSocio(Atual(ctx,contas),clube),r));
    api.MapPost("/clubes/{clube:guid}/solicitacoes/{id:guid}/andamentos",(Guid clube,Guid id,AndamentoRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirGestor(Atual(ctx,contas),clube);return g.AtualizarSolicitacao(clube,id,r);
    });
    api.MapGet("/clubes/{clube:guid}/convites",(Guid clube,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        var c=Atual(ctx,contas);ExigirClube(c,clube);
        if(!Gestor(c,clube)&&c.Perfil!="Socio")throw new UnauthorizedAccessException();
        return g.Ler(s=>s.Convites.Where(x=>x.ClubeId==clube&&(Gestor(c,clube)||x.SolicitanteId==c.AssociadoId)).ToArray());
    });
    api.MapPost("/clubes/{clube:guid}/convites",(Guid clube,ConviteRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>
        g.Convidar(clube,ExigirSocio(Atual(ctx,contas),clube),r));
    api.MapPost("/clubes/{clube:guid}/convites/{id:guid}/decisao",(Guid clube,Guid id,DecisaoRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirGestor(Atual(ctx,contas),clube);return g.DecidirConvite(clube,id,r);
    });
    api.MapGet("/clubes/{clube:guid}/financeiro",(Guid clube,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        var c=Atual(ctx,contas);ExigirClube(c,clube);
        Guid? socio=c.Perfil=="Socio"?ExigirSocio(c,clube):null;if(socio is null)ExigirFinanceiro(c,clube);
        return Financeiro(g,clube,socio);
    });
    api.MapPost("/clubes/{clube:guid}/financeiro/mensalidades",(Guid clube,MensalidadesRequest r,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirFinanceiro(Atual(ctx,contas),clube);return new{quantidade=g.GerarMensalidades(clube,r)};
    });
    api.MapPost("/clubes/{clube:guid}/financeiro/{id:guid}/receber",(Guid clube,Guid id,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        ExigirFinanceiro(Atual(ctx,contas),clube);return g.Receber(clube,id,Atual(ctx,contas).Id);
    });
    api.MapGet("/clubes/{clube:guid}/financeiro/{id:guid}/boleto",(Guid clube,Guid id,HttpContext ctx,ContasLocais contas,GestaoClubes g)=>{
        var c=Atual(ctx,contas);ExigirClube(c,clube);
        var f=Financeiro(g,clube,c.Perfil=="Socio"?ExigirSocio(c,clube):null);
        if(c.Perfil!="Socio")ExigirFinanceiro(c,clube);
        if(f.Cobrancas.All(x=>x.Id!=id))throw new UnauthorizedAccessException();
        return new BoletoDto(false,null,"Integração bancária ainda não configurada. A cobrança interna não é um boleto bancário.");
    });
}
app.Run();
static ContaLocal Atual(HttpContext ctx,ContasLocais contas)=>contas.Atual(ctx.User)??throw new UnauthorizedAccessException();
static bool Gestor(ContaLocal c,Guid clube)=>c.Perfil=="SuperAdmin"||(c.ClubeId==clube&&c.Perfil is "Presidente" or "Administrador");
static void ExigirGestor(ContaLocal c,Guid clube){if(!Gestor(c,clube))throw new UnauthorizedAccessException();}
static void ExigirFinanceiro(ContaLocal c,Guid clube){if(!Gestor(c,clube)&&!(c.ClubeId==clube&&c.Perfil=="Tesoureiro"))throw new UnauthorizedAccessException();}
static void ExigirPortaria(ContaLocal c, Guid clube)
{
    if (!Gestor(c, clube) && !(c.Perfil == "Porteiro" && c.ClubeId == clube))
        throw new UnauthorizedAccessException();
}
static void ExigirClube(ContaLocal c,Guid clube){if(c.Perfil=="Porteiro"||(c.Perfil!="SuperAdmin"&&c.ClubeId!=clube))throw new UnauthorizedAccessException();}
static Guid ExigirSocio(ContaLocal c,Guid clube){if(c.Perfil!="Socio"||c.ClubeId!=clube||c.AssociadoId is null)throw new UnauthorizedAccessException();return c.AssociadoId.Value;}
static FinanceiroDto Financeiro(GestaoClubes g,Guid clube,Guid? socio)=>g.Ler(s=>{
    Associado? a=socio is { } id?GestaoClubes.Pessoa(s,clube,id):null;
    var hoje=DateOnly.FromDateTime(g.Agora.UtcDateTime);
    var itens=s.Cobrancas.Where(c=>c.ClubeId==clube&&(a is null||(a.Categoria==Categoria.Dependente?c.ConsumidorId==a.Id:c.TitularId==a.Id)))
        .Select(c=>new CobrancaDto(c.Id,c.TitularId,s.Associados.FirstOrDefault(x=>x.Id==c.TitularId)?.Nome??s.Visitantes.FirstOrDefault(v=>v.Id==c.TitularId)?.Nome??"Visitante",
            s.Associados.FirstOrDefault(x=>x.Id==c.ConsumidorId)?.Nome??s.Visitantes.FirstOrDefault(v=>v.Id==c.ConsumidorId)?.Nome??"Visitante",c.Origem,c.Descricao,c.Valor,c.Vencimento,c.Situacao.ToString(),c.Vencida(hoje),c.Competencia)).OrderBy(c=>c.Vencimento).ToArray();
    return new FinanceiroDto(itens.Where(x=>x.Situacao=="Aberta").Sum(x=>x.Valor),itens.Where(x=>x.Vencida).Sum(x=>x.Valor),itens.Where(x=>x.Situacao is "Paga" or "ReembolsoPendente").Sum(x=>x.Valor),itens);
});
static ExtratoDto ExtratoAtual(GestaoClubes g,Guid clube,Guid pessoa) {
    var a=g.Ler(s=>GestaoClubes.Pessoa(s,clube,pessoa));var f=Financeiro(g,clube,pessoa);
    var mensal=f.Cobrancas.Where(c=>c.Situacao=="Aberta"&&c.Origem=="Mensalidade").Sum(c=>c.Valor);
    var outros=f.Cobrancas.Where(c=>c.Situacao=="Aberta"&&c.Origem!="Mensalidade").Select(c=>new ItemExtratoDto(c.Consumidor,c.Vencimento,c.Origem,c.Descricao,c.Valor)).ToArray();
    return new(a.Nome,mensal,outros.Sum(x=>x.Valor),mensal+outros.Sum(x=>x.Valor),outros);
}
