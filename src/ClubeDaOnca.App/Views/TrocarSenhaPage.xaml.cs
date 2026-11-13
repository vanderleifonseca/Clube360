using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class TrocarSenhaPage : ContentPage
{
    public TrocarSenhaPage(TrocarSenhaViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}