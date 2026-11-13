namespace ClubeDaOnca.Contracts.Comunicacao;

/// <param name="Tipo">Geral, Evento, Cobranca ou Contrato.</param>
/// <param name="ReferenciaId">Evento, cobrança ou contrato relacionado (para abrir a tela certa).</param>
public sealed record AvisoDto(
    Guid Id,
    string Tipo,
    string Titulo,
    string Mensagem,
    Guid? ReferenciaId,
    DateTimeOffset CriadoEm,
    bool Lido);

public sealed record ResumoAvisosDto(int NaoLidos);

/// <summary>Canais pelos quais o sócio aceita receber divulgações (avisos de cobrança e contrato sempre chegam).</summary>
public sealed record PreferenciasComunicacaoDto(bool AvisosApp, bool Email, bool WhatsApp);

/// <param name="Canais">"App", "Email" e/ou "WhatsApp".</param>
/// <param name="EventoId">Divulgação de um evento (opcional): o texto pode citar data, local e como reservar.</param>
public sealed record NovaDivulgacaoRequest(string Titulo, string Mensagem, IReadOnlyList<string> Canais, Guid? EventoId = null);

public sealed record TotaisCanalDto(string Canal, int Enviados, int Falhas, int Ignorados);

/// <param name="Status">Pendente, Enviando ou Concluida.</param>
public sealed record DivulgacaoDto(
    Guid Id,
    string Titulo,
    string Mensagem,
    Guid? EventoId,
    IReadOnlyList<string> Canais,
    string Status,
    string CriadoPor,
    DateTimeOffset CriadoEm,
    DateTimeOffset? ConcluidaEm,
    IReadOnlyList<TotaisCanalDto> Totais);

public sealed record FalhaEnvioDto(string Socio, int NumeroTitulo, string Canal, string Status, string? Detalhe);

/// <summary>Quais canais o servidor está pronto para usar.</summary>
/// <param name="Email">Modo do e-mail (Smtp envia de verdade; Arquivo grava em pasta, para testes).</param>
public sealed record CanaisDisponiveisDto(bool App, string Email, bool WhatsApp, string? WhatsAppDetalhe);

/// <summary>Texto pronto para publicar nas redes sociais (a imagem vem por outra rota).</summary>
public sealed record PostRedesSociaisDto(string Texto, string ImagemUrl);
