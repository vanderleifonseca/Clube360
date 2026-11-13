using System.Collections.Concurrent;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ClubeDaOnca.Api.Infrastructure.Email;

public sealed record AnexoEmail(string Nome, string Tipo, byte[] Conteudo);

/// <param name="NomeRemetente">Nome exibido no "De" (ex.: o nome da associação); nulo usa o padrão configurado.</param>
public sealed record MensagemEmail(
    string Para,
    string? NomePara,
    string Assunto,
    string Html,
    IReadOnlyList<AnexoEmail> Anexos,
    string? NomeRemetente = null);

public interface IEnvioEmail
{
    Task EnviarAsync(MensagemEmail mensagem, CancellationToken ct = default);
}

public sealed class EmailOptions
{
    public const string Secao = "Email";

    /// <summary>Smtp (produção), Arquivo (grava .eml numa pasta, para desenvolvimento) ou Memoria (testes).</summary>
    public string Modo { get; set; } = "Arquivo";
    public string Remetente { get; set; } = "nao-responda@clubedaonca.local";
    public string NomeRemetente { get; set; } = "Clube da Onça";
    public string Pasta { get; set; } = "App_Data/emails";
    public SmtpOptions Smtp { get; set; } = new();

    public sealed class SmtpOptions
    {
        public string? Host { get; set; }
        public int Porta { get; set; } = 587;
        public string? Usuario { get; set; }
        public string? Senha { get; set; }

        /// <summary>StartTls (porta 587), SslOnConnect (porta 465) ou None.</summary>
        public string Seguranca { get; set; } = "StartTls";
    }
}

/// <summary>Monta a mensagem MIME (comum ao envio SMTP e à gravação em arquivo).</summary>
internal static class Mime
{
    public static MimeMessage Montar(MensagemEmail m, EmailOptions opt)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(m.NomeRemetente ?? opt.NomeRemetente, opt.Remetente));
        msg.To.Add(new MailboxAddress(m.NomePara ?? m.Para, m.Para));
        msg.Subject = m.Assunto;

        var corpo = new BodyBuilder { HtmlBody = m.Html };
        // Logo embutida no e-mail (não depende de o leitor baixar imagens da internet).
        if (m.Html.Contains($"cid:{Marca.CidEmail}") && Marca.LogoEmail is { } logo)
        {
            var img = corpo.LinkedResources.Add("logo.png", logo, ContentType.Parse("image/png"));
            img.ContentId = Marca.CidEmail;
        }
        foreach (var a in m.Anexos)
            corpo.Attachments.Add(a.Nome, a.Conteudo, ContentType.Parse(a.Tipo));
        msg.Body = corpo.ToMessageBody();
        return msg;
    }
}

public sealed class EmailSmtp(IOptions<EmailOptions> options, ILogger<EmailSmtp> log) : IEnvioEmail
{
    public async Task EnviarAsync(MensagemEmail mensagem, CancellationToken ct = default)
    {
        var opt = options.Value;
        var msg = Mime.Montar(mensagem, opt);
        using var smtp = new SmtpClient();
        var seguranca = Enum.TryParse<SecureSocketOptions>(opt.Smtp.Seguranca, true, out var s) ? s : SecureSocketOptions.StartTls;
        await smtp.ConnectAsync(opt.Smtp.Host, opt.Smtp.Porta, seguranca, ct);
        if (!string.IsNullOrWhiteSpace(opt.Smtp.Usuario))
            await smtp.AuthenticateAsync(opt.Smtp.Usuario, opt.Smtp.Senha, ct);
        await smtp.SendAsync(new MailKit.SendRequest(msg), ct);
        await smtp.DisconnectAsync(true, ct);
        log.LogInformation("E-mail \"{Assunto}\" enviado para {Para}.", mensagem.Assunto, mensagem.Para);
    }
}

/// <summary>Desenvolvimento: grava cada e-mail como .eml (abre no Outlook/Thunderbird ou no navegador).</summary>
public sealed class EmailArquivo(IOptions<EmailOptions> options, IWebHostEnvironment env, ILogger<EmailArquivo> log) : IEnvioEmail
{
    public async Task EnviarAsync(MensagemEmail mensagem, CancellationToken ct = default)
    {
        var opt = options.Value;
        var pasta = Path.IsPathRooted(opt.Pasta) ? opt.Pasta : Path.Combine(env.ContentRootPath, opt.Pasta);
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.eml");
        await Mime.Montar(mensagem, opt).WriteToAsync(arquivo, ct);
        log.LogInformation("E-mail \"{Assunto}\" para {Para} gravado em {Arquivo} (modo Arquivo).", mensagem.Assunto, mensagem.Para, arquivo);
    }
}

/// <summary>Testes: guarda as mensagens para conferência.</summary>
public sealed class EmailMemoria : IEnvioEmail
{
    public ConcurrentQueue<MensagemEmail> Enviados { get; } = new();

    public Task EnviarAsync(MensagemEmail mensagem, CancellationToken ct = default)
    {
        Enviados.Enqueue(mensagem);
        return Task.CompletedTask;
    }
}
