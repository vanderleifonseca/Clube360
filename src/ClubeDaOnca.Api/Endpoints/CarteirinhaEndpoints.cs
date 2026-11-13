using System.Security.Claims;
using ClubeDaOnca.Api.Infrastructure;
using ClubeDaOnca.Contracts.Carteirinha;
using ClubeDaOnca.Domain.Carteirinha;
using ClubeDaOnca.Domain.Socios;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Endpoints;

public static class CarteirinhaEndpoints
{
    public static IEndpointRouteBuilder MapCarteirinha(this IEndpointRouteBuilder app)
    {
        // Carteirinhas do titular logado e dos seus dependentes ativos.
        app.MapGet("/carteirinha", async (ClaimsPrincipal user, AppDbContext db, ChavesCarteirinha chaves, CancellationToken ct) =>
        {
            var usuarioId = user.FindFirstValue("sub");
            var socio = await db.Socios.AsNoTracking().Include(s => s.Dependentes)
                .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId, ct);
            if (socio is null)
                return Results.Problem(title: "Nenhum título vinculado a esta conta.", statusCode: StatusCodes.Status404NotFound);

            var lista = new List<CarteirinhaDto>
            {
                new(socio.Id, socio.Nome, "Titular", socio.NumeroTitulo, socio.Categoria.ToString(),
                    socio.Situacao.ToString(), null, socio.DataNascimento,
                    Convert.ToBase64String(chaves.Para(socio.Id, socio.VersaoChaveQr))),
            };
            lista.AddRange(socio.Dependentes.Where(d => d.Ativo).OrderBy(d => d.Nome).Select(d =>
                new CarteirinhaDto(d.Id, d.Nome, "Dependente", socio.NumeroTitulo, socio.Categoria.ToString(),
                    socio.Situacao.ToString(), d.Parentesco.ToString(), d.DataNascimento,
                    Convert.ToBase64String(chaves.Para(d.Id, d.VersaoChaveQr)))));

            return Results.Ok(lista);
        }).WithTags("Carteirinha").RequireAuthorization();

        var portaria = app.MapGroup("/portaria").WithTags("Portaria").RequireAuthorization("Gestao");

        // A validação é sempre feita aqui: o app da portaria só exibe o resultado.
        portaria.MapPost("/validar", async (ValidarQrRequest req, ClaimsPrincipal user, AppDbContext db,
            ChavesCarteirinha chaves, TimeProvider relogio, FinanceiroService fin, CancellationToken ct) =>
        {
            var agora = relogio.GetUtcNow();
            var registro = new RegistroAcesso { OperadorId = user.FindFirstValue("sub") ?? string.Empty, Em = agora };
            ResultadoAcessoDto resultado;

            if (!QrCarteirinha.TentarLer(req.Token, out var leitura))
            {
                resultado = Negado("QR Code não reconhecido");
            }
            else
            {
                var pessoa = await BuscarPessoaAsync(db, leitura.PessoaId, ct);
                if (pessoa is null)
                {
                    resultado = Negado("Carteirinha não encontrada");
                }
                else
                {
                    registro.PessoaId = leitura.PessoaId;
                    registro.Nome = pessoa.Nome;
                    var falha = QrCarteirinha.Validar(req.Token, chaves.Para(leitura.PessoaId, pessoa.VersaoChave), agora);
                    var inadimplente = falha == FalhaQr.Nenhuma && await fin.InadimplenteAsync(pessoa.SocioId, ct);
                    var bloquear = inadimplente && (await fin.ConfiguracaoAsync(ct)).BloquearInadimplentesNaPortaria;
                    var decisao = falha switch
                    {
                        FalhaQr.Nenhuma => RegrasAcesso.Avaliar(pessoa.Situacao, pessoa.Ativo, inadimplente, bloquear),
                        FalhaQr.Expirado => new DecisaoAcesso(false, "QR Code expirado. Peça para atualizar a carteirinha."),
                        _ => new DecisaoAcesso(false, "QR Code inválido"),
                    };
                    resultado = new ResultadoAcessoDto(decisao.Liberado, decisao.Motivo, pessoa.Nome, pessoa.Tipo,
                        pessoa.NumeroTitulo, pessoa.Situacao.ToString(), agora, inadimplente);
                }
            }

            registro.Liberado = resultado.Liberado;
            registro.Motivo = resultado.Motivo.Length > 100 ? resultado.Motivo[..100] : resultado.Motivo;
            db.RegistrosAcesso.Add(registro);
            await db.SaveChangesAsync(ct);
            return Results.Ok(resultado);

            ResultadoAcessoDto Negado(string motivo) => new(false, motivo, null, null, null, null, agora);
        });

        portaria.MapGet("/acessos", async (AppDbContext db, int? limite, CancellationToken ct) =>
        {
            var itens = await db.RegistrosAcesso.AsNoTracking()
                .OrderByDescending(r => r.Em)
                .Take(Math.Clamp(limite ?? 50, 1, 200))
                .Select(r => new RegistroAcessoDto(r.Id, r.Nome, r.Liberado, r.Motivo, r.Em))
                .ToListAsync(ct);
            return Results.Ok(itens);
        });

        return app;
    }

    private sealed record Pessoa(Guid SocioId, string Nome, string Tipo, int NumeroTitulo, SituacaoTitulo Situacao, bool Ativo, int VersaoChave);

    private static async Task<Pessoa?> BuscarPessoaAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        var titular = await db.Socios.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new Pessoa(s.Id, s.Nome, "Titular", s.NumeroTitulo, s.Situacao, true, s.VersaoChaveQr))
            .FirstOrDefaultAsync(ct);
        if (titular is not null) return titular;

        return await db.Dependentes.AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new Pessoa(d.SocioId, d.Nome, "Dependente", d.Socio!.NumeroTitulo, d.Socio.Situacao, d.Ativo, d.VersaoChaveQr))
            .FirstOrDefaultAsync(ct);
    }
}
