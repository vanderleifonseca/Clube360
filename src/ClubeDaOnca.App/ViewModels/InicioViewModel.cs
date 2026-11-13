using ClubeDaOnca.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

public partial class InicioViewModel(SessaoService sessao, IClubeApi api) : ObservableObject
{
    [ObservableProperty]
    public partial string Saudacao { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Perfis { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool EhPresidente { get; set; }

    [ObservableProperty]
    public partial string AvisoPresidencia { get; set; } = string.Empty;

    [RelayCommand]
    private Task AbrirFichasAsync() => Shell.Current.GoToAsync("fichas");

    [ObservableProperty]
    public partial bool EhSocio { get; set; }

    [ObservableProperty]
    public partial string TextoAvisos { get; set; } = "Avisos";

    [RelayCommand]
    private Task AbrirAvisosAsync() => Shell.Current.GoToAsync("avisos");

    [RelayCommand]
    private async Task CarregarAsync()
    {
        MostrarUsuario(sessao.Usuario);
        try
        {
            // Confere com o servidor (valida o token e atualiza os perfis).
            var me = await api.MeAsync();
            await sessao.AtualizarUsuarioAsync(me);
            MostrarUsuario(me);

            EhSocio = sessao.EhTitular;
            if (EhSocio)
            {
                var avisos = await api.ResumoAvisosAsync();
                TextoAvisos = avisos.NaoLidos switch
                {
                    0 => "🔔  Avisos",
                    1 => "🔔  Avisos · 1 novo",
                    var n => $"🔔  Avisos · {n} novos",
                };
            }

            await AtualizarPresidenciaAsync();
            if (EhPresidente) IniciarVerificacao();
        }
        catch (Exception ex) when (ex is HttpRequestException or Refit.ApiException)
        {
            // Offline ou sem associação: mantém os dados em cache.
        }
    }

    // ---------- Aviso de nova adesão para o presidente ----------

    private static readonly TimeSpan IntervaloVerificacao = TimeSpan.FromMinutes(1);
    private IDispatcherTimer? _timer;
    private bool _mostrandoAviso;

    private async Task AtualizarPresidenciaAsync()
    {
        var presidencia = await api.PresidenciaResumoAsync();
        EhPresidente = presidencia.EhPresidente;
        AvisoPresidencia = presidencia.Pendentes switch
        {
            0 => "Nenhuma ficha aguardando o seu aval.",
            1 => "1 ficha aguardando o seu aval.",
            var n => $"{n} fichas aguardando o seu aval.",
        };
        if (presidencia.EhPresidente && presidencia.Pendentes > 0 && presidencia.UltimoAceiteEm is { } ultimo)
            await AvisarNovaAdesaoAsync(presidencia.Pendentes, ultimo);
    }

    /// <summary>Mostra o popup quando chegou ficha nova desde o último aviso visto por este usuário.</summary>
    private async Task AvisarNovaAdesaoAsync(int pendentes, DateTimeOffset ultimoAceite)
    {
        if (_mostrandoAviso || sessao.Usuario is not { } usuario) return;
        var chave = $"presidencia_ultimo_aviso_{usuario.Id}";
        var visto = Preferences.Default.Get(chave, 0L);
        if (ultimoAceite.UtcTicks <= visto) return;

        _mostrandoAviso = true;
        try
        {
            // Grava antes de mostrar: o mesmo aviso não reaparece, mesmo se o app for fechado com o popup aberto.
            Preferences.Default.Set(chave, ultimoAceite.UtcTicks);
            var texto = pendentes == 1
                ? "Uma nova adesão foi aceita pelo candidato e aguarda o seu aval."
                : $"Chegou uma nova adesão. Há {pendentes} fichas aguardando o seu aval.";
            if (await Shell.Current.DisplayAlertAsync("Nova adesão", texto, "Ver fichas", "Depois"))
                await Shell.Current.GoToAsync("fichas");
        }
        finally
        {
            _mostrandoAviso = false;
        }
    }

    private void IniciarVerificacao()
    {
        if (_timer is not null) return;
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = IntervaloVerificacao;
        _timer.Tick += async (_, _) =>
        {
            try { await AtualizarPresidenciaAsync(); }
            catch (Exception ex) when (ex is HttpRequestException or Refit.ApiException) { }
        };
        _timer.Start();
    }

    [RelayCommand]
    private void PararVerificacao()
    {
        _timer?.Stop();
        _timer = null;
    }

    [RelayCommand]
    private Task SairAsync()
    {
        PararVerificacao();
        return sessao.SairAsync();
    }

    private void MostrarUsuario(Contracts.Auth.UsuarioDto? u)
    {
        if (u is null) return;
        Saudacao = $"Olá, {u.Nome.Split(' ')[0]}!";
        Perfis = string.Join(" · ", new[] { u.Associacao }.Concat(u.Perfis).Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
