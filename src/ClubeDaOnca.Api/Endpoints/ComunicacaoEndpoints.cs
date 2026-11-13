using System.Security.Claims;
using ClubeDaOnca.Api.Infrastructure;
using ClubeDaOnca.Api.Infrastructure.Comunicacao;
using ClubeDaOnca.Contracts.Comunicacao;
using ClubeDaOnca.Domain.Comunicacao;
using ClubeDaOnca.Domain.Eventos;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Endpoints;

/// <summary>Central de avisos do sócio, preferências de canais, comunicados da secretaria e post para redes sociais.</summary>
public static class ComunicacaoEndpoints
{
    public static IEndpointRouteBuilder MapComunicacao(this IEndpointRouteBuilder app)
    {
        MapAvisos(app.MapGroup("/avisos").WithTags("Avisos").RequireAuthorization());
        MapDivulgacoes(app.MapGroup("/divulgacoes").WithTags("Divulgação").RequireAuthorization("Secretaria"));
        MapPostRedes(app);
        return app;
    }

    private static void MapAvisos(RouteGroupBuilder g)
    {
        g.MapGet("/", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var socioId = await SocioIdAsync(db, user, ct);
            if (socioId is null) return Results.Ok(new List<AvisoDto>());
            var avisos = await db.Avisos.AsNoTracking().Where(a => a.SocioId == socioId)
                .OrderByDescending(a => a.CriadoEm).Take(100).ToListAsync(ct);
            return Results.Ok(avisos.Select(a => new AvisoDto(a.Id, a.Tipo.ToString(), a.Titulo, a.Mensagem, a.ReferenciaId,
                a.CriadoEm, a.LidoEm is not null)).ToList());
        });

