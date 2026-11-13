using System.Net;
using System.Security.Claims;
using ClubeDaOnca.Api.Infrastructure;
using ClubeDaOnca.Api.Infrastructure.Contratos;
using ClubeDaOnca.Contracts.Contratos;
using ClubeDaOnca.Domain.Contratos;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Endpoints;

/// <summary>
/// Contrato de adesão: a secretaria gera e envia; o candidato concorda pelo link (página pública /aceite);
/// o presidente do mandato vigente dá o aval pelo app.
/// </summary>
public static class ContratosEndpoints
{
    public static IEndpointRouteBuilder MapContratos(this IEndpointRouteBuilder app)
    {
        MapSecretaria(app);
        MapPresidencia(app);
        MapAceite(app);
        return app;
    }

    private static void MapSecretaria(IEndpointRouteBuilder app)
    {
        var socios = app.MapGroup("/socios/{socioId:guid}/contratos").WithTags("Contratos").RequireAuthorization("Secretaria");

        socios.MapGet("/", async (Guid socioId, AppDbContext db, CancellationToken ct) =>
        {
            var lista = await db.Contratos.AsNoTracking().Include(c => c.Socio)
                .Where(c => c.SocioId == socioId).OrderByDescending(c => c.CriadoEm).ToListAsync(ct);
            return Results.Ok(lista.Select(Mapear).ToList());
        });

        socios.MapPost("/", (Guid socioId, SalvarContratoRequest? req, ContratoService svc, CancellationToken ct) =>
            Executar(async () =>
            {
                var c = await svc.CriarAsync(socioId, req ?? new SalvarContratoRequest(), ct: ct);
                return Results.Created($"/contratos/{c.Id}", Mapear(await svc.BuscarAsync(c.Id, ct, comSocio: true)));
            }));

        var g = app.MapGroup("/contratos").WithTags("Contratos").RequireAuthorization("Secretaria");

        // Lista da gestão: por status e/ou contratos cuja vigência termina nos próximos N dias.
        g.MapGet("/", async (string? status, int? vencendoEmDias, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            var query = db.Contratos.AsNoTracking().Include(c => c.Socio).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<StatusContrato>(status, true, out var st) || !Enum.IsDefined(st))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Status inválido."] });
                query = query.Where(c => c.Status == st);
            }
            if (vencendoEmDias is { } dias)
            {
                var limite = relogio.Hoje.AddDays(Math.Clamp(dias, 0, 365));
                query = query.Where(c => c.Status == StatusContrato.Aprovado && c.VigenciaFim <= limite);
            }
            var lista = await query.OrderByDescending(c => c.CriadoEm).Take(500).ToListAsync(ct);
            return Results.Ok(lista.Select(Mapear).ToList());
        });

        g.MapGet("/{id:guid}", (Guid id, ContratoService svc, CancellationToken ct) =>
            Executar(async () => Results.Ok(Mapear(await svc.BuscarAsync(id, ct, comSocio: true)))));

        g.MapGet("/{id:guid}/documento", (Guid id, ContratoService svc, CancellationToken ct) =>
            Executar(async () => Documento(await svc.BuscarAsync(id, ct))));

        g.MapPut("/{id:guid}", (Guid id, SalvarContratoRequest req, ContratoService svc, CancellationToken ct) =>
            Executar(async () =>
            {
                await svc.AtualizarAsync(id, req, ct);
                return Results.Ok(Mapear(await svc.BuscarAsync(id, ct, comSocio: true)));
            }));

        g.MapPost("/{id:guid}/enviar", (Guid id, HttpRequest http, ContratoService svc, CancellationToken ct) =>
            Executar(async () =>
            {
                await svc.EnviarAsync(id, svc.UrlPublica(http), ct);
                return Results.Ok(Mapear(await svc.BuscarAsync(id, ct, comSocio: true)));
            }));

        // Parcelas do contrato: número, emissão, vencimento, situação e boleto.
        g.MapGet("/{id:guid}/parcelas", async (Guid id, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            var parcelas = await db.Cobrancas.AsNoTracking().Include(c => c.Socio).Include(c => c.Contrato).Include(c => c.Pagamentos)
                .Where(c => c.ContratoId == id).OrderBy(c => c.Parcela).ToListAsync(ct);
            return Results.Ok(parcelas.Select(c => FinanceiroService.Mapear(c, relogio.Hoje)).ToList());
        });

        // Emite agora os boletos que faltam (ex.: o gateway estava fora do ar na aprovação).
        g.MapPost("/{id:guid}/boletos", async (Guid id, AppDbContext db, FinanceiroService fin, CancellationToken ct) =>
        {
            if (!await db.Contratos.AnyAsync(c => c.Id == id && c.Status == StatusContrato.Aprovado, ct))
                return Results.Problem(title: "Os boletos só são emitidos para contratos aprovados.", statusCode: StatusCodes.Status409Conflict);
            var (emitidos, falhas) = await fin.EmitirBoletosContratoAsync(id, ct);
            var total = await db.Cobrancas.CountAsync(c => c.ContratoId == id, ct);
            return Results.Ok(new Contracts.Financeiro.EmissaoBoletosDto(emitidos, falhas, total));
        });

        g.MapPost("/{id:guid}/desligar", (Guid id, MotivoRequest req, ContratoService svc, CancellationToken ct) =>
            Executar(async () => Results.Ok(Mapear(await svc.DesligarAsync(id, req.Motivo, ct)))));

        g.MapPost("/{id:guid}/renovar", (Guid id, ContratoService svc, CancellationToken ct) =>
            Executar(async () =>
            {
                var novo = await svc.RenovarAsync(id, ct);
                return Results.Created($"/contratos/{novo.Id}", Mapear(await svc.BuscarAsync(novo.Id, ct, comSocio: true)));
            }));
    }

    private static void MapPresidencia(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/presidencia").WithTags("Presidência").RequireAuthorization();

        g.MapGet("/resumo", async (ClaimsPrincipal user, ContratoService svc, AppDbContext db, CancellationToken ct) =>
        {
            var mandato = await svc.MandatoVigenteAsync(ct);
            var eh = ContratoService.EhPresidente(user, mandato);
            if (!eh) return Results.Ok(new ResumoPresidenciaDto(false, 0, mandato?.Nome));
            var emAnalise = db.Contratos.Where(c => c.Status == StatusContrato.EmAnalise);
            var pendentes = await emAnalise.CountAsync(ct);
            var ultimoAceite = await emAnalise.MaxAsync(c => c.AceiteEm, ct);
            return Results.Ok(new ResumoPresidenciaDto(true, pendentes, mandato?.Nome, ultimoAceite));
        });

        g.MapGet("/fichas", async (ClaimsPrincipal user, ContratoService svc, AppDbContext db, CancellationToken ct) =>
        {
            if (!ContratoService.EhPresidente(user, await svc.MandatoVigenteAsync(ct))) return NaoEhPresidente();
            var contratos = await db.Contratos.AsNoTracking()
                .Include(c => c.Socio).ThenInclude(s => s!.Dependentes)
                .Where(c => c.Status == StatusContrato.EmAnalise).OrderBy(c => c.AceiteEm).ToListAsync(ct);
            var fichas = new List<FichaAprovacaoDto>();
            foreach (var c in contratos)
                fichas.Add(new FichaAprovacaoDto(Mapear(c), await SociosEndpoints.MapearAsync(db, c.Socio!, ct)));
            return Results.Ok(fichas);
        });

        // Documento da ficha para o presidente (o grupo /contratos é só da secretaria).
        g.MapGet("/fichas/{id:guid}/documento", async (Guid id, ClaimsPrincipal user, ContratoService svc, CancellationToken ct) =>
        {
            if (!ContratoService.EhPresidente(user, await svc.MandatoVigenteAsync(ct))) return NaoEhPresidente();
            return await Executar(async () => Documento(await svc.BuscarAsync(id, ct)));
        });

        g.MapPost("/fichas/{id:guid}/aprovar", async (Guid id, ClaimsPrincipal user, ContratoService svc, CancellationToken ct) =>
        {
            if (!ContratoService.EhPresidente(user, await svc.MandatoVigenteAsync(ct))) return NaoEhPresidente();
            return await Executar(async () =>
            {
                await svc.AprovarAsync(id, ct);
                return Results.Ok(Mapear(await svc.BuscarAsync(id, ct, comSocio: true)));
            });
        });

        g.MapPost("/fichas/{id:guid}/reprovar", async (Guid id, MotivoRequest req, ClaimsPrincipal user, ContratoService svc, CancellationToken ct) =>
        {
            if (!ContratoService.EhPresidente(user, await svc.MandatoVigenteAsync(ct))) return NaoEhPresidente();
            return await Executar(async () =>
            {
                await svc.ReprovarAsync(id, req.Motivo, ct);
                return Results.Ok(Mapear(await svc.BuscarAsync(id, ct, comSocio: true)));
            });
        });
    }

    /// <summary>Páginas públicas do link enviado por e-mail ao candidato.</summary>
    private static void MapAceite(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/aceite").WithTags("Aceite do contrato").AllowAnonymous().ExcludeFromDescription();

        g.MapGet("/{token}", async (string token, ContratoService svc, RelogioClube relogio, CancellationToken ct) =>
        {
            var c = await svc.PorTokenAsync(token, ct);
            if (c is null) return Pagina("Link inválido", "<p>Este link de proposta não é válido. Confira o endereço recebido por e-mail.</p>", 404);
            var associacao = await svc.AssociacaoAsync(ct);

            if (!svc.LinkValido(c))
            {
                var msg = c.AceiteEm is { } em
                    ? $"<p>Você já concordou com esta proposta em <b>{relogio.ParaLocal(em):dd/MM/yyyy 'às' HH:mm}</b>. A ficha está com o presidente para aprovação.</p>"
                    : "<p>Esta proposta expirou ou foi substituída. Procure a secretaria da associação para receber um novo link.</p>";
                return Pagina(associacao.Nome, msg);
            }

            var corpo = $"""
                <p>Olá, <b>{H(c.Socio!.Nome)}</b>.</p>
                <p>Proposta de adesão nº <b>{H(c.Numero)}</b> ao quadro associativo da <b>{H(associacao.Nome)}</b>.</p>
                <ul>
                  <li>Categoria: <b>{H(c.Categoria.ToString())}</b></li>
                  <li>Mensalidade: <b>R$ {c.ValorMensalidade:N2}</b>, com vencimento todo dia {c.DiaVencimento}</li>
                  <li>Primeiro vencimento: <b>{c.PrimeiroVencimento:dd/MM/yyyy}</b> (após a aprovação)</li>
                  <li>Vigência: 12 meses a partir da aprovação, com renovação automática</li>
                  <li>Dependentes: {(c.Dependentes.Count == 0 ? "nenhum" : H(string.Join(", ", c.Dependentes.Select(d => d.Nome))))}</li>
                </ul>
                <p><a href="/aceite/{H(token)}/documento" target="_blank">📄 Ler o contrato completo</a></p>
                <form method="post" action="/aceite/{H(token)}">
                  <label style="display:block;margin:18px 0"><input type="checkbox" required>
                    Li o contrato e concordo com os termos da proposta.</label>
                  <button type="submit">Concordo</button>
                </form>
                <p class="nota">Ao concordar, registramos a data, a hora e o endereço IP do aceite. Depois a ficha segue
                   para a aprovação do presidente da associação.</p>
                """;
            return Pagina(associacao.Nome, corpo);
        });

        g.MapGet("/{token}/documento", async (string token, ContratoService svc, CancellationToken ct) =>
        {
            var c = await svc.PorTokenAsync(token, ct);
            return c is null || c.Documento is null || c.Status is not (StatusContrato.AguardandoAceite or StatusContrato.EmAnalise)
                ? Results.NotFound()
                : Documento(c);
        });

        g.MapPost("/{token}", async (string token, HttpContext http, ContratoService svc, CancellationToken ct) =>
        {
            var c = await svc.PorTokenAsync(token, ct);
            if (c is null) return Pagina("Link inválido", "<p>Este link de proposta não é válido.</p>", 404);
            var associacao = await svc.AssociacaoAsync(ct);
            if (!svc.LinkValido(c))
                return Pagina(associacao.Nome, "<p>Esta proposta não está mais disponível para aceite (já foi aceita, expirou ou foi substituída).</p>");

            await svc.RegistrarAceiteAsync(c, http.Connection.RemoteIpAddress, ct);
            return Pagina(associacao.Nome, $"""
                <p>✅ <b>Aceite registrado.</b> Obrigado, {H(c.Socio!.Nome.Split(' ')[0])}!</p>
                <p>Sua ficha foi encaminhada ao presidente da associação para aprovação. Após a aprovação você
                   receberá o acesso ao aplicativo e às dependências do clube.</p>
                """);
        });
    }

    // ---------- Apoio ----------

    public static ContratoDto Mapear(Contrato c) => new(
        c.Id, c.SocioId, c.Socio?.Nome ?? string.Empty, c.Socio?.NumeroTitulo ?? 0, c.Numero, c.Status.ToString(),
        c.Categoria.ToString(), c.ValorMensalidade, c.DiaVencimento, c.PrimeiroVencimento, c.VigenciaInicio, c.VigenciaFim,
        c.RenovacaoAutomatica, c.ContratoAnteriorId,
        c.Dependentes.Select(d => new DependenteContratoDto(d.Nome, d.Parentesco.ToString(), d.DataNascimento, d.Cpf)).ToList(),
        c.EnviadoEm, c.EnviadoPara, c.Status == StatusContrato.AguardandoAceite ? c.TokenExpiraEm : null,
        c.AceiteEm, c.AceiteIp, c.AprovadoEm, c.AprovadoPor, c.MotivoReprovacao, c.DesligadoEm, c.MotivoDesligamento,
        c.Documento is null ? null : c.DocumentoTipo, c.CriadoEm);

    private static IResult Documento(Contrato c) => c.Documento is null
        ? Results.Problem(title: "O documento ainda não foi gerado. Envie a proposta ao candidato.", statusCode: StatusCodes.Status404NotFound)
        : Results.File(c.Documento, GeradorContrato.MimeType(c.DocumentoTipo), $"Contrato-{c.Numero.Replace('/', '-')}.{c.DocumentoTipo}");

    /// <summary>Converte as regras violadas em respostas HTTP claras.</summary>
    private static async Task<IResult> Executar(Func<Task<IResult>> acao)
    {
        try
        {
            return await acao();
        }
        catch (KeyNotFoundException ex)
        {
            return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ContratoException ex) when (ex.Pendencias is { Count: > 0 } p)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["cadastro"] = [ex.Message, .. p.Select(x => $"Falta: {x}")],
            }, title: ex.Message);
        }
        catch (ContratoException ex)
        {
            return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static IResult NaoEhPresidente() => Results.Problem(
        title: "Somente o presidente do mandato vigente pode avaliar as fichas.", statusCode: StatusCodes.Status403Forbidden);

    private static string H(string? v) => WebUtility.HtmlEncode(v ?? string.Empty);

    private static IResult Pagina(string titulo, string corpo, int status = 200) => Results.Content($$"""
        <!doctype html>
        <html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>{{H(titulo)}}</title>
        <style>
          body{font-family:Arial,sans-serif;background:#FFF8EC;color:#222;margin:0;padding:24px}
          main{max-width:620px;margin:auto;background:#fff;border-radius:10px;padding:24px 28px;box-shadow:0 2px 8px #0002}
          h1{color:#B45309;font-size:22px;margin-top:0;text-align:center} a{color:#B45309}
          .logo{display:block;height:130px;margin:0 auto 12px}
          button{background:#B45309;color:#fff;border:0;border-radius:6px;padding:12px 28px;font-size:16px;font-weight:bold;cursor:pointer}
          .nota{font-size:13px;color:#666} li{margin:4px 0}
        </style></head>
        <body><main><img src="/marca/logo.png" alt="Clube da Onça" class="logo"><h1>{{H(titulo)}}</h1>{{corpo}}</main></body></html>
        """, "text/html; charset=utf-8", statusCode: status);
}
