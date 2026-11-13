using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class DependentePage : ContentPage
{
    public DependentePage(DependenteViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}