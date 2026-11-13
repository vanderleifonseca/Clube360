using QRCoder;

namespace ClubeDaOnca.App.Services;

public static class QrImagem
{
    public static byte[] Png(string texto, int pixelsPorModulo = 12)
    {
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(dados).GetGraphic(pixelsPorModulo);
    }

    public static ImageSource Fonte(byte[] png) => ImageSource.FromStream(() => new MemoryStream(png));

    public static ImageSource Gerar(string texto) => Fonte(Png(texto));
}
