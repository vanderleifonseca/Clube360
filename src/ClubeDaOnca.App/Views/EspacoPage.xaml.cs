using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class EspacoPage : ContentPage
{
    public EspacoPage(EspacoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}