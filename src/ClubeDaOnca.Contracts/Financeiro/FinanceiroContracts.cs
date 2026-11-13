namespace ClubeDaOnca.Contracts.Financeiro;

/// <param name="Tipo">"Mensalidade" ou "TaxaReserva".</param>
/// <param name="Status">"Aberta", "Paga" ou "Cancelada".</param>
/// <param name="Vencida">Aberta e com vencimento já passado.</param>
public sealed record CobrancaDto(
    Guid Id,
    Guid SocioId,
    string Socio,
    int NumeroTitulo,
    string Tipo,
    string Descricao,
    DateOnly? Competencia,
    decimal Valor,
    DateOnly Vencimento,
    string Status,
    bool Vencida,
    DateTimeOffset? PagoEm,
    string? FormaPagamento,
    string? Observacao,
    int? Parcela = null,
    string? ContratoNumero = null,
    DateTimeOffset? EmitidaEm = null,
    string? BoletoUrl = null,
    string? LinhaDigitavel = null,
    DateTimeOffset? CriadoEm = null);

/// <summary>Resultado da emissão dos boletos das parcelas do contrato.</summary>
public sealed record EmissaoBoletosDto(int Emitidos, int Falhas, int Parcelas);

/// <param name="Inadimplente">Há cobrança vencida além da tolerância.</param>
public sealed record ResumoFinanceiroDto(
    decimal TotalEmAberto,
    decimal TotalVencido,
    bool Inadimplente,
    IReadOnlyList<CobrancaDto> Cobrancas);

/// <param name="Forma">"Pix" ou "Boleto".</param>
public sealed record PagarRequest(string Forma);

/// <param name="Status">"Pendente", "Confirmado" ou "Cancelado".</param>
public sealed record PagamentoDto(
    Guid Id,
    Guid CobrancaId,
    string Gateway,
    string Forma,
    string Status,
    decimal Valor,
    string? PixCopiaECola,
    string? PixImagemBase64,
    DateTimeOffset? PixExpiraEm,
    string? BoletoUrl,
    string? LinhaDigitavel,
    bool Simulado);

public sealed record GerarMensalidadesRequest(DateOnly Competencia);

public sealed record GerarMensalidadesResponse(DateOnly Competencia, int Geradas, int JaExistentes, int Ignoradas);

/// <param name="Forma">"Pix", "Boleto" ou "Manual".</param>
public sealed record BaixaManualRequest(string Forma, string? Observacao);

/// <summary>Cobrança avulsa lançada pelo financeiro para um sócio (multa, consumo, conserto etc.).</summary>
public sealed record LancarCobrancaRequest(Guid SocioId, string Descricao, decimal Valor, DateOnly Vencimento);

public sealed record InadimplenteDto(
    Guid SocioId,
    int NumeroTitulo,
    string Nome,
    string? Telefone,
    int CobrancasVencidas,
    decimal ValorVencido,
    DateOnly VencimentoMaisAntigo);

public sealed record ConfiguracaoFinanceiraDto(
    decimal ValorIndividual,
    decimal ValorFamiliar,
    decimal ValorRemido,
    int DiaVencimento,
    int DiasTolerancia,
    bool BloquearInadimplentesNaPortaria,
    bool BloquearInadimplentesNasReservas,
    int PrazoTaxaReservaDias);
