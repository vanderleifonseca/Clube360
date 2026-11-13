using System.Net;
using ClubeDaOnca.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

public partial class LoginViewModel(SessaoService sessao) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EntrarCommand))]
    public partial string Login { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EntrarCommand))]
    public partial string Senha { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Erro { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EntrarCommand))]
    public partial bool Ocupado { get; set; }

    /// <summary>Endereço da API (campo visível só no build de teste, para usar o celular pelo Wi-Fi).</summary>
    [ObservableProperty]
    public partial string Servidor { get; set; } = ApiConfig.BaseUrl;

    public bool PodeTrocarServidor => ApiConfig.PodeTrocarServidor;

    private bool PodeEntrar() => !Ocupado && !string.IsNullOrWhiteSpace(Login) && !string.IsNullOrWhiteSpace(Senha);

    [RelayCommand(CanExecute = nameof(PodeEntrar))]
    private async Task EntrarAsync()
    {
        Erro = null;
        if (PodeTrocarServidor)
        {
            try { Servidor = ApiConfig.DefinirServidor(Servidor); }
            catch (FormatException ex) { Erro = ex.Message; return; }
        }

        Ocupado = true;
        try
        {
            await sessao.EntrarAsync(Login, Senha);
            Senha = string.Empty;
            await Shell.Current.GoToAsync(AppShell.RotaInicial(sessao));
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Erro = "CPF/e-mail ou senha inválidos.";
        }
        catch (HttpRequestException)
        {
            Erro = PodeTrocarServidor
                ? $"Não foi possível conectar a {Servidor}. Confira o endereço do servidor e se o celular está no mesmo Wi-Fi do PC."
                : "Não foi possível conectar ao servidor. Verifique sua internet.";
        }
        catch (TaskCanceledException)
        {
            Erro = $"O servidor {Servidor} não respondeu. Confira o endereço e a rede Wi-Fi.";
        }
        finally
        {
            Ocupado = false;
        }
    }
}
