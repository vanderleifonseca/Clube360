namespace ClubeDaOnca.Domain.Comum;

public static class Cnpj
{
    private static readonly int[] Pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] Pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>Remove máscara, devolvendo apenas os dígitos.</summary>
    public static string Normalizar(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsDigit).ToArray());

    public static bool EhValido(string? valor)
    {
        var cnpj = Normalizar(valor);
        if (cnpj.Length != 14 || cnpj.Distinct().Count() == 1)
            return false;

        var d = cnpj.Select(c => c - '0').ToArray();
        return d[12] == Digito(d, Pesos1) && d[13] == Digito(d, Pesos2);
    }

    public static string Formatar(string? valor)
    {
        var c = Normalizar(valor);
        return c.Length == 14 ? $"{c[..2]}.{c[2..5]}.{c[5..8]}/{c[8..12]}-{c[12..]}" : c;
    }

    private static int Digito(int[] d, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++) soma += d[i] * pesos[i];
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
