using System.Security.Cryptography;
using ClubeDaOnca.Api.Infrastructure;
using ClubeDaOnca.Contracts.Associacoes;
using ClubeDaOnca.Contracts.Socios;
using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Contratos;
using ClubeDaOnca.Domain.Comum;
using ClubeDaOnca.Domain.Seguranca;
using ClubeDaOnca.Domain.Socios;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Endpoints;

/// <summary>Cadastro de sócios titulares e dependentes (secretaria e admin).</summary>
public static class SociosEndpoints
{
    public static IEndpointRouteBuilder MapSocios(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/socios").WithTags("Sócios").RequireAuthorization("Secretaria");

        g.MapGet("/", async (AppDbContext db, string? busca, string? genero, string? situacao, int? pagina, int? tamanhoPagina, CancellationToken ct) =>
        {
            var pag = Math.Max(pagina ?? 1, 1);
            var tam = Math.Clamp(tamanhoPagina ?? 20, 1, 100);

            var query = db.Socios.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(genero))
            {
                if (!TentarEnum<Genero>(genero, out var gen)) return Results.ValidationProblem(Erro("genero", MensagemGenero));
                query = query.Where(s => s.Genero == gen);
            }
            if (!string.IsNullOrWhiteSpace(situacao))
            {
                if (!TentarEnum<SituacaoTitulo>(situacao, out var sit))
                    return Results.ValidationProblem(Erro("situacao", "Situação inválida. Use Ativo, Suspenso, Cancelado ou EmAdesao."));
                query = query.Where(s => s.Situacao == sit);
            }
            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.Trim();
                var digitos = Cpf.Normalizar(termo);
                if (int.TryParse(termo, out var numero))
                    query = query.Where(s => s.NumeroTitulo == numero || s.Cpf.StartsWith(digitos));
                else if (digitos.Length >= 3 && digitos.Length == termo.Count(c => c is not ('.' or '-' or ' ')))
                    query = query.Where(s => s.Cpf.StartsWith(digitos));
                else
                    query = query.Where(s => EF.Functions.ILike(s.Nome, $"%{termo}%"));
            }

            var total = await query.CountAsync(ct);
            var linhas = await query.OrderBy(s => s.Nome)
                .Skip((pag - 1) * tam).Take(tam)
                .Select(s => new
                {
                    s.Id, s.NumeroTitulo, s.Nome, s.Cpf, s.Categoria, s.Situacao, s.Genero,
                    Dependentes = s.Dependentes.Count(d => d.Ativo),
                    Contrato = db.Contratos.Where(c => c.SocioId == s.Id).OrderByDescending(c => c.CriadoEm)
                        .Select(c => (StatusContrato?)c.Status).FirstOrDefault(),
                })
                .ToListAsync(ct);
            var itens = linhas.Select(s => new SocioResumoDto(s.Id, s.NumeroTitulo, s.Nome, s.Cpf, s.Categoria.ToString(),
                s.Situacao.ToString(), s.Dependentes, s.Genero.ToString(), s.Contrato?.ToString())).ToList();

            return Results.Ok(new PaginaDto<SocioResumoDto>(itens, total, pag, tam));
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var socio = await BuscarAsync(db, id, ct);
            return socio is null ? NaoEncontrado() : Results.Ok(await MapearAsync(db, socio, ct));
        });

        // Histórico do cadastro (titular e dependentes): quem alterou, quando e o que mudou.
        g.MapGet("/{id:guid}/historico", async (Guid id, int? limite, AppDbContext db, CancellationToken ct) =>
        {
            if (!await db.Socios.AnyAsync(s => s.Id == id, ct)) return NaoEncontrado();
            var itens = await db.Historico.AsNoTracking()
                .Where(h => h.RaizId == id)
                .OrderByDescending(h => h.Em).ThenBy(h => h.Campo)
                .Take(Math.Clamp(limite ?? 50, 1, 500))
                .Select(h => new HistoricoDto(h.Em, h.UsuarioNome, h.Acao.ToString(), h.Descricao, h.Campo, h.ValorAnterior, h.ValorNovo))
                .ToListAsync(ct);
            return Results.Ok(itens);
        });

        g.MapPost("/", async (SalvarSocioRequest req, AppDbContext db, UserManager<Usuario> users, CancellationToken ct) =>
        {
            var erros = Validar(req, out var categoria);
            if (erros.Count > 0) return Results.ValidationProblem(erros);

            var cpf = Cpf.Normalizar(req.Cpf);
            var email = req.Email!.Trim().ToLowerInvariant();
            if (await db.Socios.AnyAsync(s => s.Cpf == cpf, ct))
                return Conflito("Já existe um sócio com este CPF.");

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Reaproveita a conta se a pessoa já tiver uma (ex.: funcionário que também é sócio).
            string? senhaProvisoria = null;
            var usuario = await users.Users.FirstOrDefaultAsync(u => u.Cpf == cpf, ct) ?? await users.FindByEmailAsync(email);
            var associacaoId = db.Tenant.Exigir();
            if (usuario is not null)
            {
                if (usuario.AssociacaoId is { } outra && outra != associacaoId)
                    return Conflito("Esta pessoa (e-mail ou CPF) já tem conta em outra associação.");
                if (usuario.Cpf is not null && usuario.Cpf != cpf)
                    return Conflito("Este e-mail já pertence a uma conta com outro CPF.");
                if (await db.Socios.AnyAsync(s => s.UsuarioId == usuario.Id, ct))
                    return Conflito("Esta conta já está vinculada a outro título.");
                if (usuario.Cpf is null || usuario.AssociacaoId is null)
                {
                    usuario.Cpf ??= cpf;
                    usuario.AssociacaoId = associacaoId;
                    await users.UpdateAsync(usuario);
                }
            }
            else
            {
                senhaProvisoria = GerarSenhaProvisoria();
                usuario = new Usuario
                {
                    UserName = email,
                    Email = email,
                    Nome = req.Nome.Trim(),
                    Cpf = cpf,
                    PhoneNumber = req.Telefone?.Trim(),
                    DeveTrocarSenha = true,
                    AssociacaoId = associacaoId,
                };
                var criado = await users.CreateAsync(usuario, senhaProvisoria);
                if (!criado.Succeeded)
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["email"] = criado.Errors.Select(e => e.Code == "DuplicateEmail" || e.Code == "DuplicateUserName"
                            ? "Este e-mail já está em uso." : e.Description).ToArray(),
                    });
            }

            if (!await users.IsInRoleAsync(usuario, Perfis.Titular))
                await users.AddToRoleAsync(usuario, Perfis.Titular);

            // Candidato: sem acesso, reservas ou mensalidade até o presidente aprovar o contrato de adesão.
            var socio = new Socio { UsuarioId = usuario.Id, Situacao = SituacaoTitulo.EmAdesao };
            Aplicar(socio, req, categoria);
            db.Socios.Add(socio);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.Created($"/socios/{socio.Id}",
                new SocioCriadoResponse(await MapearAsync(db, socio, ct), usuario.Email ?? email, senhaProvisoria));
        });

        g.MapPut("/{id:guid}", async (Guid id, SalvarSocioRequest req, AppDbContext db, UserManager<Usuario> users, CancellationToken ct) =>
        {
            var erros = Validar(req, out var categoria);
            if (erros.Count > 0) return Results.ValidationProblem(erros);

            var socio = await BuscarAsync(db, id, ct);
            if (socio is null) return NaoEncontrado();

            var cpf = Cpf.Normalizar(req.Cpf);
            if (await db.Socios.AnyAsync(s => s.Cpf == cpf && s.Id != id, ct))
                return Conflito("Já existe um sócio com este CPF.");

            // Mantém a conta de acesso coerente com o cadastro.
            if (socio.UsuarioId is not null && await users.FindByIdAsync(socio.UsuarioId) is { } usuario)
            {
                var email = req.Email!.Trim().ToLowerInvariant();
                if (!string.Equals(usuario.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    if (await users.FindByEmailAsync(email) is not null)
                        return Conflito("Este e-mail já está em uso por outra conta.");
                    await users.SetEmailAsync(usuario, email);
                    await users.SetUserNameAsync(usuario, email);
                }
                usuario.Nome = req.Nome.Trim();
                usuario.Cpf = cpf;
                await users.UpdateAsync(usuario);
            }

            Aplicar(socio, req, categoria);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await MapearAsync(db, socio, ct));
        });

        g.MapPatch("/{id:guid}/situacao", async (Guid id, AlterarSituacaoRequest req, AppDbContext db, CancellationToken ct) =>
        {
            if (!TentarEnum<SituacaoTitulo>(req.Situacao, out var situacao))
                return Results.ValidationProblem(Erro("situacao", "Situação inválida. Use Ativo, Suspenso ou Cancelado."));

            var socio = await BuscarAsync(db, id, ct);
            if (socio is null) return NaoEncontrado();

            socio.Situacao = situacao;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await MapearAsync(db, socio, ct));
        });

        // Gera nova chave de QR: os códigos antigos (ex.: de um celular perdido) deixam de valer.
        g.MapPost("/{id:guid}/renovar-qr", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var socio = await BuscarAsync(db, id, ct);
            if (socio is null) return NaoEncontrado();

            socio.VersaoChaveQr++;
            foreach (var d in socio.Dependentes) d.VersaoChaveQr++;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/dependentes", async (Guid id, SalvarDependenteRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var erros = ValidarDependente(req, out var parentesco);
            if (erros.Count > 0) return Results.ValidationProblem(erros);

            var socio = await BuscarAsync(db, id, ct);
            if (socio is null) return NaoEncontrado();

            var cpf = string.IsNullOrWhiteSpace(req.Cpf) ? null : Cpf.Normalizar(req.Cpf);
            if (cpf is not null && await CpfEmUsoAsync(db, cpf, null, ct))
                return Conflito("Este CPF já está cadastrado.");

            var dependente = new Dependente { SocioId = socio.Id, Socio = socio };
            AplicarDependente(dependente, req, cpf, parentesco);
            db.Dependentes.Add(dependente);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/socios/{id}/dependentes/{dependente.Id}", MapearDependente(dependente));
        });

        g.MapPut("/{id:guid}/dependentes/{dependenteId:guid}", async (Guid id, Guid dependenteId, SalvarDependenteRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var erros = ValidarDependente(req, out var parentesco);
            if (erros.Count > 0) return Results.ValidationProblem(erros);

            var dependente = await db.Dependentes.Include(d => d.Socio).FirstOrDefaultAsync(d => d.Id == dependenteId && d.SocioId == id, ct);
            if (dependente is null) return NaoEncontrado("Dependente não encontrado.");

            var cpf = string.IsNullOrWhiteSpace(req.Cpf) ? null : Cpf.Normalizar(req.Cpf);
            if (cpf is not null && await CpfEmUsoAsync(db, cpf, dependenteId, ct))
                return Conflito("Este CPF já está cadastrado.");

            AplicarDependente(dependente, req, cpf, parentesco);
            await db.SaveChangesAsync(ct);
            return Results.Ok(MapearDependente(dependente));
        });

        // Dependentes não são apagados (histórico de acessos); apenas inativados.
        g.MapDelete("/{id:guid}/dependentes/{dependenteId:guid}", (Guid id, Guid dependenteId, AppDbContext db, CancellationToken ct) =>
            DefinirAtivoAsync(db, id, dependenteId, false, ct));

        g.MapPost("/{id:guid}/dependentes/{dependenteId:guid}/reativar", (Guid id, Guid dependenteId, AppDbContext db, CancellationToken ct) =>
            DefinirAtivoAsync(db, id, dependenteId, true, ct));

        return app;
    }

    private static async Task<IResult> DefinirAtivoAsync(AppDbContext db, Guid socioId, Guid dependenteId, bool ativo, CancellationToken ct)
    {
        var dependente = await db.Dependentes.Include(d => d.Socio).FirstOrDefaultAsync(d => d.Id == dependenteId && d.SocioId == socioId, ct);
        if (dependente is null) return NaoEncontrado("Dependente não encontrado.");
        dependente.Ativo = ativo;
        await db.SaveChangesAsync(ct);
        return Results.Ok(MapearDependente(dependente));
    }

    private static Task<Socio?> BuscarAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        db.Socios.Include(s => s.Dependentes).FirstOrDefaultAsync(s => s.Id == id, ct);

    private static async Task<bool> CpfEmUsoAsync(AppDbContext db, string cpf, Guid? ignorarDependente, CancellationToken ct) =>
        await db.Socios.AnyAsync(s => s.Cpf == cpf, ct)
        || await db.Dependentes.AnyAsync(d => d.Cpf == cpf && d.Id != ignorarDependente, ct);

    private static void Aplicar(Socio socio, SalvarSocioRequest req, CategoriaTitulo categoria)
    {
        socio.Nome = req.Nome.Trim();
        socio.Cpf = Cpf.Normalizar(req.Cpf);
        socio.Email = req.Email!.Trim().ToLowerInvariant();
        socio.Telefone = string.IsNullOrWhiteSpace(req.Telefone) ? null : req.Telefone.Trim();
        socio.DataNascimento = req.DataNascimento;
        socio.Categoria = categoria;
        socio.Genero = TentarEnum<Genero>(req.Genero, out var genero) ? genero : Genero.NaoInformado;
        socio.Profissao = Vazio(req.Profissao);
        socio.Nacionalidade = Vazio(req.Nacionalidade);
        socio.EstadoCivil = TentarEnum<EstadoCivil>(req.EstadoCivil, out var estadoCivil) ? estadoCivil : EstadoCivil.NaoInformado;
        socio.Rg = Vazio(req.Rg);
        if (req.DataCadastro is { } dataCadastro) socio.DataCadastro = dataCadastro;
        AplicarEndereco(socio.Endereco, req.Endereco);
    }

    /// <summary>Altera o endereço no próprio objeto (assim o histórico registra só os campos que mudaram).</summary>
    internal static void AplicarEndereco(Endereco destino, EnderecoDto? e)
    {
        destino.Cep = Vazio(e?.Cep) is { } cep ? Cpf.Normalizar(cep) : null;
        destino.Logradouro = Vazio(e?.Logradouro);
        destino.Numero = Vazio(e?.Numero);
        destino.Complemento = Vazio(e?.Complemento);
        destino.Bairro = Vazio(e?.Bairro);
        destino.Cidade = Vazio(e?.Cidade);
        destino.Uf = Vazio(e?.Uf)?.ToUpperInvariant();
    }

    internal static Dictionary<string, string[]> ValidarEndereco(EnderecoDto? endereco)
    {
        var erros = new Dictionary<string, string[]>();
        if (endereco?.Cep is { Length: > 0 } cep && Cpf.Normalizar(cep).Length != 8)
            erros["endereco.cep"] = ["CEP deve ter 8 dígitos."];
        if (endereco?.Uf is { Length: > 0 } uf && (uf.Trim().Length != 2 || !uf.Trim().All(char.IsLetter)))
            erros["endereco.uf"] = ["Use a sigla do estado (ex.: SP)."];
        return erros;
    }

    private static string? Vazio(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private const string MensagemGenero = "Gênero inválido. Use Masculino, Feminino, Outro ou NaoInformado.";

    private static void AplicarDependente(Dependente d, SalvarDependenteRequest req, string? cpf, Parentesco parentesco)
    {
        d.Nome = req.Nome.Trim();
        d.Cpf = cpf;
        d.DataNascimento = req.DataNascimento;
        d.Parentesco = parentesco;
        d.Genero = TentarEnum<Genero>(req.Genero, out var genero) ? genero : Genero.NaoInformado;
        d.Email = Vazio(req.Email)?.ToLowerInvariant();
        d.Telefone = Vazio(req.Telefone);
        d.Profissao = Vazio(req.Profissao);
    }

    private static Dictionary<string, string[]> Validar(SalvarSocioRequest req, out CategoriaTitulo categoria)
    {
        var erros = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Nome) || req.Nome.Trim().Length < 3)
            erros["nome"] = ["Informe o nome completo."];
        if (!Cpf.EhValido(req.Cpf))
            erros["cpf"] = ["CPF inválido."];
        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@') || req.Email.Trim().Length > 256)
            erros["email"] = ["Informe um e-mail válido. Ele é usado para o acesso ao app."];
        if (!DataNascimentoValida(req.DataNascimento))
            erros["dataNascimento"] = ["Data de nascimento inválida."];
        if (!TentarEnum(req.Categoria, out categoria))
            erros["categoria"] = ["Categoria inválida. Use Individual, Familiar ou Remido."];
        if (!string.IsNullOrWhiteSpace(req.Genero) && !TentarEnum<Genero>(req.Genero, out _))
            erros["genero"] = [MensagemGenero];
        if (req.Profissao is { Length: > 100 })
            erros["profissao"] = ["Profissão muito longa (máximo 100 caracteres)."];
        if (req.Nacionalidade is { Length: > 60 })
            erros["nacionalidade"] = ["Nacionalidade muito longa (máximo 60 caracteres)."];
        if (!string.IsNullOrWhiteSpace(req.EstadoCivil) && !TentarEnum<EstadoCivil>(req.EstadoCivil, out _))
            erros["estadoCivil"] = ["Estado civil inválido. Use Solteiro, Casado, UniaoEstavel, Separado, Divorciado, Viuvo ou NaoInformado."];
        if (req.Rg is { Length: > 30 })
            erros["rg"] = ["RG muito longo (máximo 30 caracteres)."];
        if (req.DataCadastro is { } dc && !DataNascimentoValida(dc))
            erros["dataCadastro"] = ["Data de cadastro inválida (não pode ser futura)."];
        foreach (var (campo, msgs) in ValidarEndereco(req.Endereco)) erros[campo] = msgs;
        return erros;
    }

    private static Dictionary<string, string[]> ValidarDependente(SalvarDependenteRequest req, out Parentesco parentesco)
    {
        var erros = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Nome) || req.Nome.Trim().Length < 3)
            erros["nome"] = ["Informe o nome completo."];
        if (!string.IsNullOrWhiteSpace(req.Cpf) && !Cpf.EhValido(req.Cpf))
            erros["cpf"] = ["CPF inválido."];
        if (!DataNascimentoValida(req.DataNascimento))
            erros["dataNascimento"] = ["Data de nascimento inválida."];
        if (!TentarEnum(req.Parentesco, out parentesco))
            erros["parentesco"] = ["Parentesco inválido. Use Conjuge, Filho, Enteado, Pai, Mae ou Outro."];
        if (!string.IsNullOrWhiteSpace(req.Genero) && !TentarEnum<Genero>(req.Genero, out _))
            erros["genero"] = [MensagemGenero];
        if (!string.IsNullOrWhiteSpace(req.Email) && (!req.Email.Contains('@') || req.Email.Trim().Length > 256))
            erros["email"] = ["E-mail inválido."];
        return erros;
    }

    private static bool DataNascimentoValida(DateOnly data) =>
        data.Year >= 1900 && data <= DateOnly.FromDateTime(DateTime.Today);

    private static bool TentarEnum<T>(string? valor, out T resultado) where T : struct, Enum =>
        Enum.TryParse(valor, ignoreCase: true, out resultado) && Enum.IsDefined(resultado)
        && !int.TryParse(valor, out _); // não aceita números ("1") no lugar do nome

    internal static string GerarSenhaProvisoria()
    {
        // Atende às regras do Identity (8+ caracteres, maiúscula, minúscula e número),
        // sem caracteres ambíguos (0/O, 1/l/I) para facilitar o ditado ao sócio.
        var senha = new List<char>();
        senha.AddRange(RandomNumberGenerator.GetItems<char>("ABCDEFGHJKLMNPQRSTUVWXYZ", 2));
        senha.AddRange(RandomNumberGenerator.GetItems<char>("abcdefghijkmnpqrstuvwxyz", 5));
        senha.AddRange(RandomNumberGenerator.GetItems<char>("23456789", 3));
        var arr = senha.ToArray();
        RandomNumberGenerator.Shuffle(arr.AsSpan());
        return new string(arr);
    }

    /// <summary>Mapeia o sócio com a última alteração registrada no histórico (do titular ou de um dependente).</summary>
    public static async Task<SocioDto> MapearAsync(AppDbContext db, Socio s, CancellationToken ct)
    {
        var ultima = await db.Historico.AsNoTracking().Where(h => h.RaizId == s.Id)
            .OrderByDescending(h => h.Em).Select(h => new { h.Em, h.UsuarioNome }).FirstOrDefaultAsync(ct);
        var contrato = await db.Contratos.AsNoTracking().Where(c => c.SocioId == s.Id)
            .OrderByDescending(c => c.CriadoEm).Select(c => (StatusContrato?)c.Status).FirstOrDefaultAsync(ct);
        return Mapear(s, ultima?.Em, ultima?.UsuarioNome) with { StatusContrato = contrato?.ToString() };
    }

    public static SocioDto Mapear(Socio s, DateTimeOffset? ultimaAlteracaoEm = null, string? ultimaAlteracaoPor = null) => new(
        s.Id, s.NumeroTitulo, s.Nome, s.Cpf, s.Email, s.Telefone, s.DataNascimento,
        s.Categoria.ToString(), s.Situacao.ToString(), s.UsuarioId is not null,
        s.Dependentes.OrderBy(d => d.Nome).Select(MapearDependente).ToList(),
        s.Genero.ToString(), s.Profissao,
        new EnderecoDto(s.Endereco.Cep, s.Endereco.Logradouro, s.Endereco.Numero, s.Endereco.Complemento,
            s.Endereco.Bairro, s.Endereco.Cidade, s.Endereco.Uf),
        s.DataCadastro, s.CriadoEm, ultimaAlteracaoEm, ultimaAlteracaoPor, "Titular",
        s.Nacionalidade, s.EstadoCivil.ToString(), s.Rg);

    private static DependenteDto MapearDependente(Dependente d) =>
        new(d.Id, d.Nome, d.Cpf, d.DataNascimento, d.Parentesco.ToString(), d.Ativo,
            d.Genero.ToString(), d.Email, d.Telefone, d.Profissao, "Dependente", d.Socio?.Nome);

    private static Dictionary<string, string[]> Erro(string campo, string mensagem) => new() { [campo] = [mensagem] };

    private static IResult Conflito(string mensagem) => Results.Problem(title: mensagem, statusCode: StatusCodes.Status409Conflict);

    private static IResult NaoEncontrado(string mensagem = "Sócio não encontrado.") =>
        Results.Problem(title: mensagem, statusCode: StatusCodes.Status404NotFound);
}
