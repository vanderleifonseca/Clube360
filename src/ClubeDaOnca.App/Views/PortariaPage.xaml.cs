using ClubeDaOnca.App.ViewModels;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace ClubeDaOnca.App.Views;

public partial class PortariaPage : ContentPage
{
    private readonly PortariaViewModel _vm;
    private CameraBarcodeReaderView? _camera;

    // A câmera detecta o mesmo QR várias vezes por segundo: evita validar o mesmo código em sequência.
    private string? _ultimoCodigo;
    private DateTime _ultimaLeitura;

    public PortariaPage(PortariaViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        // Depois de cada leitura o foco volta ao campo, pronto para o próximo sócio.
        _vm.LeituraConcluida += (_, _) => Dispatcher.Dispatch(() =>
        {
            if (_camera is null) CampoCodigo.Focus();
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        CampoCodigo.Focus();
        await _vm.CarregarCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DesligarCamera();
    }

    private async void AlternarCamera(object? sender, EventArgs e)
    {
        if (_camera is not null)
        {
            DesligarCamera();
            return;
        }

        if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
        {
            await DisplayAlertAsync("Câmera", "Permita o uso da câmera nas configurações do aparelho para ler os QR Codes.", "OK");
            return;
        }

        try
        {
            _camera = new CameraBarcodeReaderView
            {
                Options = new BarcodeReaderOptions
                {
                    Formats = BarcodeFormat.QrCode,
                    AutoRotate = true,
                    Multiple = false,
                },
                CameraLocation = CameraLocation.Rear,
            };
            _camera.BarcodesDetected += AoDetectar;
            AreaCamera.Content = _camera;
            AreaCamera.IsVisible = true;
            BotaoCamera.Text = "Fechar câmera";
        }
        catch (Exception)
        {
            DesligarCamera();
            await DisplayAlertAsync("Câmera", "Não foi possível abrir a câmera neste aparelho. Use um leitor USB ou cole o código.", "OK");
        }
    }

    private void DesligarCamera()
    {
        if (_camera is not null)
        {
            _camera.IsDetecting = false;
            _camera.BarcodesDetected -= AoDetectar;
            _camera.Handler?.DisconnectHandler();
            _camera = null;
        }
        AreaCamera.Content = null;
        AreaCamera.IsVisible = false;
        BotaoCamera.Text = "📷 Câmera";
    }

    // Chamado fora da thread de UI.
    private void AoDetectar(object? sender, BarcodeDetectionEventArgs e)
    {
        var codigo = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(codigo)) return;
        if (codigo == _ultimoCodigo && DateTime.UtcNow - _ultimaLeitura < TimeSpan.FromSeconds(5)) return;

        _ultimoCodigo = codigo;
        _ultimaLeitura = DateTime.UtcNow;

        Dispatcher.Dispatch(async () =>
        {
            if (_camera is not null) _camera.IsDetecting = false;
            _vm.Codigo = codigo;
            await _vm.ValidarCommand.ExecuteAsync(null);

            // Pausa curta para o porteiro ver o resultado antes da próxima leitura.
            await Task.Delay(1500);
            if (_camera is not null) _camera.IsDetecting = true;
        });
    }
}
