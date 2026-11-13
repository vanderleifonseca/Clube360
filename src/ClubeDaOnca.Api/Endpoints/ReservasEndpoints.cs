using System.Security.Claims;
using ClubeDaOnca.Api.Infrastructure;
using ClubeDaOnca.Api.Infrastructure.Eventos;
using ClubeDaOnca.Contracts.Reservas;
using ClubeDaOnca.Domain.Eventos;
using ClubeDaOnca.Domain.Reservas;
using ClubeDaOnca.Domain.Seguranca;
using ClubeDaOnca.Domain.Socios;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClubeDaOnca.Api.Endpoints;

/// <summary>Espaços do clube, agenda de horários e reservas.</summary>
public static class ReservasEndpoints
{
    public static IEndpointRouteBuilder MapReservas(this IEndpointRouteBuilder app)
    {
        MapEspacos(app.MapGroup("/espacos").WithTags("Reservas").RequireAuthorization());
        MapReservasDoSocio(app.MapGroup("/reservas").WithTags("Reservas").RequireAuthorization());
        return app;
    }

    private static void MapEspacos(RouteGroupBuilder g)
    {
        // Sócios veem só os ativos; a gestão pode pedir todos (?todos=true).
        g.MapGet("/", async (bool? todos, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.Espacos.AsNoTracking();
            if (todos != true || !EhSecretaria(user))
                query = query.Where(e => e.Ativo);
            var lista = await query.OrderBy(e => e.Nome).ToListAsync(ct);
            return Results.Ok(lista.Select(Mapear).ToList());
        });

        g.MapPost("/", async (SalvarEspacoRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var erros = Validar(req);
            if (erros.Count > 0) return Results.ValidationProblem(erros);
            if (await db.Espacos.AnyAsync(e => e.Nome == req.Nome.Trim(), ct))
                return Conflito("Já existe um espaço com este nome.");

            var espaco = new Espaco();
            Aplicar(espaco, req);
            db.Espacos.Add(espaco);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/espacos/{espaco.Id}", Mapear(espaco));
        }).RequireAuthorization("Secretaria");

        g.MapPut("/{id:guid}", async (Guid id, SalvarEspacoRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var erros = Validar(req);
            if (erros.Count > 0) return Results.ValidationProblem(erros);

            var espaco = await db.Espacos.FindAsync([id], ct);
            if (espaco is null) return NaoEncontrado("Espaço não encontrado.");
            if (await db.Espacos.AnyAsync(e => e.Nome == req.Nome.Trim() && e.Id != id, ct))
                return Conflito("Já existe um espaço com este nome.");

            // Reservas já feitas continuam valendo; as novas regras valem para as próximas.
            Aplicar(espaco, req);
            await db.SaveChangesAsync(ct);
            return Results.Ok(Mapear(espaco));
        }).RequireAuthorization("Secretaria");

        g.MapGet("/{id:guid}/agenda", async (Guid id, DateOnly? data, AppDbContext db, RelogioClube relogio, EventoService eventos, CancellationToken ct) =>
        {
            var espaco = await db.Espacos.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            if (espaco is null) return NaoEncontrado("Espaço não encontrado.");

            var dia = data ?? relogio.Hoje;
            var ocupados = await db.Reservas.AsNoTracking()
                .Where(r => r.EspacoId == id && r.Data == dia
                    && (r.Status == StatusReserva.Pendente || r.Status == StatusReserva.Confirmada))
                .Select(r => r.Inicio)
                .ToListAsync(ct);

            // Horários tomados por eventos aparecem como ocupados.
            var periodosEventos = await eventos.EventosNoDiaAsync(id, dia, ct);
            bool OcupadoPorEvento(Horario h)
            {
                var (ini, fim) = RegrasEvento.Periodo(new Reserva { Data = dia, Inicio = h.Inicio, Fim = h.Fim });
                return periodosEventos.Any(p => RegrasEvento.Sobrepoe(ini, fim, p.Inicio, p.Fim));
            }

            var agora = relogio.Agora;
            var horarios = RegrasReserva.HorariosDoDia(espaco, dia)
                .Select(h => new HorarioDto(h.Inicio, h.Fim,
                    RegrasReserva.Situacao(espaco, dia, h, agora, ocupados.Contains(h.Inicio) || OcupadoPorEvento(h)).ToString()))
                .ToList();
            return Results.Ok(new AgendaDto(id, dia, horarios));
        });
    }

