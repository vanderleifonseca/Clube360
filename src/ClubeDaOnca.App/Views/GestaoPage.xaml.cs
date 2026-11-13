using ClubeDaOnca.App.Services;

namespace ClubeDaOnca.App.Views;

public partial class GestaoPage : ContentPage
{
    private readonly SessaoService _sessao;

    public GestaoPage(SessaoService sessao)
    {
        InitializeComponent();
        _sessao = sessao;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Portaria só lê carteirinhas; o cadastro é da secretaria e do admin.
        CartaoSocios.IsVisible = _sessao.EhSecretaria;
        CartaoPortaria.IsVisible = _sessao.EhGestao;
        CartaoReservas.IsVisible = _sessao.EhSecretaria;
        CartaoEspacos.IsVisible = _sessao.EhSecretaria;
        CartaoFinanceiro.IsVisible = _sessao.EhSecretaria;
        CartaoEventos.IsVisible = _sessao.EhSecretaria;
        CartaoComunicados.IsVisible = _sessao.EhSecretaria;
        CartaoAssociacao.IsVisible = _sessao.EhAdmin;
        CartaoAssociacoes.IsVisible = _sessao.EhSuperAdmin;
    }

    private async void AbrirAssociacao(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("associacao", new ShellNavigationQueryParameters { ["atual"] = true });

    private async void AbrirAssociacoes(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("associacoes");

    private async void AbrirComunicados(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("comunicados");

    private async void AbrirEventos(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("eventos-gestao", new ShellNavigationQueryParameters { ["gestao"] = true });

    private async void AbrirFinanceiro(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("gestao-financeiro");

    private async void AbrirReservas(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("gestao-reservas");

    private async void AbrirEspacos(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("espacos");

    private async void AbrirSocios(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("socios");

    private async void AbrirPortaria(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("portaria");
}
