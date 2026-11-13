using System.Security.Cryptography;
using System.Text;

namespace ClubeDaOnca.Domain.Carteirinha;

public enum FalhaQr
{
    Nenhuma,
    FormatoInvalido,
    Expirado,
    AssinaturaInvalida,
}

public sealed record LeituraQr(Guid PessoaId, long Passo);

/// <summary>
/// QR Code dinâmico da carteirinha, no estilo TOTP.
/// O servidor deriva uma chave por pessoa a partir da chave mestra; o app recebe essa chave
/// (por canal autenticado) e gera o código sozinho, inclusive offline.
/// Formato: <c>CO1.{pessoaId:N}.{passo}.{assinatura}</c>, em que o passo muda a cada 30 s.
/// </summary>
public static class QrCarteirinha
{
    public const string Prefixo = "CO1";
    public const int JanelaSegundos = 30;

    /// <summary>Quantos passos para trás/para frente são aceitos (tolerância de relógio): ~60 s de validade.</summary>
    public const int ToleranciaPassos = 1;

    public static byte[] DerivarChave(byte[] chaveMestra, Guid pessoaId, int versao) =>
        HMACSHA256.HashData(chaveMestra, Encoding.UTF8.GetBytes($"carteirinha:{pessoaId:N}:{versao}"));

    public static long PassoDe(DateTimeOffset instante) => instante.ToUnixTimeSeconds() / JanelaSegundos;

    /// <summary>Segundos até o código atual trocar.</summary>
    public static int SegundosRestantes(DateTimeOffset agora) =>
        JanelaSegundos - (int)(agora.ToUnixTimeSeconds() % JanelaSegundos);

    public static string Gerar(Guid pessoaId, byte[] chavePessoa, DateTimeOffset agora)
    {
        var passo = PassoDe(agora);
        return $"{Prefixo}.{pessoaId:N}.{passo}.{Assinar(chavePessoa, pessoaId, passo)}";
    }

    /// <summary>Lê o token sem validar a assinatura (para descobrir de quem é).</summary>
    public static bool TentarLer(string? token, out LeituraQr leitura)
    {
        leitura = default!;
        var partes = (token ?? string.Empty).Trim().Split('.');
        if (partes.Length != 4 || partes[0] != Prefixo
            || !Guid.TryParseExact(partes[1], "N", out var pessoaId)
            || !long.TryParse(partes[2], out var passo))
            return false;

        leitura = new LeituraQr(pessoaId, passo);
        return true;
    }

    public static FalhaQr Validar(string? token, byte[] chavePessoa, DateTimeOffset agora)
    {
        if (!TentarLer(token, out var leitura))
            return FalhaQr.FormatoInvalido;

        var esperado = Encoding.ASCII.GetBytes(Assinar(chavePessoa, leitura.PessoaId, leitura.Passo));
        var recebido = Encoding.ASCII.GetBytes(token!.Trim().Split('.')[3]);
        if (!CryptographicOperations.FixedTimeEquals(esperado, recebido))
            return FalhaQr.AssinaturaInvalida;

        return Math.Abs(PassoDe(agora) - leitura.Passo) > ToleranciaPassos ? FalhaQr.Expirado : FalhaQr.Nenhuma;
    }

    private static string Assinar(byte[] chavePessoa, Guid pessoaId, long passo)
    {
        var mac = HMACSHA256.HashData(chavePessoa, Encoding.UTF8.GetBytes($"{pessoaId:N}.{passo}"));
        // 16 bytes (128 bits) bastam e deixam o QR menor.
        return Convert.ToBase64String(mac, 0, 16).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
