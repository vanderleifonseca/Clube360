using System.Globalization;
using ClubeDaOnca.Domain.Eventos;
using SkiaSharp;

namespace ClubeDaOnca.Api.Infrastructure.Comunicacao;

/// <summary>
/// Post pronto para Instagram/Facebook/WhatsApp de um evento aberto ao público:
/// imagem quadrada 1080×1080 (PNG) com título, data e local, e o texto da legenda.
/// </summary>
public static class GeradorPost
{
    private static readonly CultureInfo PtBr = new("pt-BR");
    private static readonly SKColor Laranja = SKColor.Parse("#B45309");
    private static readonly SKColor Escuro = SKColor.Parse("#7A3E0B");
    private static readonly SKColor Creme = SKColor.Parse("#FFF8EC");

    public static string Texto(Evento e, string associacao)
    {
        var locais = string.Join(", ", e.Locais.Select(l => l.Espaco?.Nome).Where(n => n is not null));
        var linhas = new List<string>
        {
            $"🎉 {e.Titulo}",
            "",
            $"📅 {char.ToUpper(DivulgacaoService.Quando(e)[0])}{DivulgacaoService.Quando(e)[1..]}",
        };
        if (locais.Length > 0) linhas.Add($"📍 {associacao} · {locais}");
        if (!string.IsNullOrWhiteSpace(e.Descricao)) { linhas.Add(""); linhas.Add(e.Descricao.Trim()); }
        linhas.Add("");
        linhas.Add("Aberto ao público! Associados reservam pelo app do clube. 🐆");
        linhas.Add("");
        linhas.Add($"#{Hashtag(associacao)} #eventos #lazer");
        return string.Join('\n', linhas);
    }

    public static byte[] Imagem(Evento e, string associacao)
    {
        const int lado = 1080;
        using var surface = SKSurface.Create(new SKImageInfo(lado, lado));
        var c = surface.Canvas;

        using (var fundo = new SKPaint())
        {
            fundo.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(lado, lado),
                [Laranja, Escuro], SKShaderTileMode.Clamp);
            c.DrawRect(0, 0, lado, lado, fundo);
        }
        using (var faixa = new SKPaint { Color = Creme.WithAlpha(40), IsAntialias = true })
            c.DrawCircle(lado * 0.92f, lado * 0.08f, 260, faixa);

        // Logo do clube no canto inferior direito (o título ocupa a largura toda no alto).
        if (Marca.Logo is { } png)
        {
            using var logo = SKBitmap.Decode(png);
            if (logo is not null)
            {
                const float altura = 220;
                var largura = altura * logo.Width / logo.Height;
                using var imagemLogo = SKImage.FromBitmap(logo);
                c.DrawImage(imagemLogo, SKRect.Create(lado - 60 - largura, lado - 50 - altura, largura, altura),
                    new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            }
        }

        var tipo = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default;
        var normal = SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;
        using var branco = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var creme = new SKPaint { Color = Creme, IsAntialias = true };

        float y = 150;
        using (var fonte = new SKFont(normal, 40))
            c.DrawText(associacao.ToUpper(PtBr), 80, y, SKTextAlign.Left, fonte, creme);

        y += 120;
        using (var fonte = new SKFont(tipo, 92))
            foreach (var linha in Quebrar(e.Titulo, fonte, lado - 160).Take(4))
            {
                c.DrawText(linha, 80, y, SKTextAlign.Left, fonte, branco);
                y += 108;
            }

        y += 40;
        using (var linhaPaint = new SKPaint { Color = Creme, StrokeWidth = 6, IsAntialias = true })
            c.DrawLine(80, y, 260, y, linhaPaint);
        y += 90;

        using (var fonte = new SKFont(normal, 48))
        {
            var quando = DivulgacaoService.Quando(e);
            foreach (var linha in Quebrar(char.ToUpper(quando[0]) + quando[1..], fonte, lado - 400).Take(2))
            {
                c.DrawText(linha, 80, y, SKTextAlign.Left, fonte, branco);
                y += 64;
            }
            var locais = string.Join(", ", e.Locais.Select(l => l.Espaco?.Nome).Where(n => n is not null));
            if (locais.Length > 0)
                foreach (var linha in Quebrar(locais, fonte, lado - 400).Take(2))
                {
                    c.DrawText(linha, 80, y, SKTextAlign.Left, fonte, creme);
                    y += 64;
                }
        }

        using (var fonte = new SKFont(tipo, 44))
            c.DrawText("ABERTO AO PÚBLICO", 80, lado - 100, SKTextAlign.Left, fonte, creme);

        using var imagem = surface.Snapshot();
        using var dados = imagem.Encode(SKEncodedImageFormat.Png, 100);
        return dados.ToArray();
    }

    private static IEnumerable<string> Quebrar(string texto, SKFont fonte, float largura)
    {
        var atual = string.Empty;
        foreach (var palavra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var tentativa = atual.Length == 0 ? palavra : $"{atual} {palavra}";
            if (fonte.MeasureText(tentativa) <= largura || atual.Length == 0) atual = tentativa;
            else
            {
                yield return atual;
                atual = palavra;
            }
        }
        if (atual.Length > 0) yield return atual;
    }

    private static string Hashtag(string texto) =>
        new(texto.Normalize(System.Text.NormalizationForm.FormD)
            .Where(ch => char.IsLetterOrDigit(ch) && CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            .ToArray());
}
