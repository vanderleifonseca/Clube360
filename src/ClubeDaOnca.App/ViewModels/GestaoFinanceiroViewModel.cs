using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Financeiro;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

/// <summary>Cobranças do clube: gerar mensalidades, dar baixa, cancelar.</summary>
public partial class GestaoFinanceiroViewModel(IClubeApi api) : ObservableObject
{
    public static readonly IReadOnlyList<Opcao> Filtros =
    [
        new("vencidas", "Vencidas"),
        new("Aberta", "Em aberto"),
        new("Paga", "Pagas"),
        new("Cancelada", "Canceladas"),
    ];

    public IReadOnlyList<Opcao> OpcoesFiltro => Filtros;
    public ObservableCollection<CobrancaLinha> Itens { get; } = [];

    [ObservableProperty] public partial Opcao Filtro { get; set; } = Filtros[0];

    /// <summary>Mês da consulta (só o mês/ano importam).</summary>
    [ObservableProperty] public partial DateTime Mes { get; set; } = DateTime.Today;

    [ObservableProperty] public partial bool FiltrarPorMes { get; set; }
    [ObservableProperty] public partial string Resumo { get; set; } = string.Empty;
    [ObservableProperty] public partial bool Carregando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    partial void OnFiltroChanged(Opcao value) => CarregarCommand.Execute(null);
    partial void OnMesChanged(DateTime value) { if (FiltrarPorMes) CarregarCommand.Execute(null); }
    partial void OnFiltrarPorMesChanged(bool value) => CarregarCommand.Execute(null);

