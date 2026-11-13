using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class CarteirinhaPage : ContentPage
{
    private readonly CarteirinhaViewModel _vm;
    private double _brilhoAnterior = -1;

    public CarteirinhaPage(CarteirinhaViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.Iniciar();
        AumentarBrilho();
        await _vm.CarregarCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Parar();
        RestaurarBrilho();
    }

    // Brilho máximo facilita a leitura do QR pelo leitor da portaria.
    private void AumentarBrilho()
    {
#if ANDROID
        var janela = Platform.CurrentActivity?.Window;
        if (janela?.Attributes is { } attrs)
        {
            _brilhoAnterior = attrs.ScreenBrightness;
            attrs.ScreenBrightness = 1f;
            janela.Attributes = attrs;
        }
#endif
    }

    private void RestaurarBrilho()
    {
#if ANDROID
        var janela = Platform.CurrentActivity?.Window;
        if (_brilhoAnterior >= -1 && janela?.Attributes is { } attrs)
        {
            attrs.ScreenBrightness = (float)_brilhoAnterior;
            janela.Attributes = attrs;
        }
#endif
    }
}
