using System.Globalization;

namespace ClubeDaOnca.App.Services;

/// <summary>Valor enviado à API e texto exibido na tela.</summary>
public sealed record Opcao(string Valor, string Texto)
{
    public override string ToString() => Texto;
}

public static class Opcoes
{
    public static readonly IReadOnlyList<Opcao> Categorias =
    [
        new("Individual", "Individual"),
        new("Familiar", "Familiar"),
        new("Remido", "Remido"),
    ];

    public static readonly IReadOnlyList<Opcao> Parentescos =
    [
        new("Conjuge", "Cônjuge"),
        new("Filho", "Filho(a)"),
        new("Enteado", "Enteado(a)"),
        new("Pai", "Pai"),
        new("Mae", "Mãe"),
        new("Outro", "Outro"),
    ];

    public static readonly IReadOnlyList<Opcao> Generos =
    [
        new("NaoInformado", "Prefere não informar"),
        new("Feminino", "Feminino"),
        new("Masculino", "Masculino"),
        new("Outro", "Outro"),
    ];

    public static readonly IReadOnlyList<Opcao> EstadosCivis =
    [
        new("NaoInformado", "Não informado"),
        new("Solteiro", "Solteiro(a)"),
        new("Casado", "Casado(a)"),
        new("UniaoEstavel", "União estável"),
        new("Separado", "Separado(a)"),
        new("Divorciado", "Divorciado(a)"),
        new("Viuvo", "Viúvo(a)"),
    ];

    /// <summary>Filtros da lista de sócios: o primeiro item ("Todos") não filtra.</summary>
    public static readonly IReadOnlyList<Opcao> FiltroGeneros =
    [
        new("", "Todos os gêneros"),
        new("Feminino", "Feminino"),
        new("Masculino", "Masculino"),
        new("Outro", "Outro"),
        new("NaoInformado", "Não informado"),
    ];

    public static readonly IReadOnlyList<Opcao> FiltroSituacoes =
    [
        new("", "Todas as situações"),
        new("EmAdesao", "Em adesão"),
        new("Ativo", "Ativo"),
        new("Suspenso", "Suspenso"),
        new("Cancelado", "Cancelado"),
    ];

    public static readonly IReadOnlyList<Opcao> Situacoes =
    [
        new("Ativo", "Ativo"),
        new("Suspenso", "Suspenso"),
        new("Cancelado", "Cancelado"),
        new("EmAdesao", "Em adesão"),
    ];

    public static readonly IReadOnlyList<Opcao> TiposLocal =
    [
        new("Quadra", "Quadra"),
        new("Quiosque", "Quiosque"),
        new("Churrasqueira", "Churrasqueira"),
        new("Salao", "Salão"),
        new("Piscina", "Piscina"),
        new("Campo", "Campo"),
        new("Auditorio", "Auditório"),
        new("Outro", "Outro"),
    ];

    public static string StatusEvento(string? status) => status switch
    {
        "Rascunho" => "Rascunho (não publicado)",
        "Publicado" => "Publicado",
        "Cancelado" => "Cancelado",
        _ => status ?? string.Empty,
    };

    public static string StatusContrato(string? status) => status switch
    {
        "Rascunho" => "Rascunho",
        "AguardandoAceite" => "Aguardando aceite",
        "EmAnalise" => "Com o presidente",
        "Aprovado" => "Aprovado",
        "Reprovado" => "Reprovado",
        "Desligado" => "Desligado",
        "Encerrado" => "Encerrado (renovado)",
        null => "Sem contrato",
        _ => status,
    };

    public static string Texto(IReadOnlyList<Opcao> lista, string? valor) =>
        lista.FirstOrDefault(o => o.Valor == valor)?.Texto ?? valor ?? string.Empty;

    public static readonly CultureInfo PtBr = new("pt-BR");

    public static string Moeda(decimal valor) => valor.ToString("C", PtBr);

    public static readonly string[] DiasAbreviados = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];

    public static string StatusReserva(string status) => status switch
    {
        "Pendente" => "Aguardando aprovação",
        "Confirmada" => "Confirmada",
        "Cancelada" => "Cancelada",
        "Recusada" => "Recusada",
        _ => status,
    };

    public static string FormatarCpf(string? cpf) =>
        cpf is { Length: 11 } ? $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}" : cpf ?? string.Empty;
}
