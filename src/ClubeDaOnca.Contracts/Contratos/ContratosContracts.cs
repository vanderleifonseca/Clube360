using ClubeDaOnca.Contracts.Socios;

namespace ClubeDaOnca.Contracts.Contratos;

public sealed record DependenteContratoDto(string Nome, string Parentesco, DateOnly DataNascimento, string? Cpf);

/// <param name="Status">Rascunho, AguardandoAceite, EmAnalise, Aprovado, Reprovado, Desligado ou Encerrado.</param>
/// <param name="AprovadoEm">Data da decisão do presidente (aprovação ou reprovação).</param>
public sealed record ContratoDto(
    Guid Id,
    Guid SocioId,
    string SocioNome,
    int NumeroTitulo,
    string Numero,
    string Status,
    string Categoria,
    decimal ValorMensalidade,
    int DiaVencimento,
    DateOnly PrimeiroVencimento,
    DateOnly? VigenciaInicio,
    DateOnly? VigenciaFim,
    bool RenovacaoAutomatica,
    Guid? ContratoAnteriorId,
    IReadOnlyList<DependenteContratoDto> Dependentes,
    DateTimeOffset? EnviadoEm,
    string? EnviadoPara,
    DateTimeOffset? LinkExpiraEm,
    DateTimeOffset? AceiteEm,
    string? AceiteIp,
    DateTimeOffset? AprovadoEm,
    string? AprovadoPor,
    string? MotivoReprovacao,
    DateTimeOffset? DesligadoEm,
    string? MotivoDesligamento,
    string? DocumentoTipo,
    DateTimeOffset CriadoEm);

/// <summary>Campos nulos usam o padrão (valor e vencimento da configuração financeira, categoria do título).</summary>
public sealed record SalvarContratoRequest(
    string? Categoria = null,
    decimal? ValorMensalidade = null,
    int? DiaVencimento = null,
    DateOnly? PrimeiroVencimento = null,
    bool? RenovacaoAutomatica = null);

public sealed record MotivoRequest(string Motivo);

/// <param name="EhPresidente">O usuário logado é o presidente do mandato vigente.</param>
/// <param name="UltimoAceiteEm">Aceite mais recente entre as fichas pendentes: o app avisa o presidente quando ele muda.</param>
public sealed record ResumoPresidenciaDto(bool EhPresidente, int Pendentes, string? Presidente, DateTimeOffset? UltimoAceiteEm = null);

/// <summary>Ficha do candidato para o aval do presidente.</summary>
public sealed record FichaAprovacaoDto(ContratoDto Contrato, SocioDto Socio);
