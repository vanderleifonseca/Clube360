using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Socios;

namespace ClubeDaOnca.Domain.Contratos;

public enum StatusContrato
{
    /// <summary>Em preparação pela secretaria.</summary>
    Rascunho = 1,

    /// <summary>Proposta enviada por e-mail; aguardando o candidato concordar pelo link.</summary>
    AguardandoAceite = 2,

    /// <summary>Candidato concordou; aguardando o aval do presidente.</summary>
    EmAnalise = 3,

    Aprovado = 4,
    Reprovado = 5,

    /// <summary>Associado saiu do clube.</summary>
    Desligado = 6,

    /// <summary>Substituído pelo contrato de renovação.</summary>
    Encerrado = 7,
}

/// <summary>Dependente como constava no contrato no momento do envio.</summary>
public sealed record DependenteContrato(string Nome, Parentesco Parentesco, DateOnly DataNascimento, string? Cpf);

/// <summary>Contrato de adesão ao quadro associativo (anual, renovável).</summary>
public sealed class Contrato : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }
    public Guid SocioId { get; set; }
    public Socio? Socio { get; set; }

    /// <summary>Número exibido: "2026/0001" (sequencial por associação e ano).</summary>
    public string Numero { get; set; } = string.Empty;
    public int Ano { get; set; }
    public int Sequencial { get; set; }

    public StatusContrato Status { get; set; } = StatusContrato.Rascunho;
    public CategoriaTitulo Categoria { get; set; }
    public decimal ValorMensalidade { get; set; }
    public int DiaVencimento { get; set; }
    public DateOnly PrimeiroVencimento { get; set; }

    public DateOnly? VigenciaInicio { get; set; }
    public DateOnly? VigenciaFim { get; set; }
    public bool RenovacaoAutomatica { get; set; } = true;
    public Guid? ContratoAnteriorId { get; set; }

    public List<DependenteContrato> Dependentes { get; set; } = [];

    public DateTimeOffset? EnviadoEm { get; set; }
    public string? EnviadoPara { get; set; }

    /// <summary>SHA-256 do token do link de aceite (o token em si só vai no e-mail).</summary>
    public string? TokenAceiteHash { get; set; }
    public DateTimeOffset? TokenExpiraEm { get; set; }
    public DateTimeOffset? AceiteEm { get; set; }
    public string? AceiteIp { get; set; }

    public DateTimeOffset? AprovadoEm { get; set; }
    public string? AprovadoPor { get; set; }
    public string? AprovadoPorUsuarioId { get; set; }

    /// <summary>Data e autor da reprovação ficam em AprovadoEm/AprovadoPor (é a decisão do presidente).</summary>
    public string? MotivoReprovacao { get; set; }

    public DateTimeOffset? DesligadoEm { get; set; }
    public string? MotivoDesligamento { get; set; }

    /// <summary>Último documento gerado (PDF, ou DOCX quando não há conversor).</summary>
    public byte[]? Documento { get; set; }
    public string? DocumentoTipo { get; set; }
    public DateTimeOffset? DocumentoGeradoEm { get; set; }

    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Contrato em andamento (impede abrir outro para o mesmo sócio).</summary>
    public bool EmAberto => Status is StatusContrato.Rascunho or StatusContrato.AguardandoAceite
        or StatusContrato.EmAnalise or StatusContrato.Aprovado;
}

public static class RegrasContrato
{
    public const int MesesVigencia = 12;
    public const int DiasValidadeLink = 15;

    private static readonly Dictionary<StatusContrato, StatusContrato[]> Transicoes = new()
    {
        [StatusContrato.Rascunho] = [StatusContrato.AguardandoAceite],
        [StatusContrato.AguardandoAceite] = [StatusContrato.AguardandoAceite, StatusContrato.Rascunho, StatusContrato.EmAnalise],
        [StatusContrato.EmAnalise] = [StatusContrato.Aprovado, StatusContrato.Reprovado],
        [StatusContrato.Aprovado] = [StatusContrato.Desligado, StatusContrato.Encerrado],
    };

    public static bool PodeMudar(StatusContrato de, StatusContrato para) =>
        Transicoes.TryGetValue(de, out var destinos) && destinos.Contains(para);

    public static bool Editavel(StatusContrato status) =>
        status is StatusContrato.Rascunho or StatusContrato.AguardandoAceite;

    /// <summary>12 meses a partir do início (o último dia é a véspera do aniversário).</summary>
    public static (DateOnly Inicio, DateOnly Fim) Vigencia(DateOnly inicio) =>
        (inicio, inicio.AddMonths(MesesVigencia).AddDays(-1));

    public static string Numero(int ano, int sequencial) => $"{ano}/{sequencial:D4}";

    /// <summary>Primeiro vencimento: o dia configurado no mês seguinte ao de referência.</summary>
    public static DateOnly PrimeiroVencimento(DateOnly referencia, int dia)
    {
        var mes = new DateOnly(referencia.Year, referencia.Month, 1).AddMonths(1);
        return new DateOnly(mes.Year, mes.Month, Math.Min(dia, DateTime.DaysInMonth(mes.Year, mes.Month)));
    }

    /// <summary>Dados que o contrato exige do titular (opcionais no cadastro, obrigatórios aqui).</summary>
    public static List<string> PendenciasCadastro(Socio s)
    {
        var p = new List<string>();
        if (string.IsNullOrWhiteSpace(s.Email)) p.Add("E-mail (a proposta é enviada por e-mail)");
        if (string.IsNullOrWhiteSpace(s.Nacionalidade)) p.Add("Nacionalidade");
        if (s.EstadoCivil == EstadoCivil.NaoInformado) p.Add("Estado civil");
        if (string.IsNullOrWhiteSpace(s.Profissao)) p.Add("Qualificação (profissão)");
        if (string.IsNullOrWhiteSpace(s.Rg)) p.Add("RG");
        var e = s.Endereco;
        if (string.IsNullOrWhiteSpace(e.Cep)) p.Add("Endereço — CEP");
        if (string.IsNullOrWhiteSpace(e.Logradouro)) p.Add("Endereço — logradouro");
        if (string.IsNullOrWhiteSpace(e.Numero)) p.Add("Endereço — número");
        if (string.IsNullOrWhiteSpace(e.Bairro)) p.Add("Endereço — bairro");
        if (string.IsNullOrWhiteSpace(e.Cidade)) p.Add("Endereço — cidade");
        if (string.IsNullOrWhiteSpace(e.Uf)) p.Add("Endereço — UF");
        return p;
    }
}
