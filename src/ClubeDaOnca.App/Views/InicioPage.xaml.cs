using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class InicioPage : ContentPage
{
    private readonly InicioViewModel _vm;

    public InicioPage(InicioViewModel vm)
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.PararVerificacaoCommand.Execute(null);
    }
}
