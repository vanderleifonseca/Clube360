namespace ClubeDaOnca.App.Services;

/// <summary>Salva um documento baixado da API (PDF ou DOCX) e abre com o leitor padrão do aparelho.</summary>
public static class DocumentoService
{
    public static async Task AbrirAsync(HttpResponseMessage resposta, string nomeBase)
    {
        resposta.EnsureSuccessStatusCode();
        var tipo = resposta.Content.Headers.ContentType?.MediaType ?? "application/pdf";
        var extensao = tipo == "application/pdf" ? "pdf" : "docx";
        var arquivo = Path.Combine(FileSystem.CacheDirectory, $"{nomeBase}.{extensao}");
        await File.WriteAllBytesAsync(arquivo, await resposta.Content.ReadAsByteArrayAsync());
        await Launcher.Default.OpenAsync(new OpenFileRequest(nomeBase, new ReadOnlyFile(arquivo, tipo)));
    }
}
