using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Socios;

namespace ClubeDaOnca.Domain.Comunicacao;

public enum CanalComunicacao
{
    /// <summary>Central de avisos dentro do app (sininho).</summary>
    App = 1,
    Email = 2,
    WhatsApp = 3,
}

public enum TipoAviso
{
    Geral = 1,
    Evento = 2,
    Cobranca = 3,
    Contrato = 4,
}

public enum StatusDivulgacao
{
    Pendente = 1,
    Enviando = 2,
    Concluida = 3,
}

public enum StatusEnvio
{
    Enviado = 1,
    Falhou = 2,

    /// <summary>Não enviado de propósito: sócio desligou o canal, sem e-mail/telefone ou canal não configurado.</summary>
    Ignorado = 3,
}

/// <summary>Aviso na central do app (sininho) de um sócio titular.</summary>
public sealed class Aviso : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }
    public Guid SocioId { get; set; }
    public TipoAviso Tipo { get; set; } = TipoAviso.Geral;
    public string Titulo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;

    /// <summary>Registro relacionado (evento, cobrança, contrato) para o app abrir a tela certa.</summary>
    public Guid? ReferenciaId { get; set; }
    public Guid? DivulgacaoId { get; set; }
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LidoEm { get; set; }
}

/// <summary>Comunicado enviado aos associados (de um evento ou geral), pelos canais escolhidos.</summary>
public sealed class Divulgacao : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public Guid? EventoId { get; set; }
    public List<CanalComunicacao> Canais { get; set; } = [];
    public StatusDivulgacao Status { get; set; } = StatusDivulgacao.Pendente;
    public string CriadoPor { get; set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConcluidaEm { get; set; }
    public List<EnvioDivulgacao> Envios { get; set; } = [];
}

/// <summary>Resultado do envio de uma divulgação para um sócio em um canal.</summary>
public sealed class EnvioDivulgacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DivulgacaoId { get; set; }
    public Guid SocioId { get; set; }
    public CanalComunicacao Canal { get; set; }
    public StatusEnvio Status { get; set; }
    public string? Detalhe { get; set; }
    public DateTimeOffset Em { get; set; } = DateTimeOffset.UtcNow;
}

public static class RegrasComunicacao
{
    /// <summary>
    /// Por qual motivo o sócio NÃO recebe a divulgação no canal (nulo = recebe).
    /// Preferências valem para divulgações; avisos de cobrança e contrato sempre chegam (outra rotina).
    /// </summary>
    public static string? MotivoParaNaoEnviar(Socio s, CanalComunicacao canal, bool canalConfigurado) => canal switch
    {
        _ when s.Situacao != SituacaoTitulo.Ativo => "Título não está ativo",
        _ when !canalConfigurado => "Canal não configurado no servidor",
        CanalComunicacao.App when !s.ReceberAvisosApp => "Sócio desligou os avisos no app",
        CanalComunicacao.Email when !s.ReceberEmail => "Sócio desligou o e-mail",
        CanalComunicacao.Email when string.IsNullOrWhiteSpace(s.Email) => "Sem e-mail cadastrado",
        CanalComunicacao.WhatsApp when !s.ReceberWhatsApp => "Sócio desligou o WhatsApp",
        CanalComunicacao.WhatsApp when TelefoneWhatsApp(s.Telefone) is null => "Sem celular válido cadastrado",
        _ => null,
    };

    /// <summary>Celular no formato internacional (55 + DDD + número), ou nulo se não for um celular brasileiro válido.</summary>
    public static string? TelefoneWhatsApp(string? telefone)
    {
        var d = new string((telefone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (d.StartsWith("55") && d.Length is 12 or 13) d = d[2..];
        if (d.StartsWith('0')) d = d.TrimStart('0');
        return d.Length == 11 && d[2] == '9' ? "55" + d : null; // DDD + 9 dígitos começando com 9
    }

    /// <summary>Texto curto para mensagens com limite (ex.: variável do modelo do WhatsApp).</summary>
    public static string Resumir(string texto, int maximo)
    {
        texto = string.Join(' ', (texto ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return texto.Length <= maximo ? texto : texto[..(maximo - 1)].TrimEnd() + "…";
    }
}
