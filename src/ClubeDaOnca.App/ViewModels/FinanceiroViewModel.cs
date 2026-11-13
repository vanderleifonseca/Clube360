using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Financeiro;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

/// <summary>Cobrança formatada para listas (sócio e gestão).</summary>
public sealed record CobrancaLinha(CobrancaDto Dto)
{
    public string Descricao => Dto.Descricao;
    public string Valor => Opcoes.Moeda(Dto.Valor);
    public string Socio => $"{Dto.Socio} · título {Dto.NumeroTitulo}";
    public string Status => Dto.Vencida ? "Vencida" : Dto.Status;
    public string Situacao => Dto.Status switch
    {
        "Paga" => $"Paga em {Dto.PagoEm?.ToLocalTime():dd/MM/yyyy}" + (Dto.FormaPagamento is null ? "" : $" · {Dto.FormaPagamento}"),
        "Cancelada" => "Cancelada",
        _ when Dto.Vencida => $"Vencida em {Dto.Vencimento:dd/MM/yyyy}",
        _ => $"Vence em {Dto.Vencimento:dd/MM/yyyy}",
    };
    public bool Aberta => Dto.Status == "Aberta";

    /// <summary>"Parcela 3/12 · emitido em 10/11/2026 · vencimento 10/02/2027".</summary>
    public string Detalhe => string.Join(" · ", new[]
    {
        Dto.Parcela is { } p ? $"Parcela {p}/12" : null,
        Dto.EmitidaEm is { } e ? $"emitido em {e.ToLocalTime():dd/MM/yyyy}" : null,
        $"vencimento {Dto.Vencimento:dd/MM/yyyy}",
    }.Where(t => t is not null));

    public bool TemBoleto => Aberta && !string.IsNullOrEmpty(Dto.LinhaDigitavel);
    public string LinhaDigitavel => Dto.LinhaDigitavel ?? string.Empty;
}

/// <summary>Mensalidades e taxas do título do sócio logado.</summary>
public partial class FinanceiroViewModel(IClubeApi api) : ObservableObject
{
    public ObservableCollection<CobrancaLinha> Itens { get; } = [];

    [ObservableProperty] public partial string TotalEmAberto { get; set; } = string.Empty;
    [ObservableProperty] public partial string? Aviso { get; set; }
    [ObservableProperty] public partial bool Inadimplente { get; set; }
    [ObservableProperty] public partial bool Carregando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Erro = null;
        Carregando = true;
        try
        {
            var r = await api.MeuFinanceiroAsync();
            TotalEmAberto = Opcoes.Moeda(r.TotalEmAberto);
            Inadimplente = r.Inadimplente;
            Aviso = r.Inadimplente
                ? $"Há {Opcoes.Moeda(r.TotalVencido)} em atraso. Algumas funções, como reservas, podem ficar bloqueadas até a regularização."
                : r.TotalVencido > 0 ? $"{Opcoes.Moeda(r.TotalVencido)} vencido. Pague para evitar bloqueios." : null;

            Itens.Clear();
            // Em aberto primeiro (mais antigas no topo), depois o histórico.
            foreach (var c in r.Cobrancas.Where(c => c.Status == "Aberta").OrderBy(c => c.Vencimento))
                Itens.Add(new CobrancaLinha(c));
            foreach (var c in r.Cobrancas.Where(c => c.Status != "Aberta"))
                Itens.Add(new CobrancaLinha(c));
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
    private async Task PagarAsync(CobrancaLinha? linha)
    {
        if (linha is null) return;
        var escolha = await Shell.Current.DisplayActionSheetAsync($"{linha.Descricao} · {linha.Valor}", "Cancelar", null,
            "Pix (compensação imediata)", "Boleto");
        var forma = escolha switch
        {
            "Boleto" => "Boleto",
            not null when escolha.StartsWith("Pix") => "Pix",
            _ => null,
        };
        if (forma is null) return;

        Carregando = true;
        try
        {
            var pagamento = await api.PagarAsync(linha.Dto.Id, new PagarRequest(forma));
            await Shell.Current.GoToAsync("pagamento", new ShellNavigationQueryParameters
            {
                ["pagamento"] = pagamento,
                ["descricao"] = $"{linha.Descricao} · {linha.Valor}",
            });
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

/// <summary>Mostra o Pix (QR e copia-e-cola) ou o boleto gerado.</summary>
public partial class PagamentoViewModel(IClubeApi api) : ObservableObject, IQueryAttributable
{
    private PagamentoDto? _pagamento;

    [ObservableProperty] public partial string Descricao { get; set; } = string.Empty;
    [ObservableProperty] public partial bool EhPix { get; set; }
    [ObservableProperty] public partial ImageSource? QrPix { get; set; }
    [ObservableProperty] public partial string CopiaECola { get; set; } = string.Empty;
    [ObservableProperty] public partial string Validade { get; set; } = string.Empty;
    [ObservableProperty] public partial string LinhaDigitavel { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TemBoletoUrl { get; set; }
    [ObservableProperty] public partial bool Simulado { get; set; }
    [ObservableProperty] public partial string? Mensagem { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("descricao", out var d)) Descricao = d?.ToString() ?? string.Empty;
        if (!query.TryGetValue("pagamento", out var p) || p is not PagamentoDto pag) return;

        _pagamento = pag;
        EhPix = pag.Forma == "Pix";
        Simulado = pag.Simulado;
        CopiaECola = pag.PixCopiaECola ?? string.Empty;
        LinhaDigitavel = pag.LinhaDigitavel ?? string.Empty;
        TemBoletoUrl = !string.IsNullOrEmpty(pag.BoletoUrl);
        Validade = pag.PixExpiraEm is { } exp ? $"Válido até {exp.ToLocalTime():dd/MM/yyyy HH:mm}" : string.Empty;

        if (EhPix)
            QrPix = !string.IsNullOrEmpty(pag.PixImagemBase64)
                ? QrImagem.Fonte(Convert.FromBase64String(pag.PixImagemBase64))
                : !string.IsNullOrEmpty(pag.PixCopiaECola) ? QrImagem.Gerar(pag.PixCopiaECola) : null;
    }

    [RelayCommand]
    private async Task CopiarAsync()
    {
        var texto = EhPix ? CopiaECola : LinhaDigitavel;
        if (string.IsNullOrEmpty(texto)) return;
        await Clipboard.Default.SetTextAsync(texto);
        Mensagem = EhPix ? "Código Pix copiado. Cole no app do seu banco." : "Linha digitável copiada.";
    }

    [RelayCommand]
    private async Task AbrirBoletoAsync()
    {
        if (_pagamento?.BoletoUrl is { } url)
            await Launcher.Default.OpenAsync(url);
    }

    /// <summary>Só no ambiente de testes: faz o papel do aviso do banco.</summary>
    [RelayCommand]
    private async Task SimularPagamentoAsync()
    {
        if (_pagamento is null) return;
        try
        {
            await api.SimularConfirmacaoAsync(_pagamento.Id);
            await Shell.Current.DisplayAlertAsync("Pagamento confirmado", "Simulação concluída: a cobrança foi baixada.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Mensagem = ErroApi.Mensagem(ex);
        }
    }

    [RelayCommand]
    private Task ConcluirAsync() => Shell.Current.GoToAsync("..");
}