    private static void MapReservasDoSocio(RouteGroupBuilder g)
    {
        g.MapPost("/", async (CriarReservaRequest req, ClaimsPrincipal user, AppDbContext db, RelogioClube relogio,
            FinanceiroService fin, EventoService eventos, CancellationToken ct) =>
        {
            var socio = await SocioDoUsuarioAsync(db, user, ct);
            if (socio is null) return Proibido("Apenas sócios titulares podem fazer reservas.");
            if (socio.Situacao != SituacaoTitulo.Ativo)
                return Erro("Seu título não está ativo. Procure a secretaria.");
            if ((await fin.ConfiguracaoAsync(ct)).BloquearInadimplentesNasReservas && await fin.InadimplenteAsync(socio.Id, ct))
                return Erro("Há mensalidade em atraso no seu título. Regularize na aba Financeiro para reservar.");

            var espaco = await db.Espacos.FirstOrDefaultAsync(e => e.Id == req.EspacoId, ct);
            if (espaco is null) return NaoEncontrado("Espaço não encontrado.");

            var hoje = relogio.Hoje;
            var ativas = await db.Reservas.CountAsync(r => r.SocioId == socio.Id && r.EspacoId == espaco.Id && r.Data >= hoje
                && (r.Status == StatusReserva.Pendente || r.Status == StatusReserva.Confirmada), ct);
            var ocupado = await db.Reservas.AnyAsync(r => r.EspacoId == espaco.Id && r.Data == req.Data && r.Inicio == req.Inicio
                && (r.Status == StatusReserva.Pendente || r.Status == StatusReserva.Confirmada), ct);

            var erro = RegrasReserva.ValidarNovaReserva(espaco, req.Data, req.Inicio, relogio.Agora, ativas, ocupado);
            if (erro is not null) return Erro(erro);

            var horario = RegrasReserva.HorariosDoDia(espaco, req.Data).First(h => h.Inicio == req.Inicio);
            var (inicioReserva, fimReserva) = RegrasEvento.Periodo(new Reserva { Data = req.Data, Inicio = horario.Inicio, Fim = horario.Fim });
            if (await eventos.EventoNoHorarioAsync(espaco.Id, inicioReserva, fimReserva, ct) is { } evento)
                return Erro($"Este horário está reservado para o evento \"{evento.Titulo}\".");
            var reserva = new Reserva
            {
                EspacoId = espaco.Id,
                Espaco = espaco,
                SocioId = socio.Id,
                Socio = socio,
                Data = req.Data,
                Inicio = horario.Inicio,
                Fim = horario.Fim,
                Taxa = espaco.Taxa,
                Observacao = string.IsNullOrWhiteSpace(req.Observacao) ? null : req.Observacao.Trim()[..Math.Min(req.Observacao.Trim().Length, 500)],
                Status = espaco.ExigeAprovacao ? StatusReserva.Pendente : StatusReserva.Confirmada,
            };
            db.Reservas.Add(reserva);
            await fin.CriarTaxaReservaAsync(reserva, ct); // só se já nasce confirmada e tem taxa

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return Conflito("Este horário acabou de ser reservado por outro sócio. Escolha outro.");
            }

            return Results.Created($"/reservas/{reserva.Id}", Mapear(reserva, relogio.Agora, gestao: false));
        });

        g.MapGet("/minhas", async (ClaimsPrincipal user, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            var socio = await SocioDoUsuarioAsync(db, user, ct);
            if (socio is null) return Results.Ok(new List<ReservaDto>());

            var desde = relogio.Hoje.AddDays(-60);
            var reservas = await db.Reservas.AsNoTracking().Include(r => r.Espaco).Include(r => r.Socio)
                .Where(r => r.SocioId == socio.Id && r.Data >= desde)
                .OrderByDescending(r => r.Data).ThenByDescending(r => r.Inicio)
                .Take(100)
                .ToListAsync(ct);

            var agora = relogio.Agora;
            return Results.Ok(reservas.Select(r => Mapear(r, agora, gestao: false)).ToList());
        });

        // Lista da gestão: por padrão, as reservas ativas de hoje em diante.
        g.MapGet("/", async (string? status, DateOnly? data, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            var query = db.Reservas.AsNoTracking().Include(r => r.Espaco).Include(r => r.Socio).AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<StatusReserva>(status, true, out var st) || int.TryParse(status, out _))
                    return Erro("Status inválido. Use Pendente, Confirmada, Cancelada ou Recusada.");
                query = query.Where(r => r.Status == st);
            }
            else
            {
                query = query.Where(r => r.Status == StatusReserva.Pendente || r.Status == StatusReserva.Confirmada);
            }

