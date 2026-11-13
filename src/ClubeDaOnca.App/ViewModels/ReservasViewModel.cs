using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Reservas;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

/// <summary>Reserva formatada para listas (sócio e gestão).</summary>
public sealed record ReservaLinha(ReservaDto Dto)
{
    public string Espaco => Dto.Espaco;
    public string Quando =>
        $"{Dto.Data.ToString("ddd, dd/MM/yyyy", Opcoes.PtBr)} · {Dto.Inicio:HH\\:mm}–{Dto.Fim:HH\\:mm}";
    public string Socio => $"{Dto.Socio} · título {Dto.NumeroTitulo}";
    public string Status => Dto.Status;
    public string StatusTexto => Opcoes.StatusReserva(Dto.Status);
    public string Detalhe => string.Join(" · ", new[]
    {
        Dto.Taxa > 0 ? $"Taxa {Opcoes.Moeda(Dto.Taxa)}" : null,
        Dto.Observacao,
        Dto.MotivoRecusa is null ? null : $"Motivo: {Dto.MotivoRecusa}",
    }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public bool TemDetalhe => Detalhe.Length > 0;
    public bool PodeCancelar => Dto.PodeCancelar;
    public bool Pendente => Dto.Status == "Pendente";
}

/// <summary>Minhas reservas (sócio titular).</summary>
public partial class ReservasViewModel(IClubeApi api) : ObservableObject
{
    public ObservableCollection<ReservaLinha> Itens { get; } = [];

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? Erro { get; set; }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Erro = null;
        Carregando = true;
        try
        {
            var reservas = await api.MinhasReservasAsync();
            Itens.Clear();
            // Próximas primeiro (mais perto no topo); depois o histórico.
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            foreach (var r in reservas.Where(r => r.Data >= hoje && r.Status is "Pendente" or "Confirmada")
                         .OrderBy(r => r.Data).ThenBy(r => r.Inicio))
                Itens.Add(new ReservaLinha(r));
            foreach (var r in reservas.Where(r => !(r.Data >= hoje && r.Status is "Pendente" or "Confirmada")))
                Itens.Add(new ReservaLinha(r));
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
    private Task NovaAsync() => Shell.Current.GoToAsync("nova-reserva");

    [RelayCommand]
    private async Task CancelarAsync(ReservaLinha? linha)
    {
        if (linha is null) return;
        var ok = await Shell.Current.DisplayAlertAsync("Cancelar reserva",
            $"Cancelar a reserva de {linha.Espaco} em {linha.Quando}?", "Cancelar reserva", "Voltar");
        if (!ok) return;

        try
        {
            await api.CancelarReservaAsync(linha.Dto.Id);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
    }
}
