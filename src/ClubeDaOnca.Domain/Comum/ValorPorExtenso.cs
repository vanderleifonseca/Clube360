namespace ClubeDaOnca.Domain.Comum;

/// <summary>Valores em reais por extenso, para contratos ("cento e cinquenta reais e cinquenta centavos").</summary>
public static class ValorPorExtenso
{
    private static readonly string[] Unidades =
    [
        "zero", "um", "dois", "três", "quatro", "cinco", "seis", "sete", "oito", "nove",
        "dez", "onze", "doze", "treze", "quatorze", "quinze", "dezesseis", "dezessete", "dezoito", "dezenove",
    ];

    private static readonly string[] Dezenas = ["", "", "vinte", "trinta", "quarenta", "cinquenta", "sessenta", "setenta", "oitenta", "noventa"];

    private static readonly string[] Centenas =
        ["", "cento", "duzentos", "trezentos", "quatrocentos", "quinhentos", "seiscentos", "setecentos", "oitocentos", "novecentos"];

    public static string Reais(decimal valor)
    {
        if (valor < 0) throw new ArgumentOutOfRangeException(nameof(valor), "Valor negativo.");
        valor = Math.Round(valor, 2, MidpointRounding.AwayFromZero);
        var inteiro = (long)Math.Truncate(valor);
        var centavos = (int)((valor - inteiro) * 100);

        if (inteiro == 0 && centavos == 0) return "zero real";

        var partes = new List<string>();
        if (inteiro > 0)
        {
            // "um milhão de reais", "dois mil reais": "de" só depois de milhão/bilhão redondos.
            var sufixoDe = inteiro >= 1_000_000 && inteiro % 1_000_000 == 0 ? " de" : "";
            partes.Add($"{Numero(inteiro)}{sufixoDe} {(inteiro == 1 ? "real" : "reais")}");
        }
        if (centavos > 0)
            partes.Add($"{Numero(centavos)} {(centavos == 1 ? "centavo" : "centavos")}");
        return string.Join(" e ", partes);
    }

    /// <summary>Número inteiro por extenso (até bilhões).</summary>
    public static string Numero(long n)
    {
        if (n == 0) return "zero";
        var grupos = new List<(long Valor, string Singular, string Plural)>
        {
            (n / 1_000_000_000 % 1000, "bilhão", "bilhões"),
            (n / 1_000_000 % 1000, "milhão", "milhões"),
            (n / 1000 % 1000, "mil", "mil"),
            (n % 1000, "", ""),
        };

        var textos = new List<(string Texto, long Valor)>();
        foreach (var (valor, singular, plural) in grupos)
        {
            if (valor == 0) continue;
            string texto;
            if (singular == "mil") texto = valor == 1 ? "mil" : $"{Centena((int)valor)} mil";
            else if (singular == "") texto = Centena((int)valor);
            else texto = $"{Centena((int)valor)} {(valor == 1 ? singular : plural)}";
            textos.Add((texto, valor));
        }

        // Liga com "e" o último grupo quando ele é menor que 100 ou centena redonda (ex.: "mil e duzentos", "dois mil e cinco").
        var resultado = textos[0].Texto;
        for (var i = 1; i < textos.Count; i++)
        {
            var (texto, valor) = textos[i];
            var ultimo = i == textos.Count - 1;
            var usaE = ultimo && (valor < 100 || valor % 100 == 0);
            resultado += (usaE ? " e " : ", ") + texto;
        }
        return resultado.Replace(", ", " ");
    }

    private static string Centena(int n)
    {
        if (n == 100) return "cem";
        var c = n / 100;
        var resto = n % 100;
        var partes = new List<string>();
        if (c > 0) partes.Add(Centenas[c]);
        if (resto > 0) partes.Add(resto < 20 ? Unidades[resto] : Dezena(resto));
        return string.Join(" e ", partes);
    }

    private static string Dezena(int n) => n % 10 == 0 ? Dezenas[n / 10] : $"{Dezenas[n / 10]} e {Unidades[n % 10]}";
}