    private DateOnly Competencia => new(Mes.Year, Mes.Month, 1);

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Erro = null;
        Carregando = true;
        try
        {
            var vencidas = Filtro.Valor == "vencidas";
            var lista = await api.CobrancasAsync(
                status: vencidas ? null : Filtro.Valor,
                vencidas: vencidas ? true : null,
                competencia: FiltrarPorMes ? Competencia : null);

            Itens.Clear();
            foreach (var c in lista) Itens.Add(new CobrancaLinha(c));
            Resumo = lista.Count == 0 ? "Nenhuma cobrança neste filtro."
                : $"{lista.Count} cobrança(s) · total {Opcoes.Moeda(lista.Sum(c => c.Valor))}";
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
    private async Task GerarMensalidadesAsync()
    {
        var mes = Competencia.ToString("MMMM/yyyy", Opcoes.PtBr);
        if (!await Shell.Current.DisplayAlertAsync("Gerar mensalidades",
                $"Gerar as mensalidades de {mes} para todos os títulos ativos? Quem já tem a mensalidade do mês não recebe outra.\n\n" +
                "Isso também acontece sozinho todo dia 1º às 6h.", "Gerar", "Voltar"))
            return;

        try
        {
            var r = await api.GerarMensalidadesAsync(new GerarMensalidadesRequest(Competencia));
            await Shell.Current.DisplayAlertAsync("Mensalidades",
                $"{r.Geradas} gerada(s), {r.JaExistentes} já existia(m), {r.Ignoradas} título(s) sem cobrança (inativos ou isentos).", "OK");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
    }

    [RelayCommand]
    private async Task BaixaAsync(CobrancaLinha? linha)
    {
        if (linha is null) return;
        var forma = await Shell.Current.DisplayActionSheetAsync($"Baixa manual: {linha.Descricao} · {linha.Valor}\n{linha.Socio}",
            "Cancelar", null, "Dinheiro/cartão no balcão", "Pix", "Boleto");
        var valor = forma switch { "Pix" => "Pix", "Boleto" => "Boleto", null or "Cancelar" => null, _ => "Manual" };
        if (valor is null) return;

        var obs = await Shell.Current.DisplayPromptAsync("Observação", "Opcional (ex.: recibo nº 123)", "Confirmar baixa", "Voltar", maxLength: 200);
        if (obs is null) return;

        await Executar(() => api.BaixaManualAsync(linha.Dto.Id, new BaixaManualRequest(valor, obs)));
    }

    [RelayCommand]
    private async Task CancelarAsync(CobrancaLinha? linha)
    {
        if (linha is null) return;
        if (!await Shell.Current.DisplayAlertAsync("Cancelar cobrança",
                $"Cancelar {linha.Descricao} ({linha.Valor}) de {linha.Socio}? Use para isenções ou lançamentos indevidos.",
                "Cancelar cobrança", "Voltar"))
            return;
        await Executar(() => api.CancelarCobrancaAsync(linha.Dto.Id));
    }

    [RelayCommand]
    private Task InadimplentesAsync() => Shell.Current.GoToAsync("inadimplentes");

    [RelayCommand]
    private Task LancarCobrancaAsync() => Shell.Current.GoToAsync("lancar-cobranca");

    [RelayCommand]
    private Task ConfiguracaoAsync() => Shell.Current.GoToAsync("config-financeiro");

    private async Task Executar(Func<Task<CobrancaDto>> acao)
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

public sealed record InadimplenteLinha(InadimplenteDto Dto)
{
    public string Nome => $"{Dto.Nome} · título {Dto.NumeroTitulo}";
    public string Detalhe => $"{Dto.CobrancasVencidas} cobrança(s) · {Opcoes.Moeda(Dto.ValorVencido)} · desde {Dto.VencimentoMaisAntigo:dd/MM/yyyy}" +
                             (string.IsNullOrWhiteSpace(Dto.Telefone) ? "" : $" · {Dto.Telefone}");
}

public partial class InadimplentesViewModel(IClubeApi api) : ObservableObject
{
    public ObservableCollection<InadimplenteLinha> Itens { get; } = [];

    [ObservableProperty] public partial string Resumo { get; set; } = string.Empty;
    [ObservableProperty] public partial bool Carregando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Erro = null;
        Carregando = true;
        try
        {
            var lista = await api.InadimplentesAsync();
            Itens.Clear();
            foreach (var i in lista) Itens.Add(new InadimplenteLinha(i));
            Resumo = lista.Count == 0 ? "Nenhum título inadimplente. 🎉"
                : $"{lista.Count} título(s) · {Opcoes.Moeda(lista.Sum(i => i.ValorVencido))} em atraso";
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
}

public partial class ConfiguracaoFinanceiraViewModel(IClubeApi api) : ObservableObject
{
    [ObservableProperty] public partial string ValorIndividual { get; set; } = string.Empty;
    [ObservableProperty] public partial string ValorFamiliar { get; set; } = string.Empty;
    [ObservableProperty] public partial string ValorRemido { get; set; } = string.Empty;
    [ObservableProperty] public partial string DiaVencimento { get; set; } = string.Empty;
    [ObservableProperty] public partial string DiasTolerancia { get; set; } = string.Empty;
    [ObservableProperty] public partial string PrazoTaxaReservaDias { get; set; } = string.Empty;
    [ObservableProperty] public partial bool BloquearPortaria { get; set; }
    [ObservableProperty] public partial bool BloquearReservas { get; set; }
    [ObservableProperty] public partial bool Ocupado { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        try
        {
            var c = await api.ConfiguracaoFinanceiraAsync();
            ValorIndividual = c.ValorIndividual.ToString("0.00", Opcoes.PtBr);
            ValorFamiliar = c.ValorFamiliar.ToString("0.00", Opcoes.PtBr);
            ValorRemido = c.ValorRemido.ToString("0.00", Opcoes.PtBr);
            DiaVencimento = c.DiaVencimento.ToString();
            DiasTolerancia = c.DiasTolerancia.ToString();
            PrazoTaxaReservaDias = c.PrazoTaxaReservaDias.ToString();
            BloquearPortaria = c.BloquearInadimplentesNaPortaria;
            BloquearReservas = c.BloquearInadimplentesNasReservas;
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Erro = null;
        static bool Dinheiro(string s, out decimal v) =>
            decimal.TryParse(s.Replace("R$", "").Trim(), System.Globalization.NumberStyles.Number, Opcoes.PtBr, out v);

        if (!Dinheiro(ValorIndividual, out var ind) || !Dinheiro(ValorFamiliar, out var fam) || !Dinheiro(ValorRemido, out var rem)
            || !int.TryParse(DiaVencimento, out var dia) || !int.TryParse(DiasTolerancia, out var tol) || !int.TryParse(PrazoTaxaReservaDias, out var prazo))
        {
            Erro = "Preencha os números corretamente (use vírgula para centavos).";
            return;
        }

        Ocupado = true;
        try
        {
            await api.SalvarConfiguracaoFinanceiraAsync(new ConfiguracaoFinanceiraDto(ind, fam, rem, dia, tol, BloquearPortaria, BloquearReservas, prazo));
            await Shell.Current.DisplayAlertAsync("Configuração salva", "Os novos valores valem a partir das próximas mensalidades geradas.", "OK");
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
