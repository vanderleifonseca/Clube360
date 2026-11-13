namespace ClubeDaOnca.Domain.Associacoes;

/// <summary>Registro que pertence a uma associação (unidade). O banco filtra tudo pela associação do usuário logado.</summary>
public interface IPertenceAssociacao
{
    Guid AssociacaoId { get; set; }
}

/// <summary>Endereço postal (usado na associação e, depois, no cadastro do sócio).</summary>
public sealed class Endereco
{
    /// <summary>CEP somente com dígitos.</summary>
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }

    /// <summary>Sigla do estado (ex.: SP).</summary>
    public string? Uf { get; set; }

    public override string ToString()
    {
        var partes = new List<string>();
        var linha = string.Join(", ", new[] { Logradouro, Numero }.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (!string.IsNullOrWhiteSpace(Complemento)) linha = $"{linha} - {Complemento}";
        if (!string.IsNullOrWhiteSpace(linha)) partes.Add(linha);
        if (!string.IsNullOrWhiteSpace(Bairro)) partes.Add(Bairro!);
        var cidade = string.Join("/", new[] { Cidade, Uf }.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (!string.IsNullOrWhiteSpace(cidade)) partes.Add(cidade);
        if (!string.IsNullOrWhiteSpace(Cep) && Cep!.Length == 8) partes.Add($"CEP {Cep[..5]}-{Cep[5..]}");
        return string.Join(", ", partes);
    }
}

/// <summary>Associação (unidade do clube). Cada uma tem seus sócios, espaços, financeiro e diretoria.</summary>
public sealed class Associacao
{
    /// <summary>Associação criada na migração: recebeu todos os dados que já existiam (sistema de clube único).</summary>
    public static readonly Guid PadraoId = new("0c1b0000-0000-4000-8000-000000000001");

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nome { get; set; } = string.Empty;

    /// <summary>CNPJ somente com dígitos.</summary>
    public string? Cnpj { get; set; }

    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public Endereco Endereco { get; set; } = new();
    public bool Ativa { get; set; } = true;
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Chave da API do Asaas da própria unidade, criptografada (nula = usa a conta geral do servidor).</summary>
    public string? AsaasApiKeyProtegida { get; set; }

    /// <summary>Token dos webhooks do Asaas desta unidade, criptografado.</summary>
    public string? AsaasWebhookTokenProtegido { get; set; }

    public List<MandatoPresidente> Mandatos { get; set; } = [];
    public List<Diretor> Diretores { get; set; } = [];
}

/// <summary>Mandato do presidente da associação (renovado a cada 2 anos).</summary>
public sealed class MandatoPresidente
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>CPF somente com dígitos (opcional).</summary>
    public string? Cpf { get; set; }

    /// <summary>E-mail da conta do presidente: é ele quem dá o aval final às novas associações de sócios.</summary>
    public string? Email { get; set; }

    public DateOnly Inicio { get; set; }

    /// <summary>Último dia do mandato (início + 2 anos - 1 dia).</summary>
    public DateOnly Fim { get; set; }

    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Membro da diretoria da associação.</summary>
public sealed class Diretor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Cargo na diretoria (ex.: Vice-presidente, Diretor financeiro, Secretário).</summary>
    public string Cargo { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public DateOnly Inicio { get; set; }
    public DateOnly? Fim { get; set; }
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    public bool AtivoEm(DateOnly dia) => Inicio <= dia && (Fim is null || Fim >= dia);
}

/// <summary>Regras da diretoria: mandato de 2 anos, sem sobreposição.</summary>
public static class RegrasAssociacao
{
    public const int DuracaoMandatoAnos = 2;

    /// <summary>Antecedência com que o sistema avisa que o mandato do presidente está acabando.</summary>
    public const int DiasAvisoFimMandato = 60;

    public static DateOnly FimDoMandato(DateOnly inicio) => inicio.AddYears(DuracaoMandatoAnos).AddDays(-1);

    public static bool Sobrepoe(DateOnly inicio, DateOnly fim, IEnumerable<MandatoPresidente> existentes, Guid? ignorar = null) =>
        existentes.Any(m => m.Id != ignorar && m.Inicio <= fim && inicio <= m.Fim);

    public static MandatoPresidente? PresidenteEm(IEnumerable<MandatoPresidente> mandatos, DateOnly dia) =>
        mandatos.Where(m => m.Inicio <= dia && dia <= m.Fim).OrderByDescending(m => m.Inicio).FirstOrDefault();

    /// <summary>Verdadeiro quando o mandato vigente termina em até <see cref="DiasAvisoFimMandato"/> dias e não há sucessor cadastrado.</summary>
    public static bool MandatoVencendo(IEnumerable<MandatoPresidente> mandatos, DateOnly hoje)
    {
        var lista = mandatos.ToList();
        var atual = PresidenteEm(lista, hoje);
        if (atual is null) return false;
        var temSucessor = lista.Any(m => m.Inicio > atual.Fim);
        return !temSucessor && atual.Fim.DayNumber - hoje.DayNumber <= DiasAvisoFimMandato;
    }
}
