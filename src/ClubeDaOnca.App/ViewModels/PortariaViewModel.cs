using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Carteirinha;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

public sealed record AcessoLinha(string Nome, string Detalhe, bool Liberado)
{
    public string Icone => Liberado ? "✔" : "✖";

    public static AcessoLinha De(RegistroAcessoDto r) =>
        new(r.Nome ?? "Não identificado", $"{r.Em.ToLocalTime():dd/MM HH:mm:ss} · {r.Motivo}", r.Liberado);
}

/// <summary>
/// Modo portaria: recebe o texto do QR (leitor USB/Bluetooth funciona como teclado e envia Enter)
/// e mostra o resultado devolvido pela API. A validação é sempre feita no servidor.
/// </summary>
public partial class PortariaViewModel(IClubeApi api) : ObservableObject
{
    public ObservableCollection<AcessoLinha> Acessos { get; } = [];

    [ObservableProperty]
    public partial string Codigo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool TemResultado { get; set; }

    [ObservableProperty]
    public partial bool Liberado { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Color CorResultado { get; set; } = Colors.Gray;

    [ObservableProperty]
    public partial string NomePessoa { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Detalhe { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool Ocupado { get; set; }

    [ObservableProperty]
    public partial string? Erro { get; set; }

    /// <summary>Disparado após cada leitura, para a tela devolver o foco ao campo do leitor.</summary>
    public event EventHandler? LeituraConcluida;

    [RelayCommand]
    private async Task CarregarAsync()
    {
        try
        {
            var acessos = await api.AcessosAsync();
            Acessos.Clear();
            foreach (var a in acessos)
                Acessos.Add(AcessoLinha.De(a));
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
    }

    [RelayCommand]
    private async Task ValidarAsync()
    {
        var codigo = Codigo.Trim();
        if (codigo.Length == 0 || Ocupado) return;

        Erro = null;
        Ocupado = true;
        try
        {
            var r = await api.ValidarQrAsync(new ValidarQrRequest(codigo));
            TemResultado = true;
            Liberado = r.Liberado;
            Status = r.Liberado ? "ENTRADA LIBERADA" : "ENTRADA BLOQUEADA";
            // Verde: liberado · âmbar: liberado com mensalidade em atraso · vermelho: bloqueado.
            CorResultado = !r.Liberado ? Color.FromArgb("#B3261E") : r.Inadimplente ? Color.FromArgb("#B45309") : Color.FromArgb("#1B873F");
            NomePessoa = r.Nome ?? "Carteirinha não identificada";
            Detalhe = r.NumeroTitulo is null
                ? r.Motivo
                : $"{r.Tipo} · Título nº {r.NumeroTitulo} · {r.Situacao}" + (r.Liberado && !r.Inadimplente ? "" : $"\n{r.Motivo}");

            Acessos.Insert(0, new AcessoLinha(r.Nome ?? "Não identificado",
                $"{r.Em.ToLocalTime():dd/MM HH:mm:ss} · {r.Motivo}", r.Liberado));
            while (Acessos.Count > 30) Acessos.RemoveAt(Acessos.Count - 1);

            try { HapticFeedback.Default.Perform(r.Liberado ? HapticFeedbackType.Click : HapticFeedbackType.LongPress); }
            catch { /* aparelho sem vibração */ }
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
        finally
        {
            Codigo = string.Empty;
            Ocupado = false;
            LeituraConcluida?.Invoke(this, EventArgs.Empty);
        }
    }
}
