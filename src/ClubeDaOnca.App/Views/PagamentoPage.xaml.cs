using ClubeDaOnca.App.ViewModels;

namespace ClubeDaOnca.App.Views;

public partial class PagamentoPage : ContentPage
{
    public PagamentoPage(PagamentoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}