using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class FinanceiroPage : ContentPage
{
    private readonly FinanceiroViewModel _vm;

    public FinanceiroPage(FinanceiroViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Carrega pelo OnAppearing: nas páginas raiz das abas do Shell, o EventToCommandBehavior não dispara o comando.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.CarregarCommand.ExecuteAsync(null);
    }
}
