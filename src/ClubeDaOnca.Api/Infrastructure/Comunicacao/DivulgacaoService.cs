using System.Globalization;
using System.Net;
using ClubeDaOnca.Api.Infrastructure.Email;
using ClubeDaOnca.Contracts.Comunicacao;
using ClubeDaOnca.Domain.Comunicacao;
using ClubeDaOnca.Domain.Eventos;
using ClubeDaOnca.Domain.Socios;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClubeDaOnca.Api.Infrastructure.Comunicacao;

/// <summary>
/// Comunicação com os associados: divulgações (eventos e comunicados) pelos canais escolhidos,
/// respeitando as preferências de cada sócio, e avisos automáticos (cobrança, contrato, evento cancelado).
/// </summary>
public sealed class DivulgacaoService(
    AppDbContext db,
    IEnvioEmail email,
    IEnvioWhatsApp whatsApp,
    IOptions<EmailOptions> emailOptions,
    IServiceProvider services,
    UsuarioAtual usuario,
    ILogger<DivulgacaoService> log)
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public CanaisDisponiveisDto Canais() =>
        new(true, emailOptions.Value.Modo, whatsApp.Configurado, whatsApp.Detalhe);

    private bool Configurado(CanalComunicacao canal) => canal switch
    {
        CanalComunicacao.WhatsApp => whatsApp.Configurado,
        _ => true,
    };

    // ---------- Divulgações ----------

    public async Task<Divulgacao> CriarAsync(string titulo, string mensagem, IReadOnlyCollection<CanalComunicacao> canais,
        Guid? eventoId, CancellationToken ct = default)
    {
        var d = new Divulgacao
        {
            Titulo = titulo.Trim(),
            Mensagem = mensagem.Trim(),
            EventoId = eventoId,
            Canais = canais.Distinct().Order().ToList(),
            CriadoPor = usuario.Nome,
        };
        db.Divulgacoes.Add(d);
        await db.SaveChangesAsync(ct);

        // Com Hangfire, vai para a fila (centenas de e-mails/WhatsApp não travam a tela); sem ele (testes), envia na hora.
        var associacaoId = db.Tenant.Exigir();
        if (services.GetService<IBackgroundJobClient>() is { } fila)
            fila.Enqueue<JobsComunicacao>(j => j.ProcessarDivulgacaoAsync(associacaoId, d.Id));
        else
            await ProcessarAsync(d.Id, ct);
        return d;
    }

    /// <summary>Divulgação automática quando um evento é publicado com "divulgar aos associados".</summary>
    public Task<Divulgacao> DivulgarEventoAsync(Evento e, CancellationToken ct = default)
    {
        var locais = string.Join(", ", e.Locais.Select(l => l.Espaco?.Nome).Where(n => n is not null));
        var mensagem = $"{Quando(e)}{(locais.Length > 0 ? $", em {locais}" : "")}. " +
                       (string.IsNullOrWhiteSpace(e.Descricao) ? "" : $"{e.Descricao.Trim()} ") +
                       "Reserve pelo app do clube, na aba Eventos." +
                       (e.AbertoAoPublico ? " Aberto ao público: traga seus convidados!" : "");
        return CriarAsync(e.Titulo, mensagem, [CanalComunicacao.App, CanalComunicacao.Email, CanalComunicacao.WhatsApp], e.Id, ct);
    }

    public async Task ProcessarAsync(Guid divulgacaoId, CancellationToken ct = default)
    {
        var d = await db.Divulgacoes.FirstOrDefaultAsync(x => x.Id == divulgacaoId, ct);
        if (d is null || d.Status == StatusDivulgacao.Concluida) return;
        d.Status = StatusDivulgacao.Enviando;
        await db.SaveChangesAsync(ct);

        var associacao = await db.Associacoes.AsNoTracking().Where(a => a.Id == d.AssociacaoId).Select(a => a.Nome).FirstAsync(ct);
        var jaEnviados = (await db.EnviosDivulgacao.Where(e => e.DivulgacaoId == d.Id)
            .Select(e => new { e.SocioId, e.Canal }).ToListAsync(ct)).Select(e => (e.SocioId, e.Canal)).ToHashSet();
        var socios = await db.Socios.AsNoTracking().OrderBy(s => s.Nome).ToListAsync(ct);

        var lote = 0;
        foreach (var s in socios)
        {
            foreach (var canal in d.Canais)
            {
                if (jaEnviados.Contains((s.Id, canal))) continue; // reprocessamento: não repete
                var envio = new EnvioDivulgacao { DivulgacaoId = d.Id, SocioId = s.Id, Canal = canal };
                if (RegrasComunicacao.MotivoParaNaoEnviar(s, canal, Configurado(canal)) is { } motivo)
                {
                    envio.Status = StatusEnvio.Ignorado;
                    envio.Detalhe = motivo;
                }
                else
                {
                    try
                    {
                        await EnviarAsync(d, s, canal, associacao, ct);
                        envio.Status = StatusEnvio.Enviado;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        envio.Status = StatusEnvio.Falhou;
                        envio.Detalhe = RegrasComunicacao.Resumir(ex.Message, 300);
                        log.LogWarning(ex, "Divulgação {Divulgacao}: falha no {Canal} para {Socio}.", d.Id, canal, s.Id);
                    }
                }
                db.EnviosDivulgacao.Add(envio);
                if (++lote % 50 == 0) await db.SaveChangesAsync(ct);
            }
        }

        d.Status = StatusDivulgacao.Concluida;
        d.ConcluidaEm = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        log.LogInformation("Divulgação \"{Titulo}\" concluída.", d.Titulo);
    }

    private async Task EnviarAsync(Divulgacao d, Socio s, CanalComunicacao canal, string associacao, CancellationToken ct)
    {
        switch (canal)
        {
            case CanalComunicacao.App:
                db.Avisos.Add(new Aviso
                {
                    SocioId = s.Id, Titulo = d.Titulo, Mensagem = d.Mensagem, DivulgacaoId = d.Id,
                    Tipo = d.EventoId is null ? TipoAviso.Geral : TipoAviso.Evento, ReferenciaId = d.EventoId,
                });
                break;
            case CanalComunicacao.Email:
                await email.EnviarAsync(new MensagemEmail(s.Email!, s.Nome, d.Titulo, Html(associacao, s.Nome, d.Titulo, d.Mensagem), [], associacao), ct);
                break;
            case CanalComunicacao.WhatsApp:
                await whatsApp.EnviarAsync(RegrasComunicacao.TelefoneWhatsApp(s.Telefone)!, s.Nome.Split(' ')[0], d.Titulo, d.Mensagem, ct);
                break;
        }
    }

    // ---------- Avisos automáticos (sempre chegam: não dependem das preferências) ----------

    /// <summary>Aviso na central do app e, se pedido, também por e-mail.</summary>
    public async Task AvisarAsync(Socio socio, TipoAviso tipo, string titulo, string mensagem, Guid? referenciaId,
        bool porEmail, CancellationToken ct = default)
    {
        db.Avisos.Add(new Aviso { SocioId = socio.Id, Tipo = tipo, Titulo = titulo, Mensagem = mensagem, ReferenciaId = referenciaId });
        if (porEmail && !string.IsNullOrWhiteSpace(socio.Email))
        {
            try
            {
                var associacao = await db.Associacoes.AsNoTracking().Where(a => a.Id == socio.AssociacaoId).Select(a => a.Nome).FirstAsync(ct);
                await email.EnviarAsync(new MensagemEmail(socio.Email, socio.Nome, titulo, Html(associacao, socio.Nome, titulo, mensagem), [], associacao), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Aviso \"{Titulo}\": e-mail para {Socio} não enviado.", titulo, socio.Id);
            }
        }
    }

    // ---------- Apoio ----------

    public static string Quando(Evento e) => e.Inicio.Date == e.Termino.Date
        ? $"{e.Inicio.ToString("dddd, dd/MM", PtBr)}, das {e.Inicio:HH\\:mm} às {e.Termino:HH\\:mm}"
        : $"de {e.Inicio.ToString("dd/MM HH\\:mm", PtBr)} a {e.Termino.ToString("dd/MM HH\\:mm", PtBr)}";

    private static string Html(string associacao, string nome, string titulo, string mensagem)
    {
        static string H(string? v) => WebUtility.HtmlEncode(v ?? string.Empty);
        return $"""
            <div style="font-family:Arial,sans-serif;max-width:560px;margin:auto;color:#222">
              {Marca.ImgEmail}
              <h2 style="color:#B45309;text-align:center">{H(associacao)}</h2>
              <p>Olá, {H(nome.Split(' ')[0])}!</p>
              <h3 style="margin-bottom:4px">{H(titulo)}</h3>
              <p style="white-space:pre-line">{H(mensagem)}</p>
              <p style="font-size:12px;color:#888;margin-top:28px">Você recebe este e-mail por ser associado.
                 Para não receber divulgações por e-mail, desligue a opção em Avisos → Preferências, no app do clube.</p>
            </div>
            """;
    }
}

