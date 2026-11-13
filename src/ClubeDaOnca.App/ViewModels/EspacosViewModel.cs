using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Reservas;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

public sealed record EspacoLinha(EspacoDto Dto)
{
    public string Nome => Dto.Nome;
    public string Detalhe =>
        $"{Dto.Abertura:HH\\:mm}–{Dto.Fechamento:HH\\:mm} · horários de {Dto.DuracaoMinutos} min · " +
        (Dto.Taxa > 0 ? Opcoes.Moeda(Dto.Taxa) : "sem taxa") + (Dto.ExigeAprovacao ? " · com aprovação" : "") +
        $" · {Opcoes.Texto(Opcoes.TiposLocal, Dto.Tipo)}" + (Dto.Capacidade > 0 ? $" · {Dto.Capacidade} pessoas" : "");
    public bool Ativo => Dto.Ativo;
    public string Situacao => Dto.Ativo ? "Ativo" : "Inativo";
}

public partial class EspacosViewModel(IClubeApi api) : ObservableObject
{
    public ObservableCollection<EspacoLinha> Itens { get; } = [];

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
            // Busca antes de limpar: duas cargas simultâneas (Appearing + RefreshView) não duplicam a lista.
            var carregados = await api.EspacosAsync(todos: true);
            Itens.Clear();
            foreach (var e in carregados)
                Itens.Add(new EspacoLinha(e));
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
    private Task NovoAsync() => Shell.Current.GoToAsync("espaco");

    [RelayCommand]
    private Task AbrirAsync(EspacoLinha? linha) =>
        linha is null ? Task.CompletedTask
            : Shell.Current.GoToAsync("espaco", new ShellNavigationQueryParameters { ["espaco"] = linha.Dto });
}

public partial class DiaSemanaItem(int dia, bool marcado) : ObservableObject
{
    public int Dia { get; } = dia;
    public string Nome => Opcoes.DiasAbreviados[Dia];

    [ObservableProperty]
    public partial bool Marcado { get; set; } = marcado;
}

/// <summary>Cadastro/edição de espaço e suas regras. Rota: espaco (+ parâmetro "espaco" ao editar).</summary>
public partial class EspacoViewModel(IClubeApi api) : ObservableObject, IQueryAttributable
{
    private Guid? _id;

    public ObservableCollection<DiaSemanaItem> Dias { get; } = new(Enumerable.Range(0, 7).Select(d => new DiaSemanaItem(d, true)));

    [ObservableProperty] public partial string TituloPagina { get; set; } = "Novo espaço";
    [ObservableProperty] public partial string Nome { get; set; } = string.Empty;
    [ObservableProperty] public partial string Descricao { get; set; } = string.Empty;
    [ObservableProperty] public partial bool Ativo { get; set; } = true;
    [ObservableProperty] public partial TimeSpan Abertura { get; set; } = new(8, 0, 0);
    [ObservableProperty] public partial TimeSpan Fechamento { get; set; } = new(22, 0, 0);
    [ObservableProperty] public partial string DuracaoMinutos { get; set; } = "60";
    [ObservableProperty] public partial string AntecedenciaMinimaHoras { get; set; } = "2";
    [ObservableProperty] public partial string AntecedenciaMaximaDias { get; set; } = "30";
    [ObservableProperty] public partial string LimiteReservasAtivas { get; set; } = "2";
    [ObservableProperty] public partial string Taxa { get; set; } = "0";
    [ObservableProperty] public partial bool ExigeAprovacao { get; set; }
    [ObservableProperty] public partial string PrazoCancelamentoHoras { get; set; } = "24";
    [ObservableProperty] public partial Opcao? Tipo { get; set; } = Opcoes.TiposLocal[^1];
    [ObservableProperty] public partial string Capacidade { get; set; } = "0";
    public IReadOnlyList<Opcao> TiposLocal => Opcoes.TiposLocal;
    [ObservableProperty] public partial bool Ocupado { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("espaco", out var v) || v is not EspacoDto e) return;

        _id = e.Id;
        TituloPagina = "Editar espaço";
        Nome = e.Nome;
        Descricao = e.Descricao ?? string.Empty;
        Ativo = e.Ativo;
        Abertura = e.Abertura.ToTimeSpan();
        Fechamento = e.Fechamento.ToTimeSpan();
        DuracaoMinutos = e.DuracaoMinutos.ToString();
        AntecedenciaMinimaHoras = e.AntecedenciaMinimaHoras.ToString();
        AntecedenciaMaximaDias = e.AntecedenciaMaximaDias.ToString();
        LimiteReservasAtivas = e.LimiteReservasAtivas.ToString();
        Taxa = e.Taxa.ToString("0.00", Opcoes.PtBr);
        ExigeAprovacao = e.ExigeAprovacao;
        PrazoCancelamentoHoras = e.PrazoCancelamentoHoras.ToString();
        Tipo = Opcoes.TiposLocal.FirstOrDefault(t => t.Valor == e.Tipo) ?? Opcoes.TiposLocal[^1];
        Capacidade = e.Capacidade.ToString();
        foreach (var d in Dias) d.Marcado = e.DiasSemana.Contains(d.Dia);
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Erro = null;
        if (!int.TryParse(DuracaoMinutos, out var duracao) || !int.TryParse(AntecedenciaMinimaHoras, out var minima)
            || !int.TryParse(AntecedenciaMaximaDias, out var maxima) || !int.TryParse(LimiteReservasAtivas, out var limite)
            || !int.TryParse(PrazoCancelamentoHoras, out var prazo) || !int.TryParse(Capacidade, out var capacidade)
            || !decimal.TryParse(Taxa.Replace("R$", "").Trim(), System.Globalization.NumberStyles.Number, Opcoes.PtBr, out var taxa))
        {
            Erro = "Preencha os números corretamente (use vírgula para centavos na taxa).";
            return;
        }

        var req = new SalvarEspacoRequest(Nome, Descricao, Ativo, TimeOnly.FromTimeSpan(Abertura), TimeOnly.FromTimeSpan(Fechamento),
            duracao, Dias.Where(d => d.Marcado).Select(d => d.Dia).ToList(), minima, maxima, limite, taxa, ExigeAprovacao, prazo,
            Tipo?.Valor ?? "Outro", capacidade);

        Ocupado = true;
        try
        {
            if (_id is { } id) await api.AtualizarEspacoAsync(id, req);
            else await api.CriarEspacoAsync(req);
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
