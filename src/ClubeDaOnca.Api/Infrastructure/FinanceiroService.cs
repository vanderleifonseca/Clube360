using ClubeDaOnca.Api.Infrastructure.Pagamentos;
using ClubeDaOnca.Contracts.Financeiro;
using ClubeDaOnca.Domain.Contratos;
using ClubeDaOnca.Domain.Financeiro;
using ClubeDaOnca.Domain.Reservas;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Infrastructure;

/// <summary>Regras financeiras que dependem do banco: mensalidades, taxas de reserva, inadimplência e baixas.</summary>
public sealed class FinanceiroService(AppDbContext db, RelogioClube relogio, IGatewayPagamento gateway, ILogger<FinanceiroService> log)
{
    public async Task<ConfiguracaoFinanceira> ConfiguracaoAsync(CancellationToken ct = default)
    {
        var cfg = await db.ConfiguracoesFinanceiras.FirstOrDefaultAsync(ct);
        if (cfg is not null) return cfg;

        cfg = new ConfiguracaoFinanceira();
        db.ConfiguracoesFinanceiras.Add(cfg);
        await db.SaveChangesAsync(ct);
        return cfg;
    }

    /// <summary>Gera as mensalidades da competência para todos os títulos ativos. Pode ser repetido sem duplicar.</summary>
    public async Task<GerarMensalidadesResponse> GerarMensalidadesAsync(DateOnly competencia, CancellationToken ct = default)
    {
        competencia = RegrasFinanceiras.Competencia(competencia);
        var cfg = await ConfiguracaoAsync(ct);

        // Quem tem contrato aprovado cobrindo o mês paga pelas parcelas do contrato, não pelo job mensal.
        var comContrato = (await db.Contratos.Where(c => c.Status == StatusContrato.Aprovado).ToListAsync(ct))
            .Where(c => RegrasFinanceiras.ContratoCobre(c, competencia)).Select(c => c.SocioId).ToHashSet();
        var socios = (await db.Socios.ToListAsync(ct)).Where(s => !comContrato.Contains(s.Id)).ToList();
        var jaTem = (await db.Cobrancas
                .Where(c => c.Tipo == TipoCobranca.Mensalidade && c.Competencia == competencia)
                .Select(c => c.SocioId).ToListAsync(ct))
            .ToHashSet();

        int geradas = 0, ignoradas = 0;
        foreach (var socio in socios)
        {
            if (jaTem.Contains(socio.Id)) continue;
            var cobranca = RegrasFinanceiras.GerarMensalidade(socio, competencia, cfg, jaExiste: false);
            if (cobranca is null) { ignoradas++; continue; }
            db.Cobrancas.Add(cobranca);
            geradas++;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Duas gerações ao mesmo tempo (job + secretaria): o índice único impede duplicar.
            log.LogWarning(ex, "Geração de mensalidades concorrente para {Competencia}; tente novamente.", competencia);
            throw;
        }

        ignoradas += comContrato.Count;
        log.LogInformation("Mensalidades {Competencia}: {Geradas} geradas, {Existentes} já existiam, {Ignoradas} ignoradas.",
            competencia, geradas, jaTem.Count, ignoradas);
        return new GerarMensalidadesResponse(competencia, geradas, jaTem.Count, ignoradas);
    }

    /// <summary>Adiciona ao contexto as 12 parcelas do contrato (o chamador salva). Meses já cobrados são pulados.</summary>
    public async Task<int> GerarParcelasContratoAsync(Contrato contrato, CancellationToken ct = default)
    {
        var existentes = (await db.Cobrancas
                .Where(c => c.SocioId == contrato.SocioId && c.Tipo == TipoCobranca.Mensalidade && c.Competencia != null)
                .Select(c => c.Competencia!.Value).ToListAsync(ct))
            .ToHashSet();
        var parcelas = RegrasFinanceiras.ParcelasContrato(contrato, existentes);
        db.Cobrancas.AddRange(parcelas);
        if (parcelas.Count < RegrasFinanceiras.ParcelasPorContrato && contrato.ValorMensalidade > 0)
            log.LogWarning("Contrato {Numero}: {Qtd} parcela(s) puladas porque o mês já tinha mensalidade.",
                contrato.Numero, RegrasFinanceiras.ParcelasPorContrato - parcelas.Count);
        return parcelas.Count;
    }

