namespace ClubeDaOnca.Api.Infrastructure;

/// <summary>
/// Logo do clube (pasta Marca/, copiada para a saída do build), usada nos documentos, e-mails,
/// páginas públicas e posts. Sem o arquivo, cada lugar segue sem a imagem.
/// </summary>
public static class Marca
{
    /// <summary>Content-ID da logo embutida nos e-mails (use <c>src="cid:logo"</c> no HTML).</summary>
    public const string CidEmail = "logo";

    /// <summary>Tag &lt;img&gt; da logo para o topo dos e-mails.</summary>
    public const string ImgEmail =
        """<p style="text-align:center;margin:0 0 8px"><img src="cid:logo" alt="Clube da Onça" height="110" style="height:110px"></p>""";

    /// <summary>Logo em PNG com fundo transparente (512 px de altura).</summary>
    public static byte[]? Logo => _logo.Value;

    /// <summary>Versão leve para e-mail (160 px de altura).</summary>
    public static byte[]? LogoEmail => _logoEmail.Value;

    private static readonly Lazy<byte[]?> _logo = new(() => Ler("logo.png"));
    private static readonly Lazy<byte[]?> _logoEmail = new(() => Ler("logo_email.png"));

    private static byte[]? Ler(string arquivo)
    {
        var caminho = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Marca", arquivo),
            Path.Combine(Directory.GetCurrentDirectory(), "Marca", arquivo),
        }.FirstOrDefault(File.Exists);
        return caminho is null ? null : File.ReadAllBytes(caminho);
    }
}
