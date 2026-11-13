using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class LancarCobrancaPage : ContentPage
{
    public LancarCobrancaPage(LancarCobrancaViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
