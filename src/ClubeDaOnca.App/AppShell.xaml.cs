using ClubeDaOnca.App.Services;
using ClubeDaOnca.App.Views;

namespace ClubeDaOnca.App;

public partial class AppShell : Shell
{
    private readonly SessaoService _sessao;

    public AppShell(SessaoService sessao)
    {
        InitializeComponent();
        _sessao = sessao;
        _sessao.SessaoEncerrada += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await GoToAsync("//login"));

        // Telas empilhadas sobre as abas (navegação com "voltar").
        Routing.RegisterRoute("socios", typeof(SociosPage));
        Routing.RegisterRoute("socio", typeof(SocioPage));
        Routing.RegisterRoute("dependente", typeof(DependentePage));
        Routing.RegisterRoute("portaria", typeof(PortariaPage));
        Routing.RegisterRoute("nova-reserva", typeof(NovaReservaPage));
        Routing.RegisterRoute("gestao-reservas", typeof(GestaoReservasPage));
        Routing.RegisterRoute("espacos", typeof(EspacosPage));
        Routing.RegisterRoute("espaco", typeof(EspacoPage));
        Routing.RegisterRoute("pagamento", typeof(PagamentoPage));
        Routing.RegisterRoute("gestao-financeiro", typeof(GestaoFinanceiroPage));
        Routing.RegisterRoute("inadimplentes", typeof(InadimplentesPage));
        Routing.RegisterRoute("lancar-cobranca", typeof(LancarCobrancaPage));
        Routing.RegisterRoute("config-financeiro", typeof(ConfiguracaoFinanceiraPage));
        Routing.RegisterRoute("associacoes", typeof(AssociacoesPage));
        Routing.RegisterRoute("associacao", typeof(AssociacaoPage));
        Routing.RegisterRoute("contrato", typeof(ContratoPage));
        Routing.RegisterRoute("fichas", typeof(FichasPage));
        Routing.RegisterRoute("eventos-gestao", typeof(EventosPage));
        Routing.RegisterRoute("evento", typeof(EventoPage));
        Routing.RegisterRoute("evento-detalhe", typeof(EventoDetalhePage));
        Routing.RegisterRoute("avisos", typeof(AvisosPage));
        Routing.RegisterRoute("comunicados", typeof(ComunicadosPage));
    }

    /// <summary>Rota inicial depois do login: troca de senha provisória ou abas principais.</summary>
    public static string RotaInicial(SessaoService sessao) =>
        sessao.DeveTrocarSenha ? "//trocar-senha" : "//main/inicio";

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        // Abas aparecem conforme os perfis do usuário logado.
        CarteirinhaTab.IsVisible = _sessao.TemCarteirinha;
        ReservasTab.IsVisible = _sessao.EhTitular;
        EventosTab.IsVisible = _sessao.EhTitular;
        FinanceiroTab.IsVisible = _sessao.EhTitular;
        GestaoTab.IsVisible = _sessao.EhGestao;

        // Logo do clube no cabeçalho de todas as telas (as que já têm cabeçalho próprio ficam como estão).
        if (CurrentPage is { } pagina && GetTitleView(pagina) is null && GetNavBarIsVisible(pagina))
            SetTitleView(pagina, CabecalhoComLogo(pagina));
    }

    private View CabecalhoComLogo(Page pagina)
    {
        var titulo = new Label { FontSize = 20, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
        titulo.SetBinding(Label.TextProperty, new Binding(nameof(Page.Title), source: pagina));
        titulo.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
        return new HorizontalStackLayout
        {
            Spacing = 10,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Image { Source = "logo.png", HeightRequest = 40, WidthRequest = 34, Aspect = Aspect.AspectFit, VerticalOptions = LayoutOptions.Center },
                titulo,
            },
        };
    }

    protected override async void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is not null && await _sessao.RestaurarAsync())
            await GoToAsync(RotaInicial(_sessao));
    }
}
