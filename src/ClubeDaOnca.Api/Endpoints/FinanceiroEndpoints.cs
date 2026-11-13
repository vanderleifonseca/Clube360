using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClubeDaOnca.Api.Infrastructure;
using ClubeDaOnca.Api.Infrastructure.Comunicacao;
using ClubeDaOnca.Domain.Comunicacao;
using ClubeDaOnca.Api.Infrastructure.Pagamentos;
using ClubeDaOnca.Contracts.Financeiro;
using ClubeDaOnca.Domain.Financeiro;
using ClubeDaOnca.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClubeDaOnca.Api.Endpoints;

public static class FinanceiroEndpoints
{
    public static IEndpointRouteBuilder MapFinanceiro(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/financeiro").WithTags("Financeiro").RequireAuthorization();
        MapSocio(g);
        MapGestao(g);
        MapWebhooks(app);
        return app;
    }

    // ---------- Sócio ----------

    private static void MapSocio(RouteGroupBuilder g)
    {
        g.MapGet("/minhas", async (ClaimsPrincipal user, AppDbContext db, FinanceiroService fin, RelogioClube relogio, CancellationToken ct) =>
        {
            var socio = await SocioDoUsuarioAsync(db, user, ct);
            if (socio is null)
                return Results.Ok(new ResumoFinanceiroDto(0, 0, false, []));

            var hoje = relogio.Hoje;
            var desde = hoje.AddMonths(-24);
            var cobrancas = await db.Cobrancas.AsNoTracking().Include(c => c.Socio).Include(c => c.Contrato).Include(c => c.Pagamentos)
                .Where(c => c.SocioId == socio.Id && (c.Status == StatusCobranca.Aberta || c.Vencimento >= desde))
                .OrderByDescending(c => c.Vencimento)
                .ToListAsync(ct);

            var abertas = cobrancas.Where(c => c.Status == StatusCobranca.Aberta).ToList();
            return Results.Ok(new ResumoFinanceiroDto(
                abertas.Sum(c => c.Valor),
                abertas.Where(c => c.Vencida(hoje)).Sum(c => c.Valor),
                await fin.InadimplenteAsync(socio.Id, ct),
                cobrancas.Select(c => FinanceiroService.Mapear(c, hoje)).ToList()));
        });

        // Gera (ou reaproveita) o Pix/boleto da cobrança no gateway.
        g.MapPost("/cobrancas/{id:guid}/pagar", async (Guid id, PagarRequest req, ClaimsPrincipal user, AppDbContext db,
            IGatewayPagamento gateway, RelogioClube relogio, CancellationToken ct) =>
        {
            if (!Enum.TryParse<FormaPagamento>(req.Forma, true, out var forma) || forma == FormaPagamento.Manual || int.TryParse(req.Forma, out _))
                return Erro("Forma de pagamento inválida. Use Pix ou Boleto.");

            var cobranca = await db.Cobrancas.Include(c => c.Socio).Include(c => c.Pagamentos)
                .FirstOrDefaultAsync(c => c.Id == id, ct);
            var socio = await SocioDoUsuarioAsync(db, user, ct);
            if (cobranca is null || (socio?.Id != cobranca.SocioId && !EhSecretaria(user)))
                return NaoEncontrado("Cobrança não encontrada.");
            if (cobranca.Status != StatusCobranca.Aberta)
                return Erro(cobranca.Status == StatusCobranca.Paga ? "Esta cobrança já foi paga." : "Esta cobrança foi cancelada.");

            var agora = relogio.UtcAgora;
            var existente = cobranca.Pagamentos
                .Where(p => p.Status == StatusPagamento.Pendente && p.Forma == forma && p.Gateway == gateway.Nome
                            && p.Valor == cobranca.Valor && (p.PixExpiraEm is null || p.PixExpiraEm > agora.AddMinutes(5)))
                .MaxBy(p => p.CriadoEm);
            if (existente is not null)
                return Results.Ok(FinanceiroService.Mapear(existente, gateway.Simulado));

            var hoje = relogio.Hoje;
            var vencimento = cobranca.Vencimento < hoje ? hoje : cobranca.Vencimento; // boleto não aceita vencimento no passado
            var s = cobranca.Socio!;
            CobrancaGateway criada;
            try
            {
                criada = await gateway.CriarAsync(new DadosPagador(s.Id, s.Nome, s.Cpf, s.Email, s.Telefone),
                    cobranca, forma, vencimento, ct);
            }
            catch (GatewayException ex)
            {
                return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }

            var pagamento = new Pagamento
            {
                CobrancaId = cobranca.Id,
                Gateway = gateway.Nome,
                IdExterno = criada.IdExterno,
                Forma = forma,
                Valor = cobranca.Valor,
                PixCopiaECola = criada.PixCopiaECola,
                PixImagemBase64 = criada.PixImagemBase64,
                PixExpiraEm = criada.PixExpiraEm,
                BoletoUrl = criada.BoletoUrl,
                LinhaDigitavel = criada.LinhaDigitavel,
            };
            db.Pagamentos.Add(pagamento);
            await db.SaveChangesAsync(ct);
            return Results.Ok(FinanceiroService.Mapear(pagamento, gateway.Simulado));
        });
    }

