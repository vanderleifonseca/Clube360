namespace ClubeDaOnca.Domain.Comum;

public static class Cpf
{
    /// <summary>Remove máscara, devolvendo apenas os dígitos.</summary>
    public static string Normalizar(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsDigit).ToArray());

    public static bool EhValido(string? valor)
    {
        var cpf = Normalizar(valor);
        if (cpf.Length != 11 || cpf.Distinct().Count() == 1)
            return false;

        var digitos = cpf.Select(c => c - '0').ToArray();
        return digitos[9] == CalcularDigito(digitos, 9) && digitos[10] == CalcularDigito(digitos, 10);
    }

    private static int CalcularDigito(int[] digitos, int tamanho)
    {
        var soma = 0;
        for (var i = 0; i < tamanho; i++)
            soma += digitos[i] * (tamanho + 1 - i);
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
