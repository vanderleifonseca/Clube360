using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

/// <summary>Troca obrigatória da senha provisória no primeiro acesso.</summary>
public partial class TrocarSenhaViewModel(SessaoService sessao, IClubeApi api) : ObservableObject
{
    [ObservableProperty]
    public partial string SenhaAtual { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NovaSenha { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Confirmacao { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Erro { get; set; }

    [ObservableProperty]
    public partial bool Ocupado { get; set; }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Erro = null;
        if (string.IsNullOrWhiteSpace(SenhaAtual) || string.IsNullOrWhiteSpace(NovaSenha))
        {
            Erro = "Preencha a senha atual e a nova senha.";
            return;
        }
        if (NovaSenha != Confirmacao)
        {
            Erro = "A confirmação não confere com a nova senha.";
            return;
        }

        Ocupado = true;
        try
        {
            await api.AlterarSenhaAsync(new AlterarSenhaRequest(SenhaAtual, NovaSenha));
            if (sessao.Usuario is { } u)
                await sessao.AtualizarUsuarioAsync(u with { DeveTrocarSenha = false });

            SenhaAtual = NovaSenha = Confirmacao = string.Empty;
            await Shell.Current.GoToAsync("//main/inicio");
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private Task SairAsync() => sessao.SairAsync();
}
