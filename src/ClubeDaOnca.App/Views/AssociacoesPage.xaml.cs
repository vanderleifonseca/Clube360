using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class AssociacoesPage : ContentPage
{
    private readonly AssociacoesViewModel _vm;

    public AssociacoesPage(AssociacoesViewModel vm)
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
