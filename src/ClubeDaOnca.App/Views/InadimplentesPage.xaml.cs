using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class InadimplentesPage : ContentPage
{
    private readonly InadimplentesViewModel _vm;

    public InadimplentesPage(InadimplentesViewModel vm)
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
