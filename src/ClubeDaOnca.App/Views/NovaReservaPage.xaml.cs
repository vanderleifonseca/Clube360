using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class NovaReservaPage : ContentPage
{
    private readonly NovaReservaViewModel _vm;

    public NovaReservaPage(NovaReservaViewModel vm)
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
