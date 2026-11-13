using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class AssociacaoPage : ContentPage
{
    private readonly AssociacaoViewModel _vm;

    public AssociacaoPage(AssociacaoViewModel vm)
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
