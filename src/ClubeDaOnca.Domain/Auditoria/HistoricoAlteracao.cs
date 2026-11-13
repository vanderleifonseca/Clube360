using System.Globalization;
using ClubeDaOnca.Domain.Associacoes;

namespace ClubeDaOnca.Domain.Auditoria;

public enum AcaoHistorico
{
    Criado = 1,
    Alterado = 2,
    Removido = 3,
}

/// <summary>Uma alteração de cadastro: quem mudou, quando, qual campo e de qual valor para qual.</summary>
public sealed class HistoricoAlteracao : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }

    /// <summary>Tipo do registro alterado ("Socio", "Dependente", "Associacao").</summary>
    public string Entidade { get; set; } = string.Empty;
    public Guid EntidadeId { get; set; }

    /// <summary>Registro principal ao qual a alteração pertence (ex.: o titular, para alterações de dependentes).</summary>
    public Guid RaizId { get; set; }

    /// <summary>Descrição curta de quem foi alterado (ex.: "Dependente Ana Souza").</summary>
    public string? Descricao { get; set; }

    public AcaoHistorico Acao { get; set; }

    /// <summary>Rótulo do campo em português; nulo nas linhas de criação/remoção.</summary>
    public string? Campo { get; set; }
    public string? ValorAnterior { get; set; }
    public string? ValorNovo { get; set; }

    public string? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = "Sistema";
    public DateTimeOffset Em { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Rótulos e formatação dos valores do histórico (texto que a secretaria lê na tela).</summary>
public static class FormatadorHistorico
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>Campos técnicos que não aparecem no histórico.</summary>
    public static readonly IReadOnlySet<string> Ignorados = new HashSet<string>
    {
        "Id", "AssociacaoId", "UsuarioId", "CriadoEm", "SocioId", "NumeroTitulo",
        "Ano", "Sequencial", "Dependentes", "TokenAceiteHash", "TokenExpiraEm", "Documento", "DocumentoTipo",
        "DocumentoGeradoEm", "EnviadoEm", "AceiteEm", "AprovadoEm", "AprovadoPor", "AprovadoPorUsuarioId", "DesligadoEm",
        "ContratoAnteriorId", "AsaasApiKeyProtegida", "AsaasWebhookTokenProtegido", "EmitidaEm",
    };

    private static readonly Dictionary<string, string> Rotulos = new()
    {
        ["Nome"] = "Nome",
        ["Cpf"] = "CPF",
        ["Cnpj"] = "CNPJ",
        ["Email"] = "E-mail",
        ["Telefone"] = "Telefone",
        ["DataNascimento"] = "Data de nascimento",
        ["Genero"] = "Gênero",
        ["Profissao"] = "Profissão",
        ["Nacionalidade"] = "Nacionalidade",
        ["EstadoCivil"] = "Estado civil",
        ["Rg"] = "RG",
        ["ReceberAvisosApp"] = "Receber avisos no app",
        ["ReceberEmail"] = "Receber divulgação por e-mail",
        ["ReceberWhatsApp"] = "Receber divulgação por WhatsApp",
        ["Status"] = "Status do contrato",
        ["ValorMensalidade"] = "Valor da mensalidade",
        ["DiaVencimento"] = "Dia de vencimento",
        ["PrimeiroVencimento"] = "Primeiro vencimento",
        ["VigenciaInicio"] = "Início da vigência",
        ["VigenciaFim"] = "Fim da vigência",
        ["RenovacaoAutomatica"] = "Renovação automática",
        ["MotivoReprovacao"] = "Motivo da reprovação",
        ["MotivoDesligamento"] = "Motivo do desligamento",
        ["EnviadoPara"] = "Proposta enviada para",
        ["AceiteIp"] = "Aceite — IP",
        ["DataCadastro"] = "Data de cadastro",
        ["Categoria"] = "Categoria",
        ["Situacao"] = "Situação",
        ["Parentesco"] = "Parentesco",
        ["Ativo"] = "Ativo",
        ["Ativa"] = "Ativa",
        ["VersaoChaveQr"] = "QR Code",
        ["Cep"] = "Endereço — CEP",
        ["Logradouro"] = "Endereço — logradouro",
        ["Numero"] = "Endereço — número",
        ["Complemento"] = "Endereço — complemento",
        ["Bairro"] = "Endereço — bairro",
        ["Cidade"] = "Endereço — cidade",
        ["Uf"] = "Endereço — UF",
    };

    private static readonly Dictionary<string, string> TextosEnum = new()
    {
        ["NaoInformado"] = "Não informado",
        ["Conjuge"] = "Cônjuge",
        ["Mae"] = "Mãe",
        ["Solteiro"] = "Solteiro(a)",
        ["Casado"] = "Casado(a)",
        ["UniaoEstavel"] = "União estável",
        ["EmAdesao"] = "Em adesão",
        ["Salao"] = "Salão",
        ["Auditorio"] = "Auditório",
        ["AguardandoAceite"] = "Aguardando aceite",
        ["EmAnalise"] = "Em análise (presidente)",
        ["Separado"] = "Separado(a)",
        ["Divorciado"] = "Divorciado(a)",
        ["Viuvo"] = "Viúvo(a)",
    };

    public static string Rotulo(string propriedade) => Rotulos.GetValueOrDefault(propriedade, propriedade);

    public static string? Formatar(object? valor) => valor switch
    {
        null => null,
        string s when string.IsNullOrWhiteSpace(s) => null,
        string s => s,
        bool b => b ? "Sim" : "Não",
        DateOnly d => d.ToString("dd/MM/yyyy", PtBr),
        DateTime dt => dt.ToString("dd/MM/yyyy HH:mm", PtBr),
        DateTimeOffset dto => dto.ToString("dd/MM/yyyy HH:mm", PtBr),
        TimeOnly t => t.ToString("HH:mm", PtBr),
        decimal m => m.ToString("C", PtBr),
        Enum e => TextosEnum.GetValueOrDefault(e.ToString(), e.ToString()),
        IFormattable f => f.ToString(null, PtBr),
        _ => valor.ToString(),
    };

    /// <summary>Linhas de alteração de um registro (só os campos que realmente mudaram).</summary>
    public static IEnumerable<(string Campo, string? Anterior, string? Novo)> Diferencas(
        IEnumerable<(string Propriedade, object? Anterior, object? Novo)> propriedades)
    {
        foreach (var (prop, anterior, novo) in propriedades)
        {
            if (Ignorados.Contains(prop)) continue;
            if (prop == "VersaoChaveQr")
            {
                if (!Equals(anterior, novo)) yield return ("QR Code", null, "Renovado");
                continue;
            }
            var a = Formatar(anterior);
            var n = Formatar(novo);
            if (a != n) yield return (Rotulo(prop), a, n);
        }
    }
}
