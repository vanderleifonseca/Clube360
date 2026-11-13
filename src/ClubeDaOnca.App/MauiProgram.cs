using ClubeDaOnca.App.Services;
using ClubeDaOnca.App.ViewModels;
using ClubeDaOnca.App.Views;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Refit;
using ZXing.Net.Maui.Controls;

namespace ClubeDaOnca.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var baseUri = new Uri(ApiConfig.BaseUrl);

        // Redireciona para o servidor escolhido na tela de login (build de teste).
        builder.Services.AddTransient<ServidorHandler>();

        // API pública (login/refresh): sem handler de autenticação, para não entrar em loop.
        builder.Services.AddRefitGeneratedClient<IAuthApi>()
            .ConfigureHttpClient(c => { c.BaseAddress = baseUri; c.Timeout = TimeSpan.FromSeconds(20); })
            .AddHttpMessageHandler<ServidorHandler>();

        // API autenticada: o handler injeta o Bearer token e renova quando expira.
        builder.Services.AddTransient<AuthHeaderHandler>();
        builder.Services.AddRefitGeneratedClient<IClubeApi>()
            .ConfigureHttpClient(c => { c.BaseAddress = baseUri; c.Timeout = TimeSpan.FromSeconds(20); })
            .AddHttpMessageHandler<ServidorHandler>()
            .AddHttpMessageHandler<AuthHeaderHandler>();

        builder.Services.AddSingleton<SessaoService>();
        builder.Services.AddSingleton<AppShell>();

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<InicioViewModel>();
        builder.Services.AddTransient<InicioPage>();
        builder.Services.AddTransient<GestaoPage>();

        builder.Services.AddSingleton<CarteirinhaService>();
        builder.Services.AddTransient<TrocarSenhaViewModel>();
        builder.Services.AddTransient<TrocarSenhaPage>();
        builder.Services.AddTransient<CarteirinhaViewModel>();
        builder.Services.AddTransient<CarteirinhaPage>();
        builder.Services.AddTransient<SociosViewModel>();
        builder.Services.AddTransient<SociosPage>();
        builder.Services.AddTransient<SocioViewModel>();
        builder.Services.AddTransient<SocioPage>();
        builder.Services.AddTransient<DependenteViewModel>();
        builder.Services.AddTransient<DependentePage>();
        builder.Services.AddTransient<PortariaViewModel>();
        builder.Services.AddTransient<PortariaPage>();
        builder.Services.AddTransient<ReservasViewModel>();
        builder.Services.AddTransient<ReservasPage>();
        builder.Services.AddTransient<NovaReservaViewModel>();
        builder.Services.AddTransient<NovaReservaPage>();
        builder.Services.AddTransient<GestaoReservasViewModel>();
        builder.Services.AddTransient<GestaoReservasPage>();
        builder.Services.AddTransient<EspacosViewModel>();
        builder.Services.AddTransient<EspacosPage>();
        builder.Services.AddTransient<EspacoViewModel>();
        builder.Services.AddTransient<EspacoPage>();
        builder.Services.AddTransient<FinanceiroViewModel>();
        builder.Services.AddTransient<FinanceiroPage>();
        builder.Services.AddTransient<PagamentoViewModel>();
        builder.Services.AddTransient<PagamentoPage>();
        builder.Services.AddTransient<GestaoFinanceiroViewModel>();
        builder.Services.AddTransient<GestaoFinanceiroPage>();
        builder.Services.AddTransient<LancarCobrancaViewModel>();
        builder.Services.AddTransient<LancarCobrancaPage>();
        builder.Services.AddTransient<InadimplentesViewModel>();
        builder.Services.AddTransient<InadimplentesPage>();
        builder.Services.AddTransient<ConfiguracaoFinanceiraViewModel>();
        builder.Services.AddTransient<ConfiguracaoFinanceiraPage>();
        builder.Services.AddTransient<AssociacoesViewModel>();
        builder.Services.AddTransient<AssociacoesPage>();
        builder.Services.AddTransient<AssociacaoViewModel>();
        builder.Services.AddTransient<AssociacaoPage>();
        builder.Services.AddTransient<ContratoViewModel>();
        builder.Services.AddTransient<ContratoPage>();
        builder.Services.AddTransient<FichasViewModel>();
        builder.Services.AddTransient<FichasPage>();
        builder.Services.AddTransient<EventosViewModel>();
        builder.Services.AddTransient<EventosPage>();
        builder.Services.AddTransient<EventoDetalheViewModel>();
        builder.Services.AddTransient<EventoDetalhePage>();
        builder.Services.AddTransient<EventoViewModel>();
        builder.Services.AddTransient<EventoPage>();
        builder.Services.AddTransient<AvisosViewModel>();
        builder.Services.AddTransient<AvisosPage>();
        builder.Services.AddTransient<ComunicadosViewModel>();
        builder.Services.AddTransient<ComunicadosPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
