using ClubeDaOnca.Contracts.Eventos;
using ClubeDaOnca.Domain.Eventos;
using ClubeDaOnca.Domain.Financeiro;
using ClubeDaOnca.Domain.Reservas;
using ClubeDaOnca.Domain.Socios;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Infrastructure.Eventos;

/// <summary>Erro de regra do evento (vira 409, ou 400 com a lista de campos).</summary>
public sealed class EventoException(string mensagem, Dictionary<string, string[]>? erros = null, IReadOnlyList<ConflitoAgendaDto>? conflitos = null)
    : Exception(mensagem)
{
    public Dictionary<string, string[]>? Erros { get; } = erros;
    public IReadOnlyList<ConflitoAgendaDto>? Conflitos { get; } = conflitos;
}

/// <summary>Eventos do clube: checagem da agenda dos locais, cadastro, publicação, cancelamento e reservas de ambientes.</summary>
public sealed class EventoService(AppDbContext db, FinanceiroService financeiro, Comunicacao.DivulgacaoService comunicacao,
    RelogioClube relogio, ILogger<EventoService> log)
{
    // ---------- Agenda ----------

    /// <summary>Reservas comuns e outros eventos que ocupam os locais no período.</summary>
    public async Task<List<ConflitoAgendaDto>> ConflitosAsync(IReadOnlyCollection<Guid> locais, DateTime inicio, DateTime termino,
        Guid? ignorarEventoId, CancellationToken ct = default)
    {
        var conflitos = new List<ConflitoAgendaDto>();
        if (locais.Count == 0 || termino <= inicio) return conflitos;

        var dataInicial = DateOnly.FromDateTime(inicio).AddDays(-1);
        var dataFinal = DateOnly.FromDateTime(termino);
        var reservas = await db.Reservas.AsNoTracking().Include(r => r.Espaco).Include(r => r.Socio)
            .Where(r => locais.Contains(r.EspacoId) && r.Data >= dataInicial && r.Data <= dataFinal
                && (r.Status == StatusReserva.Pendente || r.Status == StatusReserva.Confirmada))
            .ToListAsync(ct);
        foreach (var r in reservas)
        {
            var (ri, rf) = RegrasEvento.Periodo(r);
            if (RegrasEvento.Sobrepoe(inicio, termino, ri, rf))
                conflitos.Add(new ConflitoAgendaDto(r.Espaco!.Nome, ri, rf,
                    $"Reserva de {r.Socio?.Nome} (título {r.Socio?.NumeroTitulo}) — {(r.Status == StatusReserva.Pendente ? "pendente" : "confirmada")}"));
        }

        var eventos = await db.Eventos.AsNoTracking().Include(e => e.Locais).ThenInclude(l => l.Espaco)
            .Where(e => e.Id != ignorarEventoId && e.Status != StatusEvento.Cancelado && e.Inicio < termino && inicio < e.Termino
                && e.Locais.Any(l => locais.Contains(l.EspacoId)))
            .ToListAsync(ct);
        foreach (var e in eventos)
            foreach (var l in e.Locais.Where(l => locais.Contains(l.EspacoId)))
                conflitos.Add(new ConflitoAgendaDto(l.Espaco!.Nome, e.Inicio, e.Termino, $"Evento \"{e.Titulo}\""));

        return conflitos.OrderBy(c => c.Inicio).ToList();
    }

    /// <summary>Evento que ocupa o espaço no horário de uma reserva comum (bloqueia a reserva e marca a agenda).</summary>
    public Task<Evento?> EventoNoHorarioAsync(Guid espacoId, DateTime inicio, DateTime fim, CancellationToken ct = default) =>
        db.Eventos.AsNoTracking()
            .Where(e => e.Status != StatusEvento.Cancelado && e.Inicio < fim && inicio < e.Termino && e.Locais.Any(l => l.EspacoId == espacoId))
            .FirstOrDefaultAsync(ct);

    /// <summary>Períodos de eventos no espaço num dia (para a agenda de horários).</summary>
    public async Task<List<(DateTime Inicio, DateTime Fim)>> EventosNoDiaAsync(Guid espacoId, DateOnly dia, CancellationToken ct = default)
    {
        var de = dia.ToDateTime(TimeOnly.MinValue);
        var ate = de.AddDays(1);
        return (await db.Eventos.AsNoTracking()
                .Where(e => e.Status != StatusEvento.Cancelado && e.Inicio < ate && de < e.Termino && e.Locais.Any(l => l.EspacoId == espacoId))
                .Select(e => new { e.Inicio, e.Termino }).ToListAsync(ct))
            .Select(e => (e.Inicio, e.Termino)).ToList();
    }

    // ---------- Cadastro ----------

    public async Task<Evento> SalvarAsync(Guid? id, SalvarEventoRequest req, CancellationToken ct = default)
    {
        var evento = id is { } eid
            ? await db.Eventos.Include(e => e.Locais).Include(e => e.Ambientes).FirstOrDefaultAsync(e => e.Id == eid, ct)
              ?? throw new KeyNotFoundException("Evento não encontrado.")
            : new Evento();
        if (evento.Status == StatusEvento.Cancelado) throw new EventoException("Evento cancelado não pode ser alterado.");

        var idsLocais = (req.Locais ?? []).Distinct().ToList();
        var locais = await db.Espacos.Where(e => idsLocais.Contains(e.Id)).ToListAsync(ct);
        if (locais.Count != idsLocais.Count) throw new EventoException("Local não encontrado.");

        evento.Titulo = (req.Titulo ?? string.Empty).Trim();
        evento.Descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim();
        evento.Inicio = DateTime.SpecifyKind(req.Inicio, DateTimeKind.Unspecified);
        evento.Termino = DateTime.SpecifyKind(req.Termino, DateTimeKind.Unspecified);
        evento.Capacidade = req.Capacidade;
        evento.AbertoAoPublico = req.AbertoAoPublico;
        evento.DivulgarAssociados = req.DivulgarAssociados;
        evento.DivulgarRedesSociais = req.DivulgarRedesSociais;
        evento.PrazoCancelamentoHoras = req.PrazoCancelamentoHoras;
        AplicarAmbientes(evento, req.Ambientes ?? []);

        var erros = RegrasEvento.ValidarCadastro(evento, locais, relogio.Agora, novo: id is null);
        if (erros.Count > 0) throw new EventoException("Corrija os dados do evento.", erros);

        // Primeiro a agenda: o local precisa estar livre no período.
        var conflitos = await ConflitosAsync(idsLocais, evento.Inicio, evento.Termino, id, ct);
        if (conflitos.Count > 0)
            throw new EventoException("Os locais escolhidos não estão livres nesse período. Resolva as reservas abaixo ou escolha outro horário.",
                conflitos: conflitos);

        // Locais: substitui a lista.
        evento.Locais.RemoveAll(l => !idsLocais.Contains(l.EspacoId));
        foreach (var lid in idsLocais.Where(lid => evento.Locais.All(l => l.EspacoId != lid)))
            evento.Locais.Add(new EventoLocal { EventoId = evento.Id, EspacoId = lid });

        if (id is null) db.Eventos.Add(evento);
        await db.SaveChangesAsync(ct);
        return evento;
    }

    private static void AplicarAmbientes(Evento evento, IReadOnlyList<SalvarAmbienteRequest> ambientes)
    {
        var manter = ambientes.Where(a => a.Id is not null).Select(a => a.Id!.Value).ToHashSet();
        var removidos = evento.Ambientes.Where(a => !manter.Contains(a.Id)).ToList();
        if (removidos.FirstOrDefault(a => a.Reservadas > 0) is { } comReserva)
            throw new EventoException($"O ambiente \"{comReserva.Nome}\" já tem reservas e não pode ser removido.");
        foreach (var r in removidos) evento.Ambientes.Remove(r);

        foreach (var req in ambientes)
        {
            var a = req.Id is { } aid ? evento.Ambientes.FirstOrDefault(x => x.Id == aid) : null;
            if (a is null)
            {
                a = new AmbienteEvento { EventoId = evento.Id };
                evento.Ambientes.Add(a);
            }
            a.Nome = (req.Nome ?? string.Empty).Trim();
            a.PessoasPorReserva = req.PessoasPorReserva;
            a.Vagas = req.Vagas;
            a.LimitePorSocio = req.LimitePorSocio;
            a.Valor = Math.Round(req.Valor, 2);
        }
    }

    public async Task<Evento> PublicarAsync(Guid id, CancellationToken ct = default)
    {
        var e = await BuscarAsync(id, ct);
        if (e.Status != StatusEvento.Rascunho) throw new EventoException("Só eventos em rascunho podem ser publicados.");
        if (e.Ambientes.Count == 0) throw new EventoException("Cadastre pelo menos um ambiente reservável antes de publicar.");
        if (e.Termino <= relogio.Agora) throw new EventoException("O evento já terminou.");
        e.Status = StatusEvento.Publicado;
        await db.SaveChangesAsync(ct);
        if (e.DivulgarAssociados) await comunicacao.DivulgarEventoAsync(e, ct);
        log.LogInformation("Evento {Titulo} publicado (divulgar aos associados: {Associados}; redes sociais: {Redes}).",
            e.Titulo, e.DivulgarAssociados, e.DivulgarRedesSociais);
        return e;
    }

    /// <summary>Cancela o evento: libera a agenda, cancela as reservas e as taxas ainda não pagas.</summary>
    public async Task<Evento> CancelarAsync(Guid id, string motivo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new EventoException("Informe o motivo do cancelamento.");
        var e = await BuscarAsync(id, ct);
        if (e.Status == StatusEvento.Cancelado) throw new EventoException("O evento já está cancelado.");

        e.Status = StatusEvento.Cancelado;
        e.MotivoCancelamento = motivo.Trim();
        var reservas = await db.ReservasEvento.Where(r => r.EventoId == id && r.Status == StatusReservaEvento.Confirmada).ToListAsync(ct);
        foreach (var r in reservas)
        {
            r.Status = StatusReservaEvento.Cancelada;
            r.CanceladaEm = relogio.UtcAgora;
        }
        foreach (var a in e.Ambientes) a.Reservadas = 0;
        await CancelarTaxasAsync(reservas.Select(r => r.Id).ToList(), $"Evento cancelado: {e.MotivoCancelamento}", ct);

        // Quem tinha reserva fica sabendo (app + e-mail), independentemente das preferências de divulgação.
        var socios = await db.Socios.Where(s => reservas.Select(r => r.SocioId).Contains(s.Id)).ToListAsync(ct);
        foreach (var s in socios)
            await comunicacao.AvisarAsync(s, Domain.Comunicacao.TipoAviso.Evento, $"Evento cancelado: {e.Titulo}",
                $"O evento de {e.Inicio:dd/MM/yyyy} foi cancelado ({e.MotivoCancelamento}). Sua reserva foi cancelada" +
                " e taxas ainda não pagas também.", e.Id, porEmail: true, ct);
        await db.SaveChangesAsync(ct);
        return e;
    }

    // ---------- Reservas dos sócios ----------

    public async Task<ReservaEvento> ReservarAsync(Guid eventoId, Socio socio, ReservarEventoRequest req, CancellationToken ct = default)
    {
        if (socio.Situacao != SituacaoTitulo.Ativo) throw new EventoException("Seu título não está ativo. Procure a secretaria.");
        var cfg = await financeiro.ConfiguracaoAsync(ct);
        if (cfg.BloquearInadimplentesNasReservas && await financeiro.InadimplenteAsync(socio.Id, ct))
            throw new EventoException("Há mensalidade em atraso no seu título. Regularize na aba Financeiro para reservar.");

        // Uma reserva por vez em cada evento: trava a linha do evento até o fim da transação (última vaga não sai duas vezes).
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Eventos\" WHERE \"Id\" = {eventoId} FOR UPDATE", ct);

        var e = await BuscarAsync(eventoId, ct);
        var ambiente = e.Ambientes.FirstOrDefault(a => a.Id == req.AmbienteId) ?? throw new KeyNotFoundException("Ambiente não encontrado.");
        var minhas = await db.ReservasEvento.CountAsync(r => r.AmbienteId == ambiente.Id && r.SocioId == socio.Id
            && r.Status == StatusReservaEvento.Confirmada, ct);
        var pessoas = await PessoasReservadasAsync(eventoId, ct);

        if (RegrasEvento.ValidarReserva(e, ambiente, req.Pessoas, minhas, pessoas, relogio.Agora) is { } erro)
            throw new EventoException(erro);

        var reserva = new ReservaEvento
        {
            EventoId = e.Id, AmbienteId = ambiente.Id, SocioId = socio.Id, Pessoas = req.Pessoas, Valor = ambiente.Valor,
            Observacao = string.IsNullOrWhiteSpace(req.Observacao) ? null : req.Observacao.Trim()[..Math.Min(req.Observacao.Trim().Length, 300)],
        };
        db.ReservasEvento.Add(reserva);
        ambiente.Reservadas++;

        if (ambiente.Valor > 0)
            db.Cobrancas.Add(new Cobranca
            {
                SocioId = socio.Id,
                Tipo = TipoCobranca.TaxaEvento,
                ReservaEventoId = reserva.Id,
                Descricao = $"{e.Titulo}: {ambiente.Nome} ({e.Inicio:dd/MM/yyyy})",
                Valor = ambiente.Valor,
                Vencimento = RegrasFinanceiras.VencimentoTaxaReserva(relogio.Hoje, DateOnly.FromDateTime(e.Inicio), cfg.PrazoTaxaReservaDias),
            });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        reserva.Evento = e;
        reserva.Ambiente = ambiente;
        return reserva;
    }

    /// <summary>Cancela a reserva (pelo sócio, no prazo, ou pela secretaria a qualquer momento) e a taxa ainda não paga.</summary>
    public async Task<ReservaEvento> CancelarReservaAsync(Guid reservaId, Guid? socioId, CancellationToken ct = default)
    {
        var r = await db.ReservasEvento.Include(x => x.Evento).Include(x => x.Ambiente).Include(x => x.Socio)
            .FirstOrDefaultAsync(x => x.Id == reservaId && (socioId == null || x.SocioId == socioId), ct)
            ?? throw new KeyNotFoundException("Reserva não encontrada.");
        if (socioId is not null && RegrasEvento.ValidarCancelamentoPeloSocio(r.Evento!, r, relogio.Agora) is { } erro)
            throw new EventoException(erro);
        if (r.Status != StatusReservaEvento.Confirmada) throw new EventoException("Esta reserva já foi cancelada.");

        r.Status = StatusReservaEvento.Cancelada;
        r.CanceladaEm = relogio.UtcAgora;
        r.Ambiente!.Reservadas = Math.Max(0, r.Ambiente.Reservadas - 1);
        await CancelarTaxasAsync([r.Id], "Reserva do evento cancelada", ct);
        await db.SaveChangesAsync(ct);
        return r;
    }

    // ---------- Apoio ----------

    public async Task<Evento> BuscarAsync(Guid id, CancellationToken ct) =>
        await db.Eventos.Include(e => e.Locais).ThenInclude(l => l.Espaco).Include(e => e.Ambientes)
            .FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new KeyNotFoundException("Evento não encontrado.");

    public Task<int> PessoasReservadasAsync(Guid eventoId, CancellationToken ct) =>
        db.ReservasEvento.Where(r => r.EventoId == eventoId && r.Status == StatusReservaEvento.Confirmada).SumAsync(r => r.Pessoas, ct);

    private async Task CancelarTaxasAsync(IReadOnlyCollection<Guid> reservaIds, string observacao, CancellationToken ct)
    {
        if (reservaIds.Count == 0) return;
        var taxas = await db.Cobrancas.Where(c => c.ReservaEventoId != null && reservaIds.Contains(c.ReservaEventoId.Value)
            && c.Status == StatusCobranca.Aberta).ToListAsync(ct);
        foreach (var c in taxas)
        {
            c.Status = StatusCobranca.Cancelada;
            c.Observacao = observacao.Length > 300 ? observacao[..300] : observacao;
        }
        await financeiro.CancelarNoGatewayAsync(taxas.Select(c => c.Id).ToList(), ct);
    }
}