    /// <summary>
    /// Registra no gateway o boleto de cada parcela em aberto do contrato que ainda não tem boleto.
    /// Pode ser repetido: só emite o que falta. Falhas ficam para a próxima tentativa.
    /// </summary>
    public async Task<(int Emitidos, int Falhas)> EmitirBoletosContratoAsync(Guid contratoId, CancellationToken ct = default)
    {
        var parcelas = await db.Cobrancas.Include(c => c.Socio).Include(c => c.Pagamentos)
            .Where(c => c.ContratoId == contratoId && c.Status == StatusCobranca.Aberta)
            .OrderBy(c => c.Parcela).ToListAsync(ct);

        int emitidos = 0, falhas = 0;
        foreach (var c in parcelas)
        {
            if (c.Pagamentos.Any(p => p.Status == StatusPagamento.Pendente && p.Forma == FormaPagamento.Boleto && p.Gateway == gateway.Nome))
                continue;
            try
            {
                var vencimento = c.Vencimento < relogio.Hoje ? relogio.Hoje : c.Vencimento;
                var criada = await gateway.CriarAsync(DadosPagador.De(c.Socio!), c, FormaPagamento.Boleto, vencimento, ct);
                db.Pagamentos.Add(new Pagamento
                {
                    AssociacaoId = c.AssociacaoId,
                    CobrancaId = c.Id,
                    Gateway = gateway.Nome,
                    IdExterno = criada.IdExterno,
                    Forma = FormaPagamento.Boleto,
                    Valor = c.Valor,
                    BoletoUrl = criada.BoletoUrl,
                    LinhaDigitavel = criada.LinhaDigitavel,
                    PixCopiaECola = criada.PixCopiaECola,
                });
                c.EmitidaEm = relogio.UtcAgora;
                await db.SaveChangesAsync(ct); // salva a cada boleto: se cair no meio, não duplica no gateway
                emitidos++;
            }
            catch (GatewayException ex)
            {
                falhas++;
                log.LogWarning(ex, "Falha ao emitir o boleto da parcela {Parcela} ({Cobranca}).", c.Parcela, c.Id);
            }
        }
        log.LogInformation("Contrato {Contrato}: {Emitidos} boleto(s) emitido(s), {Falhas} falha(s).", contratoId, emitidos, falhas);
        return (emitidos, falhas);
    }

    /// <summary>
    /// Cancela no gateway os boletos/Pix pendentes das cobranças (não vale mais pagar). Melhor esforço:
    /// se o gateway falhar, o pagamento fica marcado aqui e o erro vai para o log.
    /// </summary>
    public async Task CancelarNoGatewayAsync(IReadOnlyCollection<Guid> cobrancaIds, CancellationToken ct = default)
    {
        if (cobrancaIds.Count == 0) return;
        var pendentes = await db.Pagamentos
            .Where(p => cobrancaIds.Contains(p.CobrancaId) && p.Status == StatusPagamento.Pendente).ToListAsync(ct);
        foreach (var p in pendentes)
        {
            if (p.Gateway == gateway.Nome)
            {
                try
                {
                    await gateway.CancelarAsync(p.IdExterno, ct);
                }
                catch (GatewayException ex)
                {
                    log.LogWarning(ex, "Não foi possível cancelar {IdExterno} no {Gateway}; cancele no painel.", p.IdExterno, p.Gateway);
                }
            }
            p.Status = StatusPagamento.Cancelado;
        }
    }

    /// <summary>Cria a cobrança da taxa quando a reserva fica confirmada (uma única vez por reserva).</summary>
    public async Task CriarTaxaReservaAsync(Reserva reserva, CancellationToken ct = default)
    {
        if (reserva.Taxa <= 0 || reserva.Status != StatusReserva.Confirmada) return;
        if (await db.Cobrancas.AnyAsync(c => c.ReservaId == reserva.Id, ct)) return;

        var cfg = await ConfiguracaoAsync(ct);
        db.Cobrancas.Add(new Cobranca
        {
            SocioId = reserva.SocioId,
            Tipo = TipoCobranca.TaxaReserva,
            ReservaId = reserva.Id,
            Descricao = $"Taxa de reserva: {reserva.Espaco?.Nome} em {reserva.Data:dd/MM/yyyy}",
            Valor = reserva.Taxa,
            Vencimento = RegrasFinanceiras.VencimentoTaxaReserva(relogio.Hoje, reserva.Data, cfg.PrazoTaxaReservaDias),
        });
    }

