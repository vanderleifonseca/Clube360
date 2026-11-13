using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Reservas;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

public partial class HorarioItem(HorarioDto dto) : ObservableObject
{
    public HorarioDto Dto { get; } = dto;
    public string Texto => $"{Dto.Inicio:HH\\:mm}–{Dto.Fim:HH\\:mm}";
    public bool Livre => Dto.Situacao == "Livre";
    public string Legenda => Dto.Situacao switch
    {
        "Livre" => "Livre",
        "Ocupado" => "Reservado",
        _ => "Indisponível",
    };

    [ObservableProperty]
    public partial bool Selecionado { get; set; }
}

public partial class NovaReservaViewModel(IClubeApi api) : ObservableObject
{
    public ObservableCollection<EspacoDto> Espacos { get; } = [];
    public ObservableCollection<HorarioItem> Horarios { get; } = [];
    public DateTime Hoje => DateTime.Today;

    [ObservableProperty]
    public partial EspacoDto? Espaco { get; set; }

    [ObservableProperty]
    public partial DateTime Data { get; set; } = DateTime.Today.AddDays(1);

    [ObservableProperty]
    public partial HorarioItem? Horario { get; set; }

    [ObservableProperty]
    public partial string Observacao { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Regras { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? AvisoHorarios { get; set; }

    [ObservableProperty]
    public partial bool Ocupado { get; set; }

    [ObservableProperty]
    public partial string? Erro { get; set; }

    public DateTime DataMaxima => Espaco is null ? DateTime.Today.AddYears(1) : DateTime.Today.AddDays(Espaco.AntecedenciaMaximaDias);

    [RelayCommand]
    private async Task CarregarAsync()
    {
        if (Espacos.Count > 0) return;
        try
        {
            foreach (var e in await api.EspacosAsync())
                Espacos.Add(e);
            Espaco ??= Espacos.FirstOrDefault();
            if (Espacos.Count == 0) AvisoHorarios = "Nenhum espaço disponível para reserva no momento.";
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
    }

    partial void OnEspacoChanged(EspacoDto? value)
    {
        OnPropertyChanged(nameof(DataMaxima));
        Regras = value is null ? string.Empty : DescreverRegras(value);
        _ = CarregarHorariosAsync();
    }

    partial void OnDataChanged(DateTime value) => _ = CarregarHorariosAsync();

    private async Task CarregarHorariosAsync()
    {
        Horario = null;
        Horarios.Clear();
        AvisoHorarios = null;
        if (Espaco is null) return;

        try
        {
            var agenda = await api.AgendaAsync(Espaco.Id, DateOnly.FromDateTime(Data));
            foreach (var h in agenda.Horarios)
                Horarios.Add(new HorarioItem(h));

            AvisoHorarios = agenda.Horarios.Count == 0
                ? "O espaço não funciona nesta data."
                : agenda.Horarios.Any(h => h.Situacao == "Livre") ? null : "Nenhum horário livre nesta data.";
        }
        catch (Exception ex)
        {
            AvisoHorarios = ErroApi.Mensagem(ex);
        }
    }

    [RelayCommand]
    private void Selecionar(HorarioItem? item)
    {
        if (item is null || !item.Livre) return;
        foreach (var h in Horarios) h.Selecionado = h == item;
        Horario = item;
    }

    [RelayCommand]
    private async Task ConfirmarAsync()
    {
        Erro = null;
        if (Espaco is null || Horario is null)
        {
            Erro = "Escolha o espaço e um horário livre.";
            return;
        }

        var resumo = $"{Espaco.Nome}\n{Data.ToString("dddd, dd/MM/yyyy", Opcoes.PtBr)} · {Horario.Texto}" +
                     (Espaco.Taxa > 0 ? $"\nTaxa: {Opcoes.Moeda(Espaco.Taxa)}" : "") +
                     (Espaco.ExigeAprovacao ? "\n\nA reserva fica pendente até a aprovação da secretaria." : "");
        if (!await Shell.Current.DisplayAlertAsync("Confirmar reserva", resumo, "Reservar", "Voltar"))
            return;

        Ocupado = true;
        try
        {
            var r = await api.ReservarAsync(new CriarReservaRequest(Espaco.Id, DateOnly.FromDateTime(Data), Horario.Dto.Inicio,
                string.IsNullOrWhiteSpace(Observacao) ? null : Observacao));

            await Shell.Current.DisplayAlertAsync(
                r.Status == "Pendente" ? "Reserva solicitada" : "Reserva confirmada",
                r.Status == "Pendente" ? "Você será avisado quando a secretaria aprovar." : "Bom proveito!", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
            await CarregarHorariosAsync(); // alguém pode ter reservado o horário
        }
        finally
        {
            Ocupado = false;
        }
    }

    private static string DescreverRegras(EspacoDto e)
    {
        var dias = e.DiasSemana.Count == 7 ? "todos os dias" : string.Join(", ", e.DiasSemana.Select(d => Opcoes.DiasAbreviados[d]));
        var linhas = new List<string>
        {
            $"Funciona {dias}, das {e.Abertura:HH\\:mm} às {e.Fechamento:HH\\:mm}.",
            $"Reserve com {e.AntecedenciaMinimaHoras} h a {e.AntecedenciaMaximaDias} dias de antecedência; até {e.LimiteReservasAtivas} reserva(s) ativa(s) por título.",
            e.Taxa > 0 ? $"Taxa de {Opcoes.Moeda(e.Taxa)}." : "Sem taxa.",
            $"Cancelamento pelo app até {e.PrazoCancelamentoHoras} h antes.",
        };
        if (e.ExigeAprovacao) linhas.Add("Sujeito a aprovação da secretaria.");
        if (!string.IsNullOrWhiteSpace(e.Descricao)) linhas.Insert(0, e.Descricao);
        return string.Join("\n", linhas);
    }
}