/// <summary>Tarefas da comunicação (Hangfire).</summary>
public sealed class JobsComunicacao(AppDbContext db, DivulgacaoService divulgacao, RelogioClube relogio, ILogger<JobsComunicacao> log)
{
    public const string AvisarVencimentos = "avisar-vencimentos";

    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessarDivulgacaoAsync(Guid associacaoId, Guid divulgacaoId)
    {
        db.Tenant.Definir(associacaoId);
        await divulgacao.ProcessarAsync(divulgacaoId);
    }

    /// <summary>Todo dia às 8h: avisa (app + e-mail) as cobranças que vencem em 3 dias, uma vez por cobrança.</summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task AvisarVencimentosAsync()
    {
        var associacoes = await db.Associacoes.Where(a => a.Ativa).Select(a => a.Id).ToListAsync();
        foreach (var id in associacoes)
        {
            db.Tenant.Definir(id);
            db.ChangeTracker.Clear();
            var total = await AvisarVencimentosDaAssociacaoAsync(relogio.Hoje);
            if (total > 0) log.LogInformation("Associação {Associacao}: {Total} aviso(s) de vencimento.", id, total);
        }
    }

    public async Task<int> AvisarVencimentosDaAssociacaoAsync(DateOnly hoje)
    {
        var alvo = hoje.AddDays(3);
        var cobrancas = await db.Cobrancas.Include(c => c.Socio)
            .Where(c => c.Status == Domain.Financeiro.StatusCobranca.Aberta && c.Vencimento == alvo
                && !db.Avisos.Any(a => a.ReferenciaId == c.Id && a.Tipo == TipoAviso.Cobranca))
            .ToListAsync();
        foreach (var c in cobrancas)
            await divulgacao.AvisarAsync(c.Socio!, TipoAviso.Cobranca,
                c.Tipo == Domain.Financeiro.TipoCobranca.Mensalidade ? "Mensalidade vencendo" : "Cobrança vencendo",
                $"{c.Descricao}: {c.Valor.ToString("C", new CultureInfo("pt-BR"))}, vence em {c.Vencimento:dd/MM/yyyy}. " +
                "Pague pelo app, na aba Financeiro (Pix ou boleto).", c.Id, porEmail: true);
        await db.SaveChangesAsync();
        return cobrancas.Count;
    }
}
