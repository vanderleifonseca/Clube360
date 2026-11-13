using System.Security.Claims;
using ClubeDaOnca.Api.Infrastructure;
using ClubeDaOnca.Api.Infrastructure.Pagamentos;
using ClubeDaOnca.Contracts.Associacoes;
using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Comum;
using ClubeDaOnca.Domain.Seguranca;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Endpoints;

/// <summary>
/// Associações (unidades): o SuperAdmin cadastra novas unidades; o Admin de cada unidade mantém os dados,
/// o presidente (mandato de 2 anos) e a diretoria da sua.
/// </summary>
public static class AssociacoesEndpoints
{
    public static IEndpointRouteBuilder MapAssociacoes(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/associacoes").WithTags("Associações");

        g.MapGet("/", async (AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            var lista = await db.Associacoes.AsNoTracking().Include(a => a.Mandatos).OrderBy(a => a.Nome).ToListAsync(ct);
            return Results.Ok(lista.Select(a =>
            {
                var presidente = RegrasAssociacao.PresidenteEm(a.Mandatos, relogio.Hoje);
                return new AssociacaoResumoDto(a.Id, a.Nome, a.Cnpj, a.Endereco.Cidade, a.Endereco.Uf, a.Ativa,
                    presidente?.Nome, presidente?.Fim, RegrasAssociacao.MandatoVencendo(a.Mandatos, relogio.Hoje));
            }).ToList());
        }).RequireAuthorization("SuperAdmin");

        // Dados da associação do usuário logado (nome, presidente, diretoria): visível a todos dela.
        g.MapGet("/atual", async (AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            if (db.Tenant.AssociacaoId is not { } id) return NaoEncontrado();
            var a = await BuscarAsync(db, id, ct);
            return a is null ? NaoEncontrado() : Results.Ok(Mapear(a, relogio.Hoje));
        }).RequireAuthorization();

        g.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            if (!PodeGerir(user, db, id)) return Results.Forbid();
            var a = await BuscarAsync(db, id, ct);
            return a is null ? NaoEncontrado() : Results.Ok(Mapear(a, relogio.Hoje));
        }).RequireAuthorization("AdminOuSuperAdmin");

        g.MapPost("/", async (NovaAssociacaoRequest req, AppDbContext db, UserManager<Usuario> users, RelogioClube relogio, CancellationToken ct) =>
        {
            var erros = Validar(req.Nome, req.Cnpj, req.Email, req.Endereco);
            if (string.IsNullOrWhiteSpace(req.AdminNome) || req.AdminNome.Trim().Length < 3)
                erros["adminNome"] = ["Informe o nome do administrador da unidade."];
            if (!EmailValido(req.AdminEmail))
                erros["adminEmail"] = ["Informe o e-mail do administrador da unidade. Ele é o login de acesso."];
            if (erros.Count > 0) return Results.ValidationProblem(erros);

            var nome = req.Nome.Trim();
            var cnpj = NormalizarCnpj(req.Cnpj);
            if (await db.Associacoes.AnyAsync(a => a.Nome == nome, ct))
                return Conflito("Já existe uma associação com este nome.");
            if (cnpj is not null && await db.Associacoes.AnyAsync(a => a.Cnpj == cnpj, ct))
                return Conflito("Já existe uma associação com este CNPJ.");

            var adminEmail = req.AdminEmail.Trim().ToLowerInvariant();
            if (await users.FindByEmailAsync(adminEmail) is not null)
                return Conflito("O e-mail do administrador já está em uso por outra conta.");

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var associacao = new Associacao();
            Aplicar(associacao, req.Nome, cnpj, req.Email, req.Telefone, req.Endereco);
            db.Associacoes.Add(associacao);
            await db.SaveChangesAsync(ct);

            var senha = SociosEndpoints.GerarSenhaProvisoria();
            var admin = new Usuario
            {
                UserName = adminEmail, Email = adminEmail, Nome = req.AdminNome.Trim(),
                AssociacaoId = associacao.Id, DeveTrocarSenha = true,
            };
            var criado = await users.CreateAsync(admin, senha);
            if (!criado.Succeeded)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["adminEmail"] = criado.Errors.Select(e => e.Description).ToArray(),
                });
            await users.AddToRoleAsync(admin, Perfis.Admin);
            await tx.CommitAsync(ct);

            return Results.Created($"/associacoes/{associacao.Id}",
                new AssociacaoCriadaResponse(Mapear(associacao, relogio.Hoje), adminEmail, senha));
        }).RequireAuthorization("SuperAdmin");

        g.MapPut("/{id:guid}", async (Guid id, SalvarAssociacaoRequest req, ClaimsPrincipal user, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            if (!PodeGerir(user, db, id)) return Results.Forbid();
            var erros = Validar(req.Nome, req.Cnpj, req.Email, req.Endereco);
            if (erros.Count > 0) return Results.ValidationProblem(erros);

            var a = await BuscarAsync(db, id, ct);
            if (a is null) return NaoEncontrado();

            var nome = req.Nome.Trim();
            var cnpj = NormalizarCnpj(req.Cnpj);
            if (await db.Associacoes.AnyAsync(x => x.Nome == nome && x.Id != id, ct))
                return Conflito("Já existe uma associação com este nome.");
            if (cnpj is not null && await db.Associacoes.AnyAsync(x => x.Cnpj == cnpj && x.Id != id, ct))
                return Conflito("Já existe uma associação com este CNPJ.");

            Aplicar(a, req.Nome, cnpj, req.Email, req.Telefone, req.Endereco);
            await db.SaveChangesAsync(ct);
            return Results.Ok(Mapear(a, relogio.Hoje));
        }).RequireAuthorization("AdminOuSuperAdmin");

        // Desativar bloqueia o login de todos os usuários da unidade (os dados são mantidos).
        g.MapPatch("/{id:guid}/ativa", async (Guid id, AlterarAtivaRequest req, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            if (!req.Ativa && db.Tenant.AssociacaoId == id)
                return Conflito("Você não pode desativar a associação em que está logado.");
            var a = await BuscarAsync(db, id, ct);
            if (a is null) return NaoEncontrado();
            a.Ativa = req.Ativa;
            await db.SaveChangesAsync(ct);
            return Results.Ok(Mapear(a, relogio.Hoje));
        }).RequireAuthorization("SuperAdmin");

        // Conta do Asaas da unidade: as chaves entram criptografadas e nunca voltam pela API.
        g.MapGet("/{id:guid}/pagamentos", async (Guid id, ClaimsPrincipal user, HttpRequest http, AppDbContext db,
            IGatewayPagamento gateway, IConfiguration config, CancellationToken ct) =>
        {
            if (!PodeGerir(user, db, id)) return Results.Forbid();
            var a = await db.Associacoes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return a is null ? NaoEncontrado() : Results.Ok(Pagamentos(a, http, gateway, config));
        }).RequireAuthorization("Admin");

        g.MapPut("/{id:guid}/pagamentos", async (Guid id, SalvarPagamentosAssociacaoRequest req, ClaimsPrincipal user,
            HttpRequest http, AppDbContext db, ContaAsaas conta, IGatewayPagamento gateway, IConfiguration config, CancellationToken ct) =>
        {
            if (!PodeGerir(user, db, id)) return Results.Forbid();
            var a = await db.Associacoes.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (a is null) return NaoEncontrado();

            // Nulo mantém; vazio remove (volta para a conta geral do servidor).
            if (req.ApiKey is not null)
                a.AsaasApiKeyProtegida = string.IsNullOrWhiteSpace(req.ApiKey) ? null : conta.Proteger(req.ApiKey.Trim());
            if (req.WebhookToken is not null)
                a.AsaasWebhookTokenProtegido = string.IsNullOrWhiteSpace(req.WebhookToken) ? null : conta.Proteger(req.WebhookToken.Trim());
            await db.SaveChangesAsync(ct);
            return Results.Ok(Pagamentos(a, http, gateway, config));
        }).RequireAuthorization("Admin");

        MapDiretoria(g);
        return app;
    }

    private static void MapDiretoria(RouteGroupBuilder g)
    {
        g.MapPost("/{id:guid}/mandatos", async (Guid id, SalvarMandatoRequest req, ClaimsPrincipal user, AppDbContext db,
            UserManager<Usuario> users, RelogioClube relogio, CancellationToken ct) =>
        {
            if (!PodeGerir(user, db, id)) return Results.Forbid();
            var a = await BuscarAsync(db, id, ct);
            if (a is null) return NaoEncontrado();

            var mandato = new MandatoPresidente { AssociacaoId = id };
            var erro = AplicarMandato(mandato, req, a.Mandatos);
            if (erro is not null) return erro;

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // O presidente aprova as fichas pelo app: garante uma conta com o e-mail do mandato.
            string? senhaProvisoria = null;
            if (mandato.Email is { } emailPresidente)
            {
                var conta = await users.FindByEmailAsync(emailPresidente);
                if (conta is not null && conta.AssociacaoId != id)
                    return Conflito("O e-mail do presidente pertence a uma conta de outra associação. Use outro e-mail.");
                if (conta is null)
                {
                    senhaProvisoria = SociosEndpoints.GerarSenhaProvisoria();
                    var cpfLivre = mandato.Cpf is { } cpf && !await users.Users.AnyAsync(u => u.Cpf == cpf, ct) ? cpf : null;
                    var criada = await users.CreateAsync(new Usuario
                    {
                        UserName = emailPresidente, Email = emailPresidente, Nome = mandato.Nome, Cpf = cpfLivre,
                        AssociacaoId = id, DeveTrocarSenha = true,
                    }, senhaProvisoria);
                    if (!criada.Succeeded)
                        return Results.ValidationProblem(new Dictionary<string, string[]>
                        {
                            ["email"] = criada.Errors.Select(e => e.Description).ToArray(),
                        });
                }
            }

            db.Mandatos.Add(mandato);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var dto = MapearMandato(mandato, relogio.Hoje);
            if (senhaProvisoria is not null) dto = dto with { Login = mandato.Email, SenhaProvisoria = senhaProvisoria };
            return Results.Created($"/associacoes/{id}/mandatos/{mandato.Id}", dto);
        }).RequireAuthorization("AdminOuSuperAdmin");

        g.MapPut("/{id:guid}/mandatos/{mandatoId:guid}", async (Guid id, Guid mandatoId, SalvarMandatoRequest req, ClaimsPrincipal user, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            if (!PodeGerir(user, db, id)) return Results.Forbid();
            var a = await BuscarAsync(db, id, ct);
            var mandato = a?.Mandatos.FirstOrDefault(m => m.Id == mandatoId);
            if (mandato is null) return NaoEncontrado("Mandato não encontrado.");

            var erro = AplicarMandato(mandato, req, a!.Mandatos);
            if (erro is not null) return erro;

            await db.SaveChangesAsync(ct);
            return Results.Ok(MapearMandato(mandato, relogio.Hoje));
        }).RequireAuthorization("AdminOuSuperAdmin");

        g.MapDelete("/{id:guid}/mandatos/{mandatoId:guid}", async (Guid id, Guid mandatoId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            if (!PodeGerir(user, db, id)) return Results.Forbid();
            var removidos = await db.Mandatos.Where(m => m.Id == mandatoId && m.AssociacaoId == id).ExecuteDeleteAsync(ct);
            return removidos == 0 ? NaoEncontrado("Mandato não encontrado.") : Results.NoContent();
        }).RequireAuthorization("AdminOuSuperAdmin");

        g.MapPost("/{id:guid}/diretores", async (Guid id, SalvarDiretorRequest req, ClaimsPrincipal user, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            if (!PodeGerir(user, db, id)) return Results.Forbid();
            if (!await db.Associacoes.AnyAsync(a => a.Id == id, ct)) return NaoEncontrado();
            var erros = ValidarDiretor(req);
            if (erros.Count > 0) return Results.ValidationProblem(erros);

            var diretor = new Diretor { AssociacaoId = id };
            AplicarDiretor(diretor, req);
            db.Diretores.Add(diretor);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/associacoes/{id}/diretores/{diretor.Id}", MapearDiretor(diretor, relogio.Hoje));
        }).RequireAuthorization("AdminOuSuperAdmin");

        g.MapPut("/{id:guid}/diretores/{diretorId:guid}", async (Guid id, Guid diretorId, SalvarDiretorRequest req, ClaimsPrincipal user, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            if (!PodeGerir(user, db, id)) return Results.Forbid();
            var erros = ValidarDiretor(req);
            if (erros.Count > 0) return Results.ValidationProblem(erros);

            var diretor = await db.Diretores.FirstOrDefaultAsync(d => d.Id == diretorId && d.AssociacaoId == id, ct);
            if (diretor is null) return NaoEncontrado("Diretor não encontrado.");
            AplicarDiretor(diretor, req);
            await db.SaveChangesAsync(ct);
            return Results.Ok(MapearDiretor(diretor, relogio.Hoje));
        }).RequireAuthorization("AdminOuSuperAdmin");

        g.MapDelete("/{id:guid}/diretores/{diretorId:guid}", async (Guid id, Guid diretorId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            if (!PodeGerir(user, db, id)) return Results.Forbid();
            var removidos = await db.Diretores.Where(d => d.Id == diretorId && d.AssociacaoId == id).ExecuteDeleteAsync(ct);
            return removidos == 0 ? NaoEncontrado("Diretor não encontrado.") : Results.NoContent();
        }).RequireAuthorization("AdminOuSuperAdmin");
    }

    private static PagamentosAssociacaoDto Pagamentos(Associacao a, HttpRequest http, IGatewayPagamento gateway, IConfiguration config)
    {
        var baseUrl = config["Contratos:UrlPublica"] is { Length: > 0 } url ? url.TrimEnd('/') : $"{http.Scheme}://{http.Host}";
        return new PagamentosAssociacaoDto(a.AsaasApiKeyProtegida is not null, a.AsaasWebhookTokenProtegido is not null,
            $"{baseUrl}/webhooks/asaas/{a.Id}", gateway.Nome);
    }

    /// <summary>O SuperAdmin gere qualquer unidade; o Admin, só a própria.</summary>
    private static bool PodeGerir(ClaimsPrincipal user, AppDbContext db, Guid associacaoId) =>
        user.IsInRole(Perfis.SuperAdmin) || (user.IsInRole(Perfis.Admin) && db.Tenant.AssociacaoId == associacaoId);

    private static Task<Associacao?> BuscarAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        db.Associacoes.Include(a => a.Mandatos).Include(a => a.Diretores).FirstOrDefaultAsync(a => a.Id == id, ct);

    private static IResult? AplicarMandato(MandatoPresidente mandato, SalvarMandatoRequest req, IEnumerable<MandatoPresidente> existentes)
    {
        var erros = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Nome) || req.Nome.Trim().Length < 3)
            erros["nome"] = ["Informe o nome do presidente."];
        if (!string.IsNullOrWhiteSpace(req.Cpf) && !Cpf.EhValido(req.Cpf))
            erros["cpf"] = ["CPF inválido."];
        if (!string.IsNullOrWhiteSpace(req.Email) && !EmailValido(req.Email))
            erros["email"] = ["E-mail inválido."];
        if (req.Inicio.Year < 1900)
            erros["inicio"] = ["Data de início inválida."];
        if (erros.Count > 0) return Results.ValidationProblem(erros);

        var fim = RegrasAssociacao.FimDoMandato(req.Inicio);
        if (RegrasAssociacao.Sobrepoe(req.Inicio, fim, existentes, mandato.Id))
            return Conflito($"O período {req.Inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy} se sobrepõe a outro mandato.");

        mandato.Nome = req.Nome.Trim();
        mandato.Cpf = string.IsNullOrWhiteSpace(req.Cpf) ? null : Cpf.Normalizar(req.Cpf);
        mandato.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLowerInvariant();
        mandato.Inicio = req.Inicio;
        mandato.Fim = fim;
        return null;
    }

    private static Dictionary<string, string[]> ValidarDiretor(SalvarDiretorRequest req)
    {
        var erros = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Nome) || req.Nome.Trim().Length < 3)
            erros["nome"] = ["Informe o nome do diretor."];
        if (string.IsNullOrWhiteSpace(req.Cargo))
            erros["cargo"] = ["Informe o cargo."];
        if (!string.IsNullOrWhiteSpace(req.Email) && !EmailValido(req.Email))
            erros["email"] = ["E-mail inválido."];
        if (req.Fim is { } fim && fim < req.Inicio)
            erros["fim"] = ["O fim não pode ser antes do início."];
        return erros;
    }

    private static void AplicarDiretor(Diretor d, SalvarDiretorRequest req)
    {
        d.Nome = req.Nome.Trim();
        d.Cargo = req.Cargo.Trim();
        d.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLowerInvariant();
        d.Telefone = string.IsNullOrWhiteSpace(req.Telefone) ? null : req.Telefone.Trim();
        d.Inicio = req.Inicio;
        d.Fim = req.Fim;
    }

    private static Dictionary<string, string[]> Validar(string? nome, string? cnpj, string? email, EnderecoDto? endereco)
    {
        var erros = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(nome) || nome.Trim().Length < 3)
            erros["nome"] = ["Informe o nome da associação."];
        if (!string.IsNullOrWhiteSpace(cnpj) && !Cnpj.EhValido(cnpj))
            erros["cnpj"] = ["CNPJ inválido."];
        if (!string.IsNullOrWhiteSpace(email) && !EmailValido(email))
            erros["email"] = ["E-mail inválido."];
        foreach (var (campo, msgs) in SociosEndpoints.ValidarEndereco(endereco)) erros[campo] = msgs;
        return erros;
    }

    private static void Aplicar(Associacao a, string nome, string? cnpj, string? email, string? telefone, EnderecoDto? e)
    {
        a.Nome = nome.Trim();
        a.Cnpj = cnpj;
        a.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        a.Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim();
        SociosEndpoints.AplicarEndereco(a.Endereco, e);
    }

    private static string? Vazio(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static string? NormalizarCnpj(string? cnpj) => string.IsNullOrWhiteSpace(cnpj) ? null : Cnpj.Normalizar(cnpj);

    private static bool EmailValido(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Trim().Length <= 256;

    public static AssociacaoDto Mapear(Associacao a, DateOnly hoje)
    {
        var presidente = RegrasAssociacao.PresidenteEm(a.Mandatos, hoje);
        return new AssociacaoDto(a.Id, a.Nome, a.Cnpj, a.Email, a.Telefone,
            new EnderecoDto(a.Endereco.Cep, a.Endereco.Logradouro, a.Endereco.Numero, a.Endereco.Complemento,
                a.Endereco.Bairro, a.Endereco.Cidade, a.Endereco.Uf),
            a.Ativa,
            presidente is null ? null : MapearMandato(presidente, hoje),
            RegrasAssociacao.MandatoVencendo(a.Mandatos, hoje),
            a.Mandatos.OrderByDescending(m => m.Inicio).Select(m => MapearMandato(m, hoje)).ToList(),
            a.Diretores.OrderBy(d => d.Cargo).ThenBy(d => d.Nome).Select(d => MapearDiretor(d, hoje)).ToList());
    }

    private static MandatoDto MapearMandato(MandatoPresidente m, DateOnly hoje) =>
        new(m.Id, m.Nome, m.Cpf, m.Email, m.Inicio, m.Fim, m.Inicio <= hoje && hoje <= m.Fim);

    private static DiretorDto MapearDiretor(Diretor d, DateOnly hoje) =>
        new(d.Id, d.Nome, d.Cargo, d.Email, d.Telefone, d.Inicio, d.Fim, d.AtivoEm(hoje));

    private static IResult Conflito(string mensagem) => Results.Problem(title: mensagem, statusCode: StatusCodes.Status409Conflict);

    private static IResult NaoEncontrado(string mensagem = "Associação não encontrada.") =>
        Results.Problem(title: mensagem, statusCode: StatusCodes.Status404NotFound);
}
