using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class ConfiguracaoFinanceiraPage : ContentPage
{
    private readonly ConfiguracaoFinanceiraViewModel _vm;

    public ConfiguracaoFinanceiraPage(ConfiguracaoFinanceiraViewModel vm)
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
