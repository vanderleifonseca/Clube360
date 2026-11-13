using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Financeiro;
using ClubeDaOnca.Contracts.Socios;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

/// <summary>Cobrança avulsa (multa, consumo, conserto etc.) lançada pelo financeiro para um sócio.</summary>
public partial class LancarCobrancaViewModel(IClubeApi api) : ObservableObject
{
    public ObservableCollection<SocioResumoDto> Resultados { get; } = [];

    [ObservableProperty] public partial string Busca { get; set; } = string.Empty;
    [ObservableProperty] public partial SocioResumoDto? Socio { get; set; }
    [ObservableProperty] public partial string Descricao { get; set; } = string.Empty;
    [ObservableProperty] public partial string Valor { get; set; } = string.Empty;
    [ObservableProperty] public partial DateTime Vencimento { get; set; } = DateTime.Today.AddDays(10);
    [ObservableProperty] public partial bool Ocupado { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    public DateTime Hoje => DateTime.Today;
    public bool TemSocio => Socio is not null;
    public bool SemSocio => Socio is null;
    public string SocioTexto => Socio is { } s ? $"{s.Nome} · título nº {s.NumeroTitulo} · {s.Situacao}" : string.Empty;

    partial void OnSocioChanged(SocioResumoDto? value)
    {
        OnPropertyChanged(nameof(TemSocio));
        OnPropertyChanged(nameof(SemSocio));
        OnPropertyChanged(nameof(SocioTexto));
    }

    [RelayCommand]
    private async Task BuscarAsync()
    {
        Erro = null;
        if (Busca.Trim().Length < 2)
        {
            Erro = "Digite pelo menos 2 letras do nome, o CPF ou o número do título.";
            return;
        }
        try
        {
            var pagina = await api.SociosAsync(Busca.Trim(), tamanhoPagina: 20);
            Resultados.Clear();
            foreach (var s in pagina.Itens) Resultados.Add(s);
            if (Resultados.Count == 0) Erro = "Nenhum sócio encontrado.";
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
    }

    [RelayCommand]
    private void Escolher(SocioResumoDto socio)
    {
        Socio = socio;
        Resultados.Clear();
        Erro = null;
    }

    [RelayCommand]
    private void TrocarSocio() => Socio = null;

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Erro = null;
        if (Socio is null)
        {
            Erro = "Escolha o sócio.";
            return;
        }
        if (!decimal.TryParse(Valor.Replace("R$", "").Trim(), System.Globalization.NumberStyles.Number, Opcoes.PtBr, out var valor) || valor <= 0)
        {
            Erro = "Informe o valor (use vírgula para centavos, ex.: 85,50).";
            return;
        }

        Ocupado = true;
        try
        {
            var c = await api.LancarCobrancaAsync(new LancarCobrancaRequest(Socio.Id, Descricao, valor, DateOnly.FromDateTime(Vencimento)));
            await Shell.Current.DisplayAlertAsync("Cobrança lançada",
                $"{c.Descricao}: {c.Valor.ToString("C", Opcoes.PtBr)}, vence em {c.Vencimento:dd/MM/yyyy}.\n" +
                $"{Socio.Nome} foi avisado(a) no app e por e-mail e pode pagar pela aba Financeiro.", "OK");
            await Shell.Current.GoToAsync("..");
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
}