    /// <summary>Reserva cancelada ou recusada: a taxa ainda não paga deixa de ser devida.</summary>
    public async Task CancelarTaxaReservaAsync(Guid reservaId, CancellationToken ct = default)
    {
        var abertas = await db.Cobrancas
            .Where(c => c.ReservaId == reservaId && c.Status == StatusCobranca.Aberta)
            .ToListAsync(ct);
        foreach (var c in abertas)
        {
            c.Status = StatusCobranca.Cancelada;
            c.Observacao = "Reserva cancelada";
        }
        await CancelarNoGatewayAsync(abertas.Select(c => c.Id).ToList(), ct);
    }

    public async Task<bool> InadimplenteAsync(Guid socioId, CancellationToken ct = default)
    {
        var cfg = await ConfiguracaoAsync(ct);
        var limite = RegrasFinanceiras.LimiteInadimplencia(relogio.Hoje, cfg.DiasTolerancia);
        return await db.Cobrancas.AnyAsync(c => c.SocioId == socioId && c.Status == StatusCobranca.Aberta && c.Vencimento < limite, ct);
    }

    /// <summary>
    /// Confirma um pagamento recebido do gateway (webhook). Idempotente: o gateway pode reenviar o mesmo aviso.
    /// Devolve falso se o pagamento não for deste sistema.
    /// </summary>
    public async Task<bool> ConfirmarPagamentoAsync(string gateway, string idExterno, DateTimeOffset? pagoEm, CancellationToken ct = default)
    {
        // O webhook não tem usuário logado: localiza o pagamento em qualquer associação e passa a operar nela.
        var pagamento = await db.Pagamentos.IgnoreQueryFilters().Include(p => p.Cobranca)
            .FirstOrDefaultAsync(p => p.Gateway == gateway && p.IdExterno == idExterno, ct);
        if (pagamento is null) return false;
        db.Tenant.Definir(pagamento.AssociacaoId);

        var quando = (pagoEm ?? relogio.UtcAgora).ToUniversalTime(); // o Postgres só grava timestamptz em UTC
        if (pagamento.Status != StatusPagamento.Confirmado)
        {
            pagamento.Status = StatusPagamento.Confirmado;
            pagamento.ConfirmadoEm = quando;
        }

        var cobranca = pagamento.Cobranca!;
        if (cobranca.Status != StatusCobranca.Paga)
        {
            if (cobranca.Status == StatusCobranca.Cancelada)
                log.LogWarning("Pagamento {IdExterno} recebido para a cobrança cancelada {Cobranca}: verificar estorno.", idExterno, cobranca.Id);

            cobranca.Status = StatusCobranca.Paga;
            cobranca.PagoEm = quando;
            cobranca.FormaPagamento = pagamento.Forma;

            // Outras tentativas pendentes (ex.: um boleto gerado antes do Pix) deixam de valer.
            await db.Pagamentos
                .Where(p => p.CobrancaId == cobranca.Id && p.Id != pagamento.Id && p.Status == StatusPagamento.Pendente)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusPagamento.Cancelado), ct);
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Pagamento {Gateway}/{IdExterno} confirmado; cobrança {Cobranca} paga.", gateway, idExterno, cobranca.Id);
        return true;
    }

    public static CobrancaDto Mapear(Cobranca c, DateOnly hoje)
    {
        // Boleto vigente (emitido junto com as parcelas do contrato ou pelo sócio no app).
        var boleto = c.Pagamentos.Where(p => p.Forma == FormaPagamento.Boleto && p.Status != StatusPagamento.Cancelado)
            .MaxBy(p => p.CriadoEm);
        return new(
            c.Id, c.SocioId, c.Socio?.Nome ?? string.Empty, c.Socio?.NumeroTitulo ?? 0, c.Tipo.ToString(), c.Descricao,
            c.Competencia, c.Valor, c.Vencimento, c.Status.ToString(), c.Vencida(hoje), c.PagoEm, c.FormaPagamento?.ToString(),
            c.Observacao, c.Parcela, c.Contrato?.Numero, c.EmitidaEm ?? boleto?.CriadoEm, boleto?.BoletoUrl, boleto?.LinhaDigitavel,
            c.CriadoEm);
    }

    public static PagamentoDto Mapear(Pagamento p, bool simulado) => new(
        p.Id, p.CobrancaId, p.Gateway, p.Forma.ToString(), p.Status.ToString(), p.Valor, p.PixCopiaECola, p.PixImagemBase64,
        p.PixExpiraEm, p.BoletoUrl, p.LinhaDigitavel, simulado);
}
