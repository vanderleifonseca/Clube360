using ClubeDaOnca.Domain.Associacoes;
namespace ClubeDaOnca.Domain.Socios;

public enum SituacaoTitulo
{
    Ativo = 1,
    Suspenso = 2,
    Cancelado = 3,

    /// <summary>Candidato: contrato de adesão ainda não aprovado pelo presidente (sem acesso, reservas ou mensalidade).</summary>
    EmAdesao = 4,
}

public enum CategoriaTitulo
{
    Individual = 1,
    Familiar = 2,
    Remido = 3,
}

public enum Genero
{
    NaoInformado = 0,
    Masculino = 1,
    Feminino = 2,
    Outro = 3,
}

public enum EstadoCivil
{
    NaoInformado = 0,
    Solteiro = 1,
    Casado = 2,
    UniaoEstavel = 3,
    Separado = 4,
    Divorciado = 5,
    Viuvo = 6,
}

public enum Parentesco
{
    Conjuge = 1,
    Filho = 2,
    Enteado = 3,
    Pai = 4,
    Mae = 5,
    Outro = 9,
}

/// <summary>Sócio titular de um título do clube.</summary>
public sealed class Socio : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }

    /// <summary>Número do título (sequencial, exibido na carteirinha).</summary>
    public int NumeroTitulo { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>CPF somente com dígitos.</summary>
    public string Cpf { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public DateOnly DataNascimento { get; set; }
    public Genero Genero { get; set; }

    /// <summary>Qualificação (profissão) do associado, usada no contrato.</summary>
    public string? Profissao { get; set; }

    /// <summary>Nacionalidade (ex.: Brasileira), usada na qualificação do contrato.</summary>
    public string? Nacionalidade { get; set; }

    public EstadoCivil EstadoCivil { get; set; }

    /// <summary>RG com órgão emissor, como digitado (ex.: 12.345.678-9 SSP/PR).</summary>
    public string? Rg { get; set; }

    public Endereco Endereco { get; set; } = new();

    /// <summary>Data de entrada no clube (editável para sócios antigos; o registro técnico é <see cref="CriadoEm"/>).</summary>
    public DateOnly DataCadastro { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public CategoriaTitulo Categoria { get; set; } = CategoriaTitulo.Individual;
    public SituacaoTitulo Situacao { get; set; } = SituacaoTitulo.Ativo;

    /// <summary>Conta de acesso ao app (Identity), quando houver.</summary>
    public string? UsuarioId { get; set; }

    /// <summary>Incrementar invalida todos os QR Codes já gerados (ex.: celular perdido).</summary>
    public int VersaoChaveQr { get; set; } = 1;

    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    // Preferências de divulgação (LGPD): o sócio escolhe os canais. Avisos de cobrança e contrato sempre chegam.
    public bool ReceberAvisosApp { get; set; } = true;
    public bool ReceberEmail { get; set; } = true;
    public bool ReceberWhatsApp { get; set; } = true;

    public List<Dependente> Dependentes { get; set; } = [];
}

/// <summary>Dependente vinculado ao título de um sócio.</summary>
public sealed class Dependente : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }
    public Guid SocioId { get; set; }
    public Socio? Socio { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>CPF somente com dígitos (opcional para menores).</summary>
    public string? Cpf { get; set; }

    public DateOnly DataNascimento { get; set; }
    public Parentesco Parentesco { get; set; }
    public Genero Genero { get; set; }
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? Profissao { get; set; }
    public bool Ativo { get; set; } = true;
    public int VersaoChaveQr { get; set; } = 1;
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Registro de cada leitura de carteirinha na portaria.</summary>
public sealed class RegistroAcesso : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }

    /// <summary>Id do sócio ou do dependente lido; nulo quando o QR é inválido.</summary>
    public Guid? PessoaId { get; set; }
    public string? Nome { get; set; }
    public bool Liberado { get; set; }
    public string Motivo { get; set; } = string.Empty;

    /// <summary>Usuário da portaria que fez a leitura.</summary>
    public string OperadorId { get; set; } = string.Empty;
    public DateTimeOffset Em { get; set; } = DateTimeOffset.UtcNow;
}
