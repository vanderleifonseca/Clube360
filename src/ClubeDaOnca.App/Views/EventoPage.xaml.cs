using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class EventoPage : ContentPage
{
    private readonly EventoViewModel _vm;

    public EventoPage(EventoViewModel vm)
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
