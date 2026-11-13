using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class SociosPage : ContentPage
{
    private readonly SociosViewModel _vm;

    public SociosPage(SociosViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Carrega pelo OnAppearing: o EventToCommandBehavior do evento Appearing não dispara o comando.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.BuscarCommand.ExecuteAsync(null);
    }
}
