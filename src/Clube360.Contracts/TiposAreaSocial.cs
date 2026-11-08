namespace Clube360.Contracts;

public static class TiposAreaSocial
{
    public const string Piscina = "Piscina";
    public const string Academia = "Academia";
    public const string Churrasqueira = "Churrasqueira";
    public const string Quadra = "Quadra de esportes";
    public const string Campo = "Campo de futebol";
    public const string Salao = "Salão de festas";

    public static IReadOnlyList<string> Todos { get; } =
        Array.AsReadOnly(new[] { Piscina, Academia, Churrasqueira, Quadra, Campo, Salao });

    public static bool Reservavel(string? tipo) =>
        tipo is Churrasqueira or Quadra or Campo or Salao;

    public static string? Identificar(string? tipo, string nome)
    {
        if (tipo is not null)
            return tipo;

        // Preserva a agenda dos salões já cadastrados antes da classificação.
        if (string.Equals(nome, "Salão social", StringComparison.OrdinalIgnoreCase))
            return Salao;

        return Todos.FirstOrDefault(t => string.Equals(t, nome, StringComparison.OrdinalIgnoreCase));
    }
}