    // ---------- Secretaria ----------

    private static void MapGestao(RouteGroupBuilder g)
    {
        g.MapGet("/cobrancas", async (string? status, DateOnly? competencia, Guid? socioId, bool? vencidas,
            AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            var hoje = relogio.Hoje;
            var query = db.Cobrancas.AsNoTracking().Include(c => c.Socio).Include(c => c.Contrato).Include(c => c.Pagamentos).AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<StatusCobranca>(status, true, out var st) || int.TryParse(status, out _))
                    return Erro("Status inválido. Use Aberta, Paga ou Cancelada.");
                query = query.Where(c => c.Status == st);
            }
            if (competencia is { } comp)
            {
                var mes = RegrasFinanceiras.Competencia(comp);
                query = query.Where(c => c.Competencia == mes);
            }
            if (socioId is { } sid) query = query.Where(c => c.SocioId == sid);
            if (vencidas == true) query = query.Where(c => c.Status == StatusCobranca.Aberta && c.Vencimento < hoje);

            var lista = await query.OrderBy(c => c.Vencimento).ThenBy(c => c.Socio!.Nome).Take(500).ToListAsync(ct);
            return Results.Ok(lista.Select(c => FinanceiroService.Mapear(c, hoje)).ToList());
        }).RequireAuthorization("Secretaria");

        // Cobrança avulsa (multa, consumo, conserto etc.): o sócio paga pelo app como a mensalidade.
        g.MapPost("/cobrancas", async (LancarCobrancaRequest req, ClaimsPrincipal user, AppDbContext db,
            DivulgacaoService comunicacao, RelogioClube relogio, CancellationToken ct) =>
        {
            var socio = await db.Socios.FirstOrDefaultAsync(s => s.Id == req.SocioId, ct);
            if (socio is null) return NaoEncontrado("Sócio não encontrado.");

            var hoje = relogio.Hoje;
            var (cobranca, erro) = RegrasFinanceiras.CobrancaAvulsa(socio, req.Descricao, req.Valor, req.Vencimento, hoje);
            if (erro is not null) return Erro(erro);

            cobranca!.Socio = socio;
            cobranca.Observacao = Limitar($"Lançada por {user.FindFirstValue("name")}", 300);
            db.Cobrancas.Add(cobranca);

            // Aviso "Geral" (não "Cobranca"): assim o lembrete de 3 dias antes do vencimento continua saindo.
            await comunicacao.AvisarAsync(socio, TipoAviso.Geral, "Nova cobrança",
                $"{cobranca.Descricao}: {cobranca.Valor.ToString("C", new CultureInfo("pt-BR"))}, vence em {cobranca.Vencimento:dd/MM/yyyy}. " +
                "Pague pelo app, na aba Financeiro (Pix ou boleto).", cobranca.Id, porEmail: true, ct);

            await db.SaveChangesAsync(ct);
            return Results.Created($"/financeiro/cobrancas/{cobranca.Id}", FinanceiroService.Mapear(cobranca, hoje));
        }).RequireAuthorization("Secretaria");

        g.MapPost("/mensalidades/gerar", async (GerarMensalidadesRequest req, FinanceiroService fin, CancellationToken ct) =>
            Results.Ok(await fin.GerarMensalidadesAsync(req.Competencia, ct)))
            .RequireAuthorization("Secretaria");

        // Pagamento recebido fora do app (balcão, transferência).
        g.MapPost("/cobrancas/{id:guid}/baixa", async (Guid id, BaixaManualRequest req, ClaimsPrincipal user,
            AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            if (!Enum.TryParse<FormaPagamento>(req.Forma, true, out var forma) || int.TryParse(req.Forma, out _))
                return Erro("Forma inválida. Use Pix, Boleto ou Manual.");

            var cobranca = await db.Cobrancas.Include(c => c.Socio).FirstOrDefaultAsync(c => c.Id == id, ct);
            if (cobranca is null) return NaoEncontrado("Cobrança não encontrada.");
            if (cobranca.Status != StatusCobranca.Aberta) return Erro("Só cobranças em aberto podem receber baixa.");

            cobranca.Status = StatusCobranca.Paga;
            cobranca.PagoEm = relogio.UtcAgora;
            cobranca.FormaPagamento = forma;
            cobranca.Observacao = Limitar($"Baixa manual por {user.FindFirstValue("name")}" +
                (string.IsNullOrWhiteSpace(req.Observacao) ? "" : $": {req.Observacao.Trim()}"), 300);

            await db.Pagamentos.Where(p => p.CobrancaId == id && p.Status == StatusPagamento.Pendente)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusPagamento.Cancelado), ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(FinanceiroService.Mapear(cobranca, relogio.Hoje));
        }).RequireAuthorization("Secretaria");

        g.MapPost("/cobrancas/{id:guid}/cancelar", async (Guid id, AppDbContext db, FinanceiroService fin, RelogioClube relogio, CancellationToken ct) =>
        {
            var cobranca = await db.Cobrancas.Include(c => c.Socio).FirstOrDefaultAsync(c => c.Id == id, ct);
            if (cobranca is null) return NaoEncontrado("Cobrança não encontrada.");
            if (cobranca.Status != StatusCobranca.Aberta) return Erro("Só cobranças em aberto podem ser canceladas.");

            cobranca.Status = StatusCobranca.Cancelada;
            await fin.CancelarNoGatewayAsync([id], ct); // o boleto/Pix também deixa de valer no gateway
            await db.SaveChangesAsync(ct);
            return Results.Ok(FinanceiroService.Mapear(cobranca, relogio.Hoje));
        }).RequireAuthorization("Secretaria");

        g.MapGet("/inadimplentes", async (AppDbContext db, FinanceiroService fin, RelogioClube relogio, CancellationToken ct) =>
        {
            var cfg = await fin.ConfiguracaoAsync(ct);
            var limite = RegrasFinanceiras.LimiteInadimplencia(relogio.Hoje, cfg.DiasTolerancia);
            var totais = await db.Cobrancas.AsNoTracking()
                .Where(c => c.Status == StatusCobranca.Aberta && c.Vencimento < limite)
                .GroupBy(c => c.SocioId)
                .Select(gr => new { SocioId = gr.Key, Quantidade = gr.Count(), Valor = gr.Sum(c => c.Valor), MaisAntigo = gr.Min(c => c.Vencimento) })
                .ToListAsync(ct);

            var ids = totais.Select(t => t.SocioId).ToList();
            var socios = await db.Socios.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

            return Results.Ok(totais
                .Select(t => new InadimplenteDto(t.SocioId, socios[t.SocioId].NumeroTitulo, socios[t.SocioId].Nome,
                    socios[t.SocioId].Telefone, t.Quantidade, t.Valor, t.MaisAntigo))
                .OrderByDescending(i => i.ValorVencido)
                .ToList());
        }).RequireAuthorization("Secretaria");

        g.MapGet("/configuracao", async (FinanceiroService fin, CancellationToken ct) =>
            Results.Ok(Mapear(await fin.ConfiguracaoAsync(ct))))
            .RequireAuthorization("Secretaria");

        g.MapPut("/configuracao", async (ConfiguracaoFinanceiraDto req, AppDbContext db, FinanceiroService fin, CancellationToken ct) =>
        {
            var erros = new Dictionary<string, string[]>();
            if (req.ValorIndividual < 0 || req.ValorFamiliar < 0 || req.ValorRemido < 0) erros["valores"] = ["Os valores não podem ser negativos."];
            if (req.DiaVencimento is < 1 or > 31) erros["diaVencimento"] = ["O dia de vencimento deve ficar entre 1 e 31."];
            if (req.DiasTolerancia is < 0 or > 90) erros["diasTolerancia"] = ["A tolerância deve ficar entre 0 e 90 dias."];
            if (req.PrazoTaxaReservaDias is < 0 or > 60) erros["prazoTaxaReservaDias"] = ["O prazo da taxa deve ficar entre 0 e 60 dias."];
            if (erros.Count > 0) return Results.ValidationProblem(erros);

            // Novos valores valem para as próximas mensalidades; as já geradas não mudam.
            var cfg = await fin.ConfiguracaoAsync(ct);
            cfg.ValorIndividual = req.ValorIndividual;
            cfg.ValorFamiliar = req.ValorFamiliar;
            cfg.ValorRemido = req.ValorRemido;
            cfg.DiaVencimento = req.DiaVencimento;
            cfg.DiasTolerancia = req.DiasTolerancia;
            cfg.BloquearInadimplentesNaPortaria = req.BloquearInadimplentesNaPortaria;
            cfg.BloquearInadimplentesNasReservas = req.BloquearInadimplentesNasReservas;
            cfg.PrazoTaxaReservaDias = req.PrazoTaxaReservaDias;
            await db.SaveChangesAsync(ct);
            return Results.Ok(Mapear(cfg));
        }).RequireAuthorization("Admin");
    }

    // ---------- Webhooks ----------

    private static void MapWebhooks(IEndpointRouteBuilder app)
    {
        // Avisos do Asaas. Respondemos 200 mesmo para eventos ignorados: um erro faria o Asaas pausar a fila de webhooks.
        // /webhooks/asaas = conta geral do servidor; /webhooks/asaas/{associacaoId} = conta própria da unidade.
        app.MapPost("/webhooks/asaas/{associacaoId:guid?}", async (Guid? associacaoId, HttpRequest http, ContaAsaas conta,
            FinanceiroService fin, ILoggerFactory logs, CancellationToken ct) =>
        {
            var log = logs.CreateLogger("Webhooks.Asaas");
            var esperado = await conta.WebhookTokenAsync(associacaoId, ct);
            var recebido = http.Headers["asaas-access-token"].ToString();
            if (string.IsNullOrEmpty(esperado)
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(esperado), Encoding.UTF8.GetBytes(recebido)))
            {
                log.LogWarning("Webhook do Asaas com token inválido.");
                return Results.Unauthorized();
            }

            using var doc = await JsonDocument.ParseAsync(http.Body, cancellationToken: ct);
            var evento = doc.RootElement.TryGetProperty("event", out var e) ? e.GetString() : null;
            if (evento is not ("PAYMENT_RECEIVED" or "PAYMENT_CONFIRMED")
                || !doc.RootElement.TryGetProperty("payment", out var pagamento)
                || pagamento.GetProperty("id").GetString() is not { } idExterno)
                return Results.Ok();

            DateTimeOffset? pagoEm = pagamento.TryGetProperty("clientPaymentDate", out var d) && d.ValueKind == JsonValueKind.String
                && DateOnly.TryParse(d.GetString(), out var data)
                    ? new DateTimeOffset(data.ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(-3))
                    : null;

            if (!await fin.ConfirmarPagamentoAsync("Asaas", idExterno, pagoEm, ct))
                log.LogInformation("Webhook do Asaas para pagamento desconhecido {Id}; ignorado.", idExterno);
            return Results.Ok();
        }).WithTags("Webhooks").AllowAnonymous();

        // Só existe com o gateway simulado: faz o papel do webhook para testar o fluxo inteiro.
        app.MapPost("/financeiro/pagamentos/{id:guid}/simular-confirmacao", async (Guid id, ClaimsPrincipal user,
            AppDbContext db, IGatewayPagamento gateway, FinanceiroService fin, CancellationToken ct) =>
        {
            if (!gateway.Simulado) return Results.NotFound();

            var pagamento = await db.Pagamentos.Include(p => p.Cobranca).FirstOrDefaultAsync(p => p.Id == id, ct);
            var socio = await SocioDoUsuarioAsync(db, user, ct);
            if (pagamento is null || (pagamento.Cobranca!.SocioId != socio?.Id && !EhSecretaria(user)))
                return NaoEncontrado("Pagamento não encontrado.");

            await fin.ConfirmarPagamentoAsync(pagamento.Gateway, pagamento.IdExterno, null, ct);
            return Results.NoContent();
        }).WithTags("Financeiro").RequireAuthorization();
    }

    private static ConfiguracaoFinanceiraDto Mapear(ConfiguracaoFinanceira c) => new(
        c.ValorIndividual, c.ValorFamiliar, c.ValorRemido, c.DiaVencimento, c.DiasTolerancia,
        c.BloquearInadimplentesNaPortaria, c.BloquearInadimplentesNasReservas, c.PrazoTaxaReservaDias);

    private static Task<Domain.Socios.Socio?> SocioDoUsuarioAsync(AppDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var usuarioId = user.FindFirstValue("sub");
        return db.Socios.FirstOrDefaultAsync(s => s.UsuarioId == usuarioId, ct);
    }

    private static bool EhSecretaria(ClaimsPrincipal user) => user.IsInRole(Perfis.Secretaria) || user.IsInRole(Perfis.Admin);
    private static string Limitar(string s, int max) => s.Length > max ? s[..max] : s;
    private static IResult Erro(string m) => Results.Problem(title: m, statusCode: StatusCodes.Status400BadRequest);
    private static IResult NaoEncontrado(string m) => Results.Problem(title: m, statusCode: StatusCodes.Status404NotFound);
}
