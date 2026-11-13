using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Reservas;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

/// <summary>Fila de reservas da secretaria: aprovar, recusar e cancelar.</summary>
public partial class GestaoReservasViewModel(IClubeApi api) : ObservableObject
{
    public static readonly IReadOnlyList<Opcao> Filtros =
    [
        new("Pendente", "Aguardando aprovação"),
        new("", "Próximas (pendentes e confirmadas)"),
        new("Cancelada", "Canceladas"),
        new("Recusada", "Recusadas"),
    ];

    public IReadOnlyList<Opcao> OpcoesFiltro => Filtros;
    public ObservableCollection<ReservaLinha> Itens { get; } = [];

    [ObservableProperty]
    public partial Opcao Filtro { get; set; } = Filtros[0];

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string Resumo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Erro { get; set; }

    partial void OnFiltroChanged(Opcao value) => CarregarCommand.Execute(null);

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Erro = null;
        Carregando = true;
        try
        {
            var lista = await api.ReservasAsync(string.IsNullOrEmpty(Filtro.Valor) ? null : Filtro.Valor);
            Itens.Clear();
            foreach (var r in lista) Itens.Add(new ReservaLinha(r));
            Resumo = lista.Count == 0 ? "Nenhuma reserva neste filtro." : $"{lista.Count} reserva(s)";
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private Task AprovarAsync(ReservaLinha? linha) =>
        linha is null ? Task.CompletedTask : Executar(() => api.AprovarReservaAsync(linha.Dto.Id));

    [RelayCommand]
    private async Task RecusarAsync(ReservaLinha? linha)
    {
        if (linha is null) return;
        var motivo = await Shell.Current.DisplayPromptAsync("Recusar reserva",
            $"{linha.Espaco} · {linha.Quando}\n{linha.Socio}\n\nInforme o motivo (o sócio verá esta mensagem):",
            "Recusar", "Voltar", maxLength: 300);
        if (string.IsNullOrWhiteSpace(motivo)) return;
        await Executar(() => api.RecusarReservaAsync(linha.Dto.Id, new RecusarReservaRequest(motivo)));
    }

    [RelayCommand]
    private async Task CancelarAsync(ReservaLinha? linha)
    {
        if (linha is null) return;
        if (!await Shell.Current.DisplayAlertAsync("Cancelar reserva",
                $"Cancelar a reserva de {linha.Socio} ({linha.Espaco}, {linha.Quando})?", "Cancelar reserva", "Voltar"))
            return;
        await Executar(() => api.CancelarReservaAsync(linha.Dto.Id));
    }

    private async Task Executar(Func<Task<ReservaDto>> acao)
    {
        try
        {
            await acao();
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
    }
}
