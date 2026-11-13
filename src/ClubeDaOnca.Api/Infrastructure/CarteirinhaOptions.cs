using System.Text;
using ClubeDaOnca.Domain.Carteirinha;

namespace ClubeDaOnca.Api.Infrastructure;

public sealed class CarteirinhaOptions
{
    public const string Secao = "Carteirinha";

    /// <summary>
    /// Chave mestra (32+ caracteres) da qual saem as chaves do QR de cada pessoa.
    /// Em produção vem de variável de ambiente (Carteirinha__ChaveMestra). Trocá-la invalida todos os QR.
    /// </summary>
    public string ChaveMestra { get; set; } = string.Empty;
}

/// <summary>Deriva as chaves de QR por pessoa a partir da chave mestra.</summary>
public sealed class ChavesCarteirinha(byte[] chaveMestra)
{
    public static ChavesCarteirinha Criar(CarteirinhaOptions opt)
    {
        if (opt.ChaveMestra.Length < 32)
            throw new InvalidOperationException("Configure Carteirinha:ChaveMestra com pelo menos 32 caracteres.");
        return new ChavesCarteirinha(Encoding.UTF8.GetBytes(opt.ChaveMestra));
    }

    public byte[] Para(Guid pessoaId, int versao) => QrCarteirinha.DerivarChave(chaveMestra, pessoaId, versao);
}
