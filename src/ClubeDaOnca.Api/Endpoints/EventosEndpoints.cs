using System.Security.Claims;
using ClubeDaOnca.Api.Infrastructure;
using ClubeDaOnca.Api.Infrastructure.Eventos;
using ClubeDaOnca.Contracts.Eventos;
using ClubeDaOnca.Domain.Eventos;
using ClubeDaOnca.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Endpoints;

/// <summary>
/// Eventos do clube. A secretaria cadastra (checando a agenda dos locais), publica e cancela;
/// os sócios veem os eventos publicados e reservam ambientes (mesas, quiosques...) dentro deles.
/// </summary>
public static class EventosEndpoints
{
    public static IEndpointRouteBuilder MapEventos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/eventos").WithTags("Eventos").RequireAuthorization();
        MapConsulta(g);
        MapGestao(g);
        MapReservasDoSocio(g);
        return app;
    }

    private static void MapConsulta(RouteGroupBuilder g)
    {
        // Sócio: eventos publicados que ainda não terminaram. Gestão (?todos=true): todos, inclusive rascunhos e cancelados.
        g.MapGet("/", async (bool? todos, ClaimsPrincipal user, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            var gestao = todos == true && EhSecretaria(user);
            var query = db.Eventos.AsNoTracking().Include(e => e.Locais).ThenInclude(l => l.Espaco).Include(e => e.Ambientes).AsQueryable();
            if (!gestao)
            {
                var agora = relogio.Agora;
                query = query.Where(e => e.Status == StatusEvento.Publicado && e.Termino > agora);
            }
            var eventos = await query.OrderBy(e => e.Inicio).Take(300).ToListAsync(ct);
            return Results.Ok(await MapearAsync(db, eventos, gestao ? null : await SocioIdAsync(db, user, ct), ct));
        });

        g.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var e = await db.Eventos.AsNoTracking().Include(x => x.Locais).ThenInclude(l => l.Espaco).Include(x => x.Ambientes)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (e is null || (e.Status != StatusEvento.Publicado && !EhSecretaria(user))) return NaoEncontrado("Evento não encontrado.");
            return Results.Ok((await MapearAsync(db, [e], await SocioIdAsync(db, user, ct), ct))[0]);
        });
    }

    private static void MapGestao(RouteGroupBuilder g)
    {
        // Verificar a agenda antes de cadastrar: locais livres no período?
        g.MapPost("/disponibilidade", async (VerificarAgendaRequest req, EventoService svc, CancellationToken ct) =>
        {
            if (req.Termino <= req.Inicio) return Erro("O término deve ser depois do início.");
            var conflitos = await svc.ConflitosAsync((req.Locais ?? []).Distinct().ToList(), req.Inicio, req.Termino, req.IgnorarEventoId, ct);
            return Results.Ok(new DisponibilidadeDto(conflitos.Count == 0, conflitos));
        }).RequireAuthorization("Secretaria");

        g.MapPost("/", (SalvarEventoRequest req, EventoService svc, AppDbContext db, CancellationToken ct) =>
            Executar(async () =>
            {
                var e = await svc.SalvarAsync(null, req, ct);
                return Results.Created($"/eventos/{e.Id}", await UmAsync(db, e.Id, ct));
            })).RequireAuthorization("Secretaria");

        g.MapPut("/{id:guid}", (Guid id, SalvarEventoRequest req, EventoService svc, AppDbContext db, CancellationToken ct) =>
            Executar(async () =>
            {
                await svc.SalvarAsync(id, req, ct);
                return Results.Ok(await UmAsync(db, id, ct));
            })).RequireAuthorization("Secretaria");

        g.MapPost("/{id:guid}/publicar", (Guid id, EventoService svc, AppDbContext db, CancellationToken ct) =>
            Executar(async () =>
            {
                await svc.PublicarAsync(id, ct);
                return Results.Ok(await UmAsync(db, id, ct));
            })).RequireAuthorization("Secretaria");

        g.MapPost("/{id:guid}/cancelar", (Guid id, CancelarEventoRequest req, EventoService svc, AppDbContext db, CancellationToken ct) =>
            Executar(async () =>
            {
                await svc.CancelarAsync(id, req.Motivo, ct);
                return Results.Ok(await UmAsync(db, id, ct));
            })).RequireAuthorization("Secretaria");

        g.MapGet("/{id:guid}/reservas", async (Guid id, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            var reservas = await db.ReservasEvento.AsNoTracking().Include(r => r.Evento).Include(r => r.Ambiente).Include(r => r.Socio)
                .Where(r => r.EventoId == id).OrderBy(r => r.Status).ThenBy(r => r.CriadoEm).ToListAsync(ct);
            return Results.Ok(reservas.Select(r => Mapear(r, relogio.Agora, gestao: true)).ToList());
        }).RequireAuthorization("Secretaria");

        g.MapPost("/reservas/{reservaId:guid}/cancelar", (Guid reservaId, EventoService svc, RelogioClube relogio, CancellationToken ct) =>
            Executar(async () => Results.Ok(Mapear(await svc.CancelarReservaAsync(reservaId, socioId: null, ct), relogio.Agora, gestao: true))))
            .RequireAuthorization("Secretaria");
    }

    private static void MapReservasDoSocio(RouteGroupBuilder g)
    {
        g.MapPost("/{id:guid}/reservas", async (Guid id, ReservarEventoRequest req, ClaimsPrincipal user, AppDbContext db,
            EventoService svc, RelogioClube relogio, CancellationToken ct) =>
        {
            var socio = await db.Socios.FirstOrDefaultAsync(s => s.UsuarioId == user.FindFirstValue("sub"), ct);
            if (socio is null)
                return Results.Problem(title: "Apenas sócios titulares podem reservar em eventos.", statusCode: StatusCodes.Status403Forbidden);
            return await Executar(async () =>
            {
                var r = await svc.ReservarAsync(id, socio, req, ct);
                r.Socio = socio;
                return Results.Created($"/eventos/minhas-reservas/{r.Id}", Mapear(r, relogio.Agora, gestao: false));
            });
        });

        g.MapGet("/minhas-reservas", async (ClaimsPrincipal user, AppDbContext db, RelogioClube relogio, CancellationToken ct) =>
        {
            var socioId = await SocioIdAsync(db, user, ct);
            if (socioId is null) return Results.Ok(new List<ReservaEventoDto>());
            var limite = relogio.Agora.AddDays(-60);
            var reservas = await db.ReservasEvento.AsNoTracking().Include(r => r.Evento).Include(r => r.Ambiente).Include(r => r.Socio)
                .Where(r => r.SocioId == socioId && r.Evento!.Termino > limite)
                .OrderBy(r => r.Evento!.Inicio).ToListAsync(ct);
            return Results.Ok(reservas.Select(r => Mapear(r, relogio.Agora, gestao: false)).ToList());
        });

        g.MapPost("/minhas-reservas/{reservaId:guid}/cancelar", async (Guid reservaId, ClaimsPrincipal user, AppDbContext db,
            EventoService svc, RelogioClube relogio, CancellationToken ct) =>
        {
            var socioId = await SocioIdAsync(db, user, ct);
            if (socioId is null) return NaoEncontrado("Reserva não encontrada.");
            return await Executar(async () => Results.Ok(Mapear(await svc.CancelarReservaAsync(reservaId, socioId, ct), relogio.Agora, gestao: false)));
        });
    }

    // ---------- Apoio ----------

    private static async Task<EventoDto> UmAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        var e = await db.Eventos.AsNoTracking().Include(x => x.Locais).ThenInclude(l => l.Espaco).Include(x => x.Ambientes)
            .FirstAsync(x => x.Id == id, ct);
        return (await MapearAsync(db, [e], null, ct))[0];
    }

    private static async Task<List<EventoDto>> MapearAsync(AppDbContext db, List<Evento> eventos, Guid? socioId, CancellationToken ct)
    {
        var ids = eventos.Select(e => e.Id).ToList();
        var confirmadas = await db.ReservasEvento.AsNoTracking()
            .Where(r => ids.Contains(r.EventoId) && r.Status == StatusReservaEvento.Confirmada)
            .Select(r => new { r.EventoId, r.Pessoas, r.SocioId }).ToListAsync(ct);

        return eventos.Select(e => new EventoDto(
            e.Id, e.Titulo, e.Descricao, e.Inicio, e.Termino, e.Status.ToString(), e.Capacidade,
            confirmadas.Where(r => r.EventoId == e.Id).Sum(r => r.Pessoas),
            e.AbertoAoPublico, e.DivulgarAssociados, e.DivulgarRedesSociais, e.PrazoCancelamentoHoras, e.MotivoCancelamento,
            e.Locais.Where(l => l.Espaco is not null).OrderBy(l => l.Espaco!.Nome)
                .Select(l => new LocalEventoDto(l.EspacoId, l.Espaco!.Nome, l.Espaco.Tipo.ToString(), l.Espaco.Capacidade)).ToList(),
            e.Ambientes.OrderBy(a => a.Nome)
                .Select(a => new AmbienteEventoDto(a.Id, a.Nome, a.PessoasPorReserva, a.Vagas, Math.Max(0, a.Vagas - a.Reservadas),
                    a.LimitePorSocio, a.Valor)).ToList(),
            socioId is null ? 0 : confirmadas.Count(r => r.EventoId == e.Id && r.SocioId == socioId))).ToList();
    }

    private static ReservaEventoDto Mapear(ReservaEvento r, DateTime agoraLocal, bool gestao) => new(
        r.Id, r.EventoId, r.Evento?.Titulo ?? string.Empty, r.Evento?.Inicio ?? default, r.AmbienteId, r.Ambiente?.Nome ?? string.Empty,
        r.Pessoas, r.Valor, r.Status.ToString(), r.Observacao,
        gestao ? r.Status == StatusReservaEvento.Confirmada
               : r.Evento is not null && RegrasEvento.ValidarCancelamentoPeloSocio(r.Evento, r, agoraLocal) is null,
        r.Socio?.Nome ?? string.Empty, r.Socio?.NumeroTitulo ?? 0, r.CriadoEm);

    private static async Task<Guid?> SocioIdAsync(AppDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var usuarioId = user.FindFirstValue("sub");
        return await db.Socios.Where(s => s.UsuarioId == usuarioId).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
    }

    private static async Task<IResult> Executar(Func<Task<IResult>> acao)
    {
        try
        {
            return await acao();
        }
        catch (KeyNotFoundException ex)
        {
            return NaoEncontrado(ex.Message);
        }
        catch (EventoException ex) when (ex.Conflitos is { Count: > 0 } conflitos)
        {
            return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?>
                {
                    ["conflitos"] = conflitos,
                    ["errors"] = new Dictionary<string, string[]>
                    {
                        ["agenda"] = conflitos.Select(c => $"{c.Local}: {c.Inicio:dd/MM HH:mm}–{c.Fim:dd/MM HH:mm} · {c.Descricao}").ToArray(),
                    },
                });
        }
        catch (EventoException ex) when (ex.Erros is { Count: > 0 } erros)
        {
            return Results.ValidationProblem(erros, title: ex.Message);
        }
        catch (EventoException ex)
        {
            return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static bool EhSecretaria(ClaimsPrincipal user) => user.IsInRole(Perfis.Secretaria) || user.IsInRole(Perfis.Admin);
    private static IResult Erro(string m) => Results.Problem(title: m, statusCode: StatusCodes.Status400BadRequest);
    private static IResult NaoEncontrado(string m) => Results.Problem(title: m, statusCode: StatusCodes.Status404NotFound);
}
