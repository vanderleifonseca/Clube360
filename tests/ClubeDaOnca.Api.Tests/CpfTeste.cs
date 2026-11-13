namespace ClubeDaOnca.Api.Tests;

/// <summary>Gera CPFs válidos e únicos para os testes.</summary>
public static class CpfTeste
{
    private static int _seq = 100_000_000 + Random.Shared.Next(0, 800_000_000);

    public static string Proximo()
    {
        var baseDigitos = Interlocked.Increment(ref _seq).ToString("D9").Select(c => c - '0').ToList();
        baseDigitos.Add(Digito(baseDigitos));
        baseDigitos.Add(Digito(baseDigitos));
        return string.Concat(baseDigitos);
    }

    private static int Digito(List<int> d)
    {
        var soma = 0;
        for (var i = 0; i < d.Count; i++)
            soma += d[i] * (d.Count + 1 - i);
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