        g.MapGet("/resumo", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var socioId = await SocioIdAsync(db, user, ct);
            var naoLidos = socioId is null ? 0 : await db.Avisos.CountAsync(a => a.SocioId == socioId && a.LidoEm == null, ct);
            return Results.Ok(new ResumoAvisosDto(naoLidos));
        });

        g.MapPost("/lidos", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var socioId = await SocioIdAsync(db, user, ct);
            if (socioId is not null)
                await db.Avisos.Where(a => a.SocioId == socioId && a.LidoEm == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.LidoEm, DateTimeOffset.UtcNow), ct);
            return Results.NoContent();
        });

        g.MapGet("/preferencias", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var s = await SocioAsync(db, user, ct);
            return s is null ? Results.NotFound() : Results.Ok(new PreferenciasComunicacaoDto(s.ReceberAvisosApp, s.ReceberEmail, s.ReceberWhatsApp));
        });

        g.MapPut("/preferencias", async (PreferenciasComunicacaoDto req, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var s = await SocioAsync(db, user, ct);
            if (s is null) return Results.NotFound();
            s.ReceberAvisosApp = req.AvisosApp;
            s.ReceberEmail = req.Email;
            s.ReceberWhatsApp = req.WhatsApp;
            await db.SaveChangesAsync(ct); // fica no histórico do cadastro (LGPD: registro da escolha)
            return Results.Ok(req);
        });
    }

    private static void MapDivulgacoes(RouteGroupBuilder g)
    {
        g.MapGet("/canais", (DivulgacaoService svc) => Results.Ok(svc.Canais()));

        g.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
        {
            var lista = await db.Divulgacoes.AsNoTracking().OrderByDescending(d => d.CriadoEm).Take(100).ToListAsync(ct);
            var ids = lista.Select(d => d.Id).ToList();
            var totais = await db.EnviosDivulgacao.AsNoTracking().Where(e => ids.Contains(e.DivulgacaoId))
                .GroupBy(e => new { e.DivulgacaoId, e.Canal, e.Status })
                .Select(gr => new { gr.Key.DivulgacaoId, gr.Key.Canal, gr.Key.Status, Qtd = gr.Count() })
                .ToListAsync(ct);
            return Results.Ok(lista.Select(d => new DivulgacaoDto(d.Id, d.Titulo, d.Mensagem, d.EventoId,
                d.Canais.Select(c => c.ToString()).ToList(), d.Status.ToString(), d.CriadoPor, d.CriadoEm, d.ConcluidaEm,
                d.Canais.Select(c => new TotaisCanalDto(c.ToString(),
                    totais.Where(t => t.DivulgacaoId == d.Id && t.Canal == c && t.Status == StatusEnvio.Enviado).Sum(t => t.Qtd),
                    totais.Where(t => t.DivulgacaoId == d.Id && t.Canal == c && t.Status == StatusEnvio.Falhou).Sum(t => t.Qtd),
                    totais.Where(t => t.DivulgacaoId == d.Id && t.Canal == c && t.Status == StatusEnvio.Ignorado).Sum(t => t.Qtd))).ToList()))
                .ToList());
        });

        g.MapPost("/", async (NovaDivulgacaoRequest req, DivulgacaoService svc, AppDbContext db, CancellationToken ct) =>
        {
            var erros = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(req.Titulo) || req.Titulo.Trim().Length > 150) erros["titulo"] = ["Informe o título (até 150 caracteres)."];
            if (string.IsNullOrWhiteSpace(req.Mensagem) || req.Mensagem.Trim().Length > 2000) erros["mensagem"] = ["Informe a mensagem (até 2000 caracteres)."];
            var canais = new List<CanalComunicacao>();
            foreach (var c in req.Canais ?? [])
                if (Enum.TryParse<CanalComunicacao>(c, true, out var canal) && Enum.IsDefined(canal) && !int.TryParse(c, out _)) canais.Add(canal);
                else erros["canais"] = [$"Canal inválido: {c}. Use App, Email ou WhatsApp."];
            if (canais.Count == 0 && !erros.ContainsKey("canais")) erros["canais"] = ["Escolha pelo menos um canal."];
            if (req.EventoId is { } eid && !await db.Eventos.AnyAsync(e => e.Id == eid, ct)) erros["eventoId"] = ["Evento não encontrado."];
            if (erros.Count > 0) return Results.ValidationProblem(erros);

            var d = await svc.CriarAsync(req.Titulo, req.Mensagem, canais, req.EventoId, ct);
            return Results.Created($"/divulgacoes/{d.Id}", new { d.Id, Status = d.Status.ToString() });
        });

        // Quem não recebeu e por quê (falhas e ignorados).
        g.MapGet("/{id:guid}/envios", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var envios = await db.EnviosDivulgacao.AsNoTracking()
                .Where(e => e.DivulgacaoId == id && e.Status != StatusEnvio.Enviado)
                .Join(db.Socios, e => e.SocioId, s => s.Id, (e, s) => new { e, s.Nome, s.NumeroTitulo })
                .OrderBy(x => x.Nome).Take(1000).ToListAsync(ct);
            return Results.Ok(envios.Select(x => new FalhaEnvioDto(x.Nome, x.NumeroTitulo, x.e.Canal.ToString(), x.e.Status.ToString(), x.e.Detalhe)).ToList());
        });
    }

    private static void MapPostRedes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/eventos/{id:guid}/post").WithTags("Divulgação").RequireAuthorization("Secretaria");

        g.MapGet("/", async (Guid id, HttpRequest http, AppDbContext db, CancellationToken ct) =>
        {
            var (e, associacao, erro) = await EventoParaPostAsync(db, id, ct);
            if (erro is not null) return erro;
            return Results.Ok(new PostRedesSociaisDto(GeradorPost.Texto(e!, associacao!), $"/eventos/{id}/post/imagem"));
        });

        g.MapGet("/imagem", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var (e, associacao, erro) = await EventoParaPostAsync(db, id, ct);
            if (erro is not null) return erro;
            return Results.File(GeradorPost.Imagem(e!, associacao!), "image/png", $"evento-{e!.Inicio:yyyy-MM-dd}.png");
        });
    }

    private static async Task<(Evento? Evento, string? Associacao, IResult? Erro)> EventoParaPostAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        var e = await db.Eventos.AsNoTracking().Include(x => x.Locais).ThenInclude(l => l.Espaco).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return (null, null, Results.Problem(title: "Evento não encontrado.", statusCode: StatusCodes.Status404NotFound));
        if (!e.AbertoAoPublico)
            return (null, null, Results.Problem(title: "Só eventos abertos ao público são divulgados nas redes sociais.", statusCode: StatusCodes.Status409Conflict));
        var associacao = await db.Associacoes.AsNoTracking().Where(a => a.Id == e.AssociacaoId).Select(a => a.Nome).FirstAsync(ct);
        return (e, associacao, null);
    }

    private static Task<Domain.Socios.Socio?> SocioAsync(AppDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var usuarioId = user.FindFirstValue("sub");
        return db.Socios.FirstOrDefaultAsync(s => s.UsuarioId == usuarioId, ct);
    }

    private static async Task<Guid?> SocioIdAsync(AppDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var usuarioId = user.FindFirstValue("sub");
        return await db.Socios.Where(s => s.UsuarioId == usuarioId).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
    }
}
