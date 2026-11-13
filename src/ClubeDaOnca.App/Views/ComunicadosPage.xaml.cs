using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class ComunicadosPage : ContentPage
{
    private readonly ComunicadosViewModel _vm;

    public ComunicadosPage(ComunicadosViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Carrega pelo OnAppearing: o EventToCommandBehavior do evento Appearing não dispara o comando.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.CarregarCommand.ExecuteAsync(null);
    }
}
