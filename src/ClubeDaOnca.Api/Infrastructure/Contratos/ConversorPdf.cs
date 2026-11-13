using System.Diagnostics;

namespace ClubeDaOnca.Api.Infrastructure.Contratos;

/// <summary>
/// Converte o .docx do contrato em PDF com o LibreOffice (modo headless). Sem LibreOffice ou se a conversão
/// falhar, devolve nulo e o sistema usa o próprio .docx. "Contratos:LibreOffice" = caminho do soffice
/// ou "desligado".
/// </summary>
public sealed class ConversorPdf(IConfiguration config, ILogger<ConversorPdf> log)
{
    // O LibreOffice usa um perfil de usuário: criar um novo a cada conversão leva dezenas de segundos.
    // Reaproveita um perfil fixo e converte um documento por vez (o perfil não aceita uso simultâneo).
    private static readonly SemaphoreSlim Fila = new(1, 1);
    private static readonly string Perfil = Path.Combine(Path.GetTempPath(), "clube-da-onca-libreoffice");

    private static readonly string[] Candidatos =
    [
        @"C:\Program Files\LibreOffice\program\soffice.exe",
        @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
        "/usr/bin/soffice",
        "/usr/bin/libreoffice",
        "/usr/lib/libreoffice/program/soffice",
        "/opt/libreoffice/program/soffice",
        "/Applications/LibreOffice.app/Contents/MacOS/soffice",
    ];

    public string? Executavel
    {
        get
        {
            var configurado = config["Contratos:LibreOffice"];
            if (string.Equals(configurado, "desligado", StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.IsNullOrWhiteSpace(configurado)) return File.Exists(configurado) ? configurado : null;
            return Candidatos.FirstOrDefault(File.Exists);
        }
    }

    public async Task<byte[]?> ConverterAsync(byte[] docx, CancellationToken ct = default)
    {
        var exe = Executavel;
        if (exe is null) return null;

        var pasta = Path.Combine(Path.GetTempPath(), "clube-contrato-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        await Fila.WaitAsync(ct);
        try
        {
            var entrada = Path.Combine(pasta, "contrato.docx");
            await File.WriteAllBytesAsync(entrada, docx, ct);

            var perfil = new Uri(Perfil).AbsoluteUri;
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in new[] { $"-env:UserInstallation={perfil}", "--headless", "--norestore", "--convert-to", "pdf", "--outdir", pasta, entrada })
                psi.ArgumentList.Add(arg);

            using var processo = Process.Start(psi)!;
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(60));
            try
            {
                await processo.WaitForExitAsync(limite.Token);
            }
            catch (OperationCanceledException)
            {
                processo.Kill(entireProcessTree: true);
                log.LogWarning("Conversão do contrato para PDF excedeu o tempo; será usado o .docx.");
                return null;
            }

            var saida = Path.Combine(pasta, "contrato.pdf");
            if (File.Exists(saida)) return await File.ReadAllBytesAsync(saida, ct);

            log.LogWarning("LibreOffice não gerou o PDF (código {Codigo}): {Erro}", processo.ExitCode, await processo.StandardError.ReadToEndAsync(ct));
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Falha ao converter o contrato para PDF; será usado o .docx.");
            return null;
        }
        finally
        {
            Fila.Release();
            try { Directory.Delete(pasta, recursive: true); } catch { /* arquivo temporário: ignora */ }
        }
    }
}
