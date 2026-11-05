using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Clube360.Contracts;

var root = Directory.GetCurrentDirectory();
var senha = Guid.NewGuid().ToString("N") + "!aA1";
var logs = new List<Process>();
var checks = 0;
var pastaTeste=Path.Combine(Path.GetTempPath(),"Clube360-Acesso-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(pastaTeste);
int Porta() { var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); var p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }
var apiUrl = $"http://127.0.0.1:{Porta()}";
var webUrl = $"http://127.0.0.1:{Porta()}";
void Verificar(bool ok, string mensagem) { if (!ok) throw new Exception(mensagem); checks++; }
Process Iniciar(string projeto, string url)
{
    var info = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "src", projeto), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    info.ArgumentList.Add(Path.Combine(info.WorkingDirectory, "bin", "Debug", "net10.0", projeto + ".dll"));
    info.ArgumentList.Add("--urls"); info.ArgumentList.Add(url);
    info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    info.Environment["ApiUrl"] = apiUrl;
    info.Environment["Dados__Provedor"] = "Json";
    info.Environment["Dados__Arquivo"]=Path.Combine(pastaTeste,"clubes.json");
    info.Environment["Dados__ContasArquivo"]=Path.Combine(pastaTeste,"contas.json");
    foreach (var perfil in new[] { "Administrador", "Socio", "Plataforma", "Presidente", "Tesoureiro" })
    {
        info.Environment[$"Acessos__{perfil}__Email"] = perfil.ToLowerInvariant() + "@clube360.test";
        info.Environment[$"Acessos__{perfil}__SenhaHash"] = new PasswordHasher<object>().HashPassword(new object(), senha);
    }
    var p = Process.Start(info)!;
    p.OutputDataReceived += (_, _) => { }; p.ErrorDataReceived += (_, _) => { };
    p.BeginOutputReadLine(); p.BeginErrorReadLine(); logs.Add(p); return p;
}
async Task Aguardar(string url) {
    using var c = new HttpClient();
    for (var i=0;i<80;i++) { try { using var r = await c.GetAsync(url); return; } catch (HttpRequestException) { await Task.Delay(200); } }
    throw new Exception("Servidor não iniciou: " + url);
}
async Task<HttpClient> Entrar(string perfil) {
    var c = new HttpClient { BaseAddress = new Uri(apiUrl) };
    using var r = await c.PostAsJsonAsync("/api/v1/login", new LoginRequest(perfil.ToLowerInvariant()+"@clube360.test", senha));
    Verificar(r.IsSuccessStatusCode, "Login falhou: "+perfil);
    var t = await r.Content.ReadFromJsonAsync<TokenResponse>();
    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t!.AccessToken);
    return c;
}
async Task Codigo(HttpClient c, string path, HttpStatusCode esperado) {
    using var r=await c.GetAsync(path); Verificar(r.StatusCode==esperado, path+": "+r.StatusCode);
}
try
{
    Iniciar("Clube360.Api", apiUrl); Iniciar("Clube360.Web", webUrl);
    await Aguardar(apiUrl+"/api/v1/status"); await Aguardar(webUrl+"/entrar");
    const string lago="10000000-0000-0000-0000-000000000001", rio="10000000-0000-0000-0000-000000000002", alex="20000000-0000-0000-0000-000000000001";
    using var anon=new HttpClient{BaseAddress=new Uri(apiUrl)};
    await Codigo(anon,"/api/v1/sessao",HttpStatusCode.Unauthorized);
    await Codigo(anon,$"/api/v1/clubes/{lago}/associados",HttpStatusCode.Unauthorized);
    await Codigo(anon,"/api/v1/demonstracao/clubes",HttpStatusCode.NotFound);
    using var socio=await Entrar("Socio");
    var s=await socio.GetFromJsonAsync<SessaoDto>("/api/v1/sessao");
    Verificar(s!.Clubes.Count==1 && s.ClubeUnicoId()==Guid.Parse(lago), "Clubes do sócio incorretos.");
    var e=await socio.GetFromJsonAsync<ExtratoDto>($"/api/v1/clubes/{lago}/meu-extrato");
    Verificar(e!.Total==255 && e.Despesas.All(d=>d.Consumidor=="Bia Exemplo"), "Extrato familiar incorreto.");
    await Codigo(socio,$"/api/v1/clubes/{lago}/associados",HttpStatusCode.Forbidden);
    await Codigo(socio,$"/api/v1/clubes/{rio}/meu-extrato",HttpStatusCode.Forbidden);
    await Codigo(socio,$"/api/v1/clubes/{lago}/titulares/{alex}/extrato",HttpStatusCode.Forbidden);
    using var admin=await Entrar("Administrador");
    await Codigo(admin,$"/api/v1/clubes/{lago}/associados",HttpStatusCode.OK);
    await Codigo(admin,$"/api/v1/clubes/{rio}/associados",HttpStatusCode.Forbidden);
    await Codigo(admin,$"/api/v1/clubes/{lago}/titulares/{alex}/extrato",HttpStatusCode.OK);
    using var plataforma=await Entrar("Plataforma");
    var p=await plataforma.GetFromJsonAsync<SessaoDto>("/api/v1/sessao");
    Verificar(p!.Clubes.Count==2,"Plataforma deve listar clubes.");
    await Codigo(plataforma,$"/api/v1/clubes/{lago}/associados",HttpStatusCode.OK);
    using var errado=await anon.PostAsJsonAsync("/api/v1/login",new LoginRequest("socio@clube360.test","incorreta"));
    Verificar(errado.StatusCode==HttpStatusCode.Unauthorized,"Senha incorreta aceita.");
    using var web=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer(),AllowAutoRedirect=false}){BaseAddress=new Uri(webUrl)};
    using var semCsrf=await web.PostAsync("/autenticacao/entrar",new FormUrlEncodedContent(new Dictionary<string,string>{{"email","socio@clube360.test"},{"senha",senha}}));
    Verificar(semCsrf.StatusCode==HttpStatusCode.BadRequest,"Login sem antiforgery: status inesperado " + semCsrf.StatusCode);
    var html=await web.GetStringAsync("/entrar");
    var csrf=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    Verificar(!string.IsNullOrEmpty(csrf),"Token antiforgery ausente.");
    using var login=await web.PostAsync("/autenticacao/entrar",new FormUrlEncodedContent(new Dictionary<string,string>{{"email","socio@clube360.test"},{"senha",senha},{"__RequestVerificationToken",csrf}}));
    Verificar(login.StatusCode==HttpStatusCode.Redirect && login.Headers.Location?.ToString()=="/","Login web falhou.");
    var home=WebUtility.HtmlDecode(await web.GetStringAsync("/"));
    Verificar(home.Contains("Área do associado") && home.Contains("Bia Exemplo") && !home.Contains("Associados do seu clube"),"Tela do sócio incorreta.");
    using var limitado=await anon.PostAsJsonAsync("/api/v1/login",new LoginRequest("socio@clube360.test","incorreta"));
    Verificar((int)limitado.StatusCode==429,"Limite de tentativas ausente.");
    // Nova API após reinício: verifica persistência e os perfis presidente/tesoureiro.
    foreach(var proc in logs){if(!proc.HasExited){proc.Kill(entireProcessTree:true);proc.WaitForExit();}proc.Dispose();}
    logs.Clear();apiUrl=$"http://127.0.0.1:{Porta()}";
    Iniciar("Clube360.Api",apiUrl);await Aguardar(apiUrl+"/api/v1/status");
    using var presidente=await Entrar("Presidente");
    using var tesoureiro=await Entrar("Tesoureiro");
    using var socioNovo=await Entrar("Socio");
    using var super=await Entrar("Plataforma");
    await Codigo(tesoureiro,$"/api/v1/clubes/{lago}/financeiro",HttpStatusCode.OK);
    await Codigo(tesoureiro,$"/api/v1/clubes/{lago}/associados",HttpStatusCode.Forbidden);
    await Codigo(tesoureiro,$"/api/v1/clubes/{rio}/financeiro",HttpStatusCode.Forbidden);
    await Codigo(tesoureiro,$"/api/v1/clubes/{lago}/solicitacoes",HttpStatusCode.Forbidden);
    using(var proibido=await tesoureiro.PostAsJsonAsync($"/api/v1/clubes/{lago}/espacos",new EspacoRequest("Não permitido",100)))
        Verificar(proibido.StatusCode==HttpStatusCode.Forbidden,"Tesoureiro cadastrou espaço.");
    var inicio=DateTimeOffset.UtcNow.AddDays(10);
    using(var r=await socioNovo.PostAsJsonAsync($"/api/v1/clubes/{lago}/reservas",new ReservaRequest(Guid.Parse("40000000-0000-0000-0000-000000000001"),inicio,inicio.AddHours(3),"Festa teste")))
    {
        Verificar(r.IsSuccessStatusCode,"Reserva falhou.");
        var reserva=await r.Content.ReadFromJsonAsync<ReservaDto>();
        var fin=await socioNovo.GetFromJsonAsync<FinanceiroDto>($"/api/v1/clubes/{lago}/financeiro");
        Verificar(fin!.Aberto==505,"Cobrança de aluguel não lançada.");
        using var conflito=await socioNovo.PostAsJsonAsync($"/api/v1/clubes/{lago}/reservas",new ReservaRequest(reserva!.EspacoId,inicio,inicio.AddHours(2),"Conflito"));
        Verificar(conflito.StatusCode==HttpStatusCode.BadRequest,"Reserva sobreposta aceita.");
        using var cancelamento=await socioNovo.PostAsync($"/api/v1/clubes/{lago}/reservas/{reserva.Id}/cancelar",null);
        Verificar(cancelamento.IsSuccessStatusCode,"Cancelamento permitido falhou.");
        fin=await socioNovo.GetFromJsonAsync<FinanceiroDto>($"/api/v1/clubes/{lago}/financeiro");
        Verificar(fin!.Aberto==255,"Cobrança cancelada permaneceu em aberto.");
    }
    Guid ticketId;
    using(var r=await socioNovo.PostAsJsonAsync($"/api/v1/clubes/{lago}/solicitacoes",new SolicitacaoRequest("Reclamacao","Teste de retorno","Descrição do problema.")))
    {Verificar(r.IsSuccessStatusCode,"Solicitação falhou.");ticketId=(await r.Content.ReadFromJsonAsync<SolicitacaoDto>())!.Id;}
    using(var r=await socioNovo.PostAsJsonAsync($"/api/v1/clubes/{lago}/solicitacoes/{ticketId}/andamentos",new AndamentoRequest("Concluida","Tentativa indevida")))
        Verificar(r.StatusCode==HttpStatusCode.Forbidden,"Sócio respondeu pela administração.");
    using(var r=await presidente.PostAsJsonAsync($"/api/v1/clubes/{lago}/solicitacoes/{ticketId}/andamentos",new AndamentoRequest("EmAnalise","Recebido pela administração.")))
        Verificar(r.IsSuccessStatusCode,"Presidente não analisou solicitação.");
    var tickets=await socioNovo.GetFromJsonAsync<List<SolicitacaoDto>>($"/api/v1/clubes/{lago}/solicitacoes");
    Verificar(tickets!.Single().Status=="EmAnalise"&&tickets!.Single().Historico.Count==2,"Sócio não acompanhou status.");
    var depReq=new AssociadoRequest{Nome="Dependente de teste",Cpf="52998224725",Categoria="Dependente",TitularId=Guid.Parse(alex),Parentesco="Mae",DataNascimento=new(1960,1,1),Interesses=["comida"]};
    PessoaDto dep;
    using(var r=await presidente.PostAsJsonAsync($"/api/v1/clubes/{lago}/associados",depReq))
    {Verificar(r.IsSuccessStatusCode,"Cadastro dependente falhou.");dep=(await r.Content.ReadFromJsonAsync<PessoaDto>())!;}
    var senhaCurta=System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000,10000).ToString();
    using(var r=await presidente.PostAsJsonAsync($"/api/v1/clubes/{lago}/usuarios",new NovoAcessoRequest("Dependente teste","dependente@clube360.test",senhaCurta,"Socio",dep.Id)))
    {Verificar(r.IsSuccessStatusCode,"Conta de dependente falhou.");Verificar(!(await r.Content.ReadAsStringAsync()).Contains("SenhaHash",StringComparison.OrdinalIgnoreCase),"Hash exposto.");}
    using var dependente=new HttpClient{BaseAddress=new Uri(apiUrl)};
    using(var r=await dependente.PostAsJsonAsync("/api/v1/login",new LoginRequest("dependente@clube360.test",senhaCurta)))
    {Verificar(r.IsSuccessStatusCode,"Login de 4 caracteres falhou.");var t=await r.Content.ReadFromJsonAsync<TokenResponse>();dependente.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",t!.AccessToken);}
    var cadastroDep=await dependente.GetFromJsonAsync<MeuCadastroDto>($"/api/v1/clubes/{lago}/meu-cadastro");
    Verificar(cadastroDep!.Associado.Id==dep.Id,"Identidade de dependente confundida com titular.");
    var protocolosDep=await dependente.GetFromJsonAsync<List<SolicitacaoDto>>($"/api/v1/clubes/{lago}/solicitacoes");
    Verificar(protocolosDep!.Count==0,"Solicitações do titular expostas ao dependente.");
    var extratoDep=await dependente.GetFromJsonAsync<ExtratoDto>($"/api/v1/clubes/{lago}/meu-extrato");
    Verificar(extratoDep!.Mensalidade==0&&extratoDep.Total==0,"Dependente recebeu mensalidade do titular.");
    using(var r=await presidente.PostAsJsonAsync($"/api/v1/clubes/{lago}/usuarios",new NovoAcessoRequest("Admin delegado","delegado@clube360.test",senhaCurta,"Administrador")))
        Verificar(r.IsSuccessStatusCode,"Presidente não liberou administrador.");
    using(var r=await presidente.PostAsJsonAsync($"/api/v1/clubes/{lago}/usuarios",new NovoAcessoRequest("Elevação indevida","indevido@clube360.test",senhaCurta,"SuperAdmin")))
        Verificar(r.StatusCode==HttpStatusCode.Forbidden,"Presidente elevou para super admin.");
    using(var r=await presidente.PostAsJsonAsync($"/api/v1/clubes/{rio}/usuarios",new NovoAcessoRequest("Outro clube","outro@clube360.test",senhaCurta,"Administrador")))
        Verificar(r.StatusCode==HttpStatusCode.Forbidden,"Presidente liberou outro clube.");
    Guid evento;
    using(var r=await presidente.PostAsJsonAsync($"/api/v1/clubes/{lago}/eventos",new EventoRequest("Jantar dia dos Pais","Teste",inicio.AddDays(1),inicio.AddDays(1).AddHours(3),"SomenteSocios",40)))
    {Verificar(r.IsSuccessStatusCode,"Criação evento falhou.");evento=(await r.Content.ReadFromJsonAsync<EventoDto>())!.Id;}
    var convidado=new AssociadoRequest{Nome="Convidado de teste",Cpf="11144477735",Categoria="Convidado",DataNascimento=new(1990,1,1)};
    Guid convite;
    using(var r=await socioNovo.PostAsJsonAsync($"/api/v1/clubes/{lago}/convites",new ConviteRequest(evento,convidado)))
    {Verificar(r.IsSuccessStatusCode,"Convite falhou.");var c=await r.Content.ReadFromJsonAsync<ConviteDto>();convite=c!.Id;Verificar(c.Status=="Pendente","Convite aprovado automaticamente.");}
    using(var r=await socioNovo.PostAsJsonAsync($"/api/v1/clubes/{lago}/convites/{convite}/decisao",new DecisaoRequest(true,"Indevido")))
        Verificar(r.StatusCode==HttpStatusCode.Forbidden,"Sócio aprovou próprio convite.");
    using(var r=await presidente.PostAsJsonAsync($"/api/v1/clubes/{lago}/convites/{convite}/decisao",new DecisaoRequest(true,"Participação autorizada.")))
        Verificar(r.IsSuccessStatusCode,"Aprovação de convidado falhou.");
    using(var r=await presidente.PostAsJsonAsync($"/api/v1/clubes/{lago}/convites/{convite}/decisao",new DecisaoRequest(true,"Duplicado")))
        Verificar(r.StatusCode==HttpStatusCode.BadRequest,"Aprovação duplicada aceita.");
    var privacidade=await dependente.GetFromJsonAsync<List<ConviteDto>>($"/api/v1/clubes/{lago}/convites");
    Verificar(privacidade!.Count==0,"Convites de outro associado expostos.");
    using var anonimo=new HttpClient{BaseAddress=new Uri(apiUrl)};
    var publicos=await anonimo.GetFromJsonAsync<List<EventoDto>>("/api/v1/eventos-publicos");
    Verificar(publicos!.All(x=>x.Id!=evento),"Evento restrito exposto como público.");
    using(var r=await anonimo.PostAsJsonAsync($"/api/v1/eventos-publicos/{evento}/inscricoes",new VisitanteRequest("Visitante","12345678909","visitante@example.test")))
        Verificar(r.StatusCode==HttpStatusCode.BadRequest,"Visitante entrou em evento restrito.");
    using(var r=await super.PostAsJsonAsync("/api/v1/clubes",new ClubeRequest{Nome="Clube terceiro",Sigla="T3",Cidade="Cidade",Uf="PR"}))
        Verificar(r.IsSuccessStatusCode,"Super admin não criou clube.");
    var catalogo=await super.GetFromJsonAsync<List<ClubeDto>>("/api/v1/clubes");
    Verificar(catalogo!.Count==3,"Novo clube não disponível.");
    var cobrancas=await socioNovo.GetFromJsonAsync<FinanceiroDto>($"/api/v1/clubes/{lago}/financeiro");
    var boleto=await socioNovo.GetFromJsonAsync<BoletoDto>($"/api/v1/clubes/{lago}/financeiro/{cobrancas!.Cobrancas.First().Id}/boleto");
    Verificar(!boleto!.Disponivel&&boleto.Url is null,"Boleto fictício emitido.");

    depReq.Ativo=false;
    using(var r=await presidente.PutAsJsonAsync($"/api/v1/clubes/{lago}/associados/{dep.Id}",depReq))
        Verificar(r.IsSuccessStatusCode,"Edição do dependente falhou.");
    await Codigo(dependente,$"/api/v1/clubes/{lago}/meu-cadastro",HttpStatusCode.Forbidden);

    Console.WriteLine($"{checks} verificações de acesso e fluxo HTTP concluídas com sucesso.");
}
finally { foreach (var p in logs) { if (!p.HasExited) p.Kill(entireProcessTree:true); p.Dispose(); } Directory.Delete(pastaTeste,true); }

static class ExtensoesSessao { public static Guid ClubeUnicoId(this SessaoDto s) => s.Clubes.Single().Id; }