            query = data is { } d ? query.Where(r => r.Data == d) : query.Where(r => r.Data >= relogio.Hoje);

            var reservas = await query.OrderBy(r => r.Data).ThenBy(r => r.Inicio).Take(200).ToListAsync(ct);
            var agora = relogio.Agora;
            return Results.Ok(reservas.Select(r => Mapear(r, agora, gestao: true)).ToList());
        }).RequireAuthorization("Secretaria");

        g.MapPost("/{id:guid}/cancelar", async (Guid id, ClaimsPrincipal user, AppDbContext db, RelogioClube relogio,
            FinanceiroService fin, CancellationToken ct) =>
        {
            var reserva = await BuscarAsync(db, id, ct);
            if (reserva is null) return NaoEncontrado("Reserva não encontrada.");

            var gestao = EhSecretaria(user);
            if (!gestao)
            {
                var socio = await SocioDoUsuarioAsync(db, user, ct);
                if (socio is null || reserva.SocioId != socio.Id) return NaoEncontrado("Reserva não encontrada.");

                var erro = RegrasReserva.ValidarCancelamentoPeloSocio(reserva.Espaco!, reserva, relogio.Agora);
                if (erro is not null) return Erro(erro);
            }
            else if (!reserva.Ativa)
            {
                return Erro("Esta reserva não está ativa.");
            }

            reserva.Status = StatusReserva.Cancelada;
            reserva.AtualizadoEm = relogio.UtcAgora;
            await fin.CancelarTaxaReservaAsync(reserva.Id, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(Mapear(reserva, relogio.Agora, gestao));
        });

        g.MapPost("/{id:guid}/aprovar", async (Guid id, AppDbContext db, RelogioClube relogio, FinanceiroService fin, CancellationToken ct) =>
        {
            var reserva = await BuscarAsync(db, id, ct);
            if (reserva is null) return NaoEncontrado("Reserva não encontrada.");
            if (reserva.Status != StatusReserva.Pendente) return Erro("Só reservas pendentes podem ser aprovadas.");
            if (reserva.InicioLocal <= relogio.Agora) return Erro("O horário desta reserva já passou.");

            reserva.Status = StatusReserva.Confirmada;
            reserva.AtualizadoEm = relogio.UtcAgora;
            await fin.CriarTaxaReservaAsync(reserva, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(Mapear(reserva, relogio.Agora, gestao: true));
        }).RequireAuthorization("Secretaria");

        g.MapPost("/{id:guid}/recusar", async (Guid id, RecusarReservaRequest req, AppDbContext db, RelogioClube relogio,
            FinanceiroService fin, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Motivo))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["motivo"] = ["Informe o motivo da recusa."] });

            var reserva = await BuscarAsync(db, id, ct);
            if (reserva is null) return NaoEncontrado("Reserva não encontrada.");
            if (!reserva.Ativa) return Erro("Esta reserva não está ativa.");

            reserva.Status = StatusReserva.Recusada;
            reserva.MotivoRecusa = req.Motivo.Trim()[..Math.Min(req.Motivo.Trim().Length, 300)];
            reserva.AtualizadoEm = relogio.UtcAgora;
            await fin.CancelarTaxaReservaAsync(reserva.Id, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(Mapear(reserva, relogio.Agora, gestao: true));
        }).RequireAuthorization("Secretaria");
    }

    private static Task<Reserva?> BuscarAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        db.Reservas.Include(r => r.Espaco).Include(r => r.Socio).FirstOrDefaultAsync(r => r.Id == id, ct);

    private static Task<Socio?> SocioDoUsuarioAsync(AppDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var usuarioId = user.FindFirstValue("sub");
        return db.Socios.FirstOrDefaultAsync(s => s.UsuarioId == usuarioId, ct);
    }

    private static bool EhSecretaria(ClaimsPrincipal user) =>
        user.IsInRole(Perfis.Secretaria) || user.IsInRole(Perfis.Admin);

    private static Dictionary<string, string[]> Validar(SalvarEspacoRequest r)
    {
        var erros = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(r.Nome) || r.Nome.Trim().Length < 3 || r.Nome.Trim().Length > 100)
            erros["nome"] = ["Informe o nome do espaço (3 a 100 caracteres)."];
        if (r.DuracaoMinutos is < 15 or > 1440)
            erros["duracaoMinutos"] = ["A duração de cada horário deve ficar entre 15 minutos e 24 horas."];
        if (r.Fechamento != TimeOnly.MinValue && r.Fechamento <= r.Abertura)
            erros["fechamento"] = ["O horário de fechamento deve ser depois da abertura (use 00:00 para meia-noite)."];
        if (r.DiasSemana is null || r.DiasSemana.Count == 0 || r.DiasSemana.Any(d => d is < 0 or > 6))
            erros["diasSemana"] = ["Escolha pelo menos um dia de funcionamento."];
        if (r.AntecedenciaMinimaHoras < 0 || r.AntecedenciaMaximaDias < 1)
            erros["antecedencia"] = ["Antecedência inválida."];
        if (r.LimiteReservasAtivas < 1)
            erros["limiteReservasAtivas"] = ["O limite de reservas por sócio deve ser pelo menos 1."];
        if (r.Taxa < 0)
            erros["taxa"] = ["A taxa não pode ser negativa."];
        if (r.PrazoCancelamentoHoras < 0)
            erros["prazoCancelamentoHoras"] = ["Prazo de cancelamento inválido."];
        if (r.Capacidade is < 0 or > 100_000)
            erros["capacidade"] = ["Capacidade inválida."];
        if (!string.IsNullOrWhiteSpace(r.Tipo) && (!Enum.TryParse<TipoLocal>(r.Tipo, true, out _) || int.TryParse(r.Tipo, out _)))
            erros["tipo"] = ["Tipo inválido. Use Quadra, Quiosque, Churrasqueira, Salao, Piscina, Campo, Auditorio ou Outro."];
        return erros;
    }

    private static void Aplicar(Espaco e, SalvarEspacoRequest r)
    {
        e.Nome = r.Nome.Trim();
        e.Descricao = string.IsNullOrWhiteSpace(r.Descricao) ? null : r.Descricao.Trim();
        e.Ativo = r.Ativo;
        e.Abertura = r.Abertura;
        e.Fechamento = r.Fechamento;
        e.DuracaoMinutos = r.DuracaoMinutos;
        e.DiasSemana = r.DiasSemana.Distinct().Order().ToArray();
        e.AntecedenciaMinimaHoras = r.AntecedenciaMinimaHoras;
        e.AntecedenciaMaximaDias = r.AntecedenciaMaximaDias;
        e.LimiteReservasAtivas = r.LimiteReservasAtivas;
        e.Taxa = r.Taxa;
        e.ExigeAprovacao = r.ExigeAprovacao;
        e.PrazoCancelamentoHoras = r.PrazoCancelamentoHoras;
        e.Capacidade = r.Capacidade;
        if (!string.IsNullOrWhiteSpace(r.Tipo) && Enum.TryParse<TipoLocal>(r.Tipo, true, out var tipo)) e.Tipo = tipo;
    }

    private static EspacoDto Mapear(Espaco e) => new(
        e.Id, e.Nome, e.Descricao, e.Ativo, e.Abertura, e.Fechamento, e.DuracaoMinutos, e.DiasSemana,
        e.AntecedenciaMinimaHoras, e.AntecedenciaMaximaDias, e.LimiteReservasAtivas, e.Taxa, e.ExigeAprovacao,
        e.PrazoCancelamentoHoras, e.Tipo.ToString(), e.Capacidade);

    private static ReservaDto Mapear(Reserva r, DateTime agoraLocal, bool gestao) => new(
        r.Id, r.EspacoId, r.Espaco?.Nome ?? string.Empty, r.Data, r.Inicio, r.Fim, r.Status.ToString(), r.Taxa,
        r.Observacao, r.MotivoRecusa,
        gestao ? r.Ativa : r.Espaco is not null && RegrasReserva.ValidarCancelamentoPeloSocio(r.Espaco, r, agoraLocal) is null,
        r.Socio?.NumeroTitulo ?? 0, r.Socio?.Nome ?? string.Empty, r.CriadoEm);

    private static IResult Erro(string mensagem) => Results.Problem(title: mensagem, statusCode: StatusCodes.Status400BadRequest);
    private static IResult Conflito(string mensagem) => Results.Problem(title: mensagem, statusCode: StatusCodes.Status409Conflict);
    private static IResult Proibido(string mensagem) => Results.Problem(title: mensagem, statusCode: StatusCodes.Status403Forbidden);
    private static IResult NaoEncontrado(string mensagem) => Results.Problem(title: mensagem, statusCode: StatusCodes.Status404NotFound);
}
