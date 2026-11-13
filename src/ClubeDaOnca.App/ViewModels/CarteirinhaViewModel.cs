using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Carteirinha;
using ClubeDaOnca.Domain.Carteirinha;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

public partial class CarteirinhaViewModel(CarteirinhaService servico) : ObservableObject
{
    private IDispatcherTimer? _timer;

    public ObservableCollection<CarteirinhaItemViewModel> Itens { get; } = [];

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? Aviso { get; set; }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Carregando = true;
        try
        {
            var (itens, offline) = await servico.ObterAsync();
            Itens.Clear();
            foreach (var c in itens)
                Itens.Add(new CarteirinhaItemViewModel(c));

            Aviso = itens.Count == 0
                ? (offline ? "Sem conexão e nenhuma carteirinha salva neste aparelho." : "Nenhuma carteirinha encontrada.")
                : offline ? "Sem conexão: exibindo a carteirinha salva neste aparelho." : null;

            AtualizarCodigos();
        }
        catch (Exception ex)
        {
            Aviso = ErroApi.Mensagem(ex);
        }
        finally
        {
            Carregando = false;
        }
    }

    public void Iniciar()
    {
        _timer ??= Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick -= AoTick;
        _timer.Tick += AoTick;
        _timer.Start();
    }

    public void Parar() => _timer?.Stop();

    private void AoTick(object? sender, EventArgs e) => AtualizarCodigos();

    private void AtualizarCodigos()
    {
        var agora = DateTimeOffset.UtcNow;
        foreach (var item in Itens)
            item.Atualizar(agora);
    }
}

/// <summary>Uma carteirinha (titular ou dependente) com o QR que se renova a cada 30 s.</summary>
public partial class CarteirinhaItemViewModel : ObservableObject
{
    private readonly CarteirinhaDto _dto;
    private readonly byte[] _chave;
    private long _passoAtual = -1;

    public CarteirinhaItemViewModel(CarteirinhaDto dto)
    {
        _dto = dto;
        _chave = Convert.FromBase64String(dto.ChaveQr);
    }

    public string Nome => _dto.Nome;

    public string Vinculo => _dto.Tipo == "Titular"
        ? "Sócio titular"
        : $"Dependente · {Opcoes.Texto(Opcoes.Parentescos, _dto.Parentesco)}";

    public string Titulo => $"Título nº {_dto.NumeroTitulo} · {Opcoes.Texto(Opcoes.Categorias, _dto.Categoria)}";
    public string Nascimento => $"Nascimento: {_dto.DataNascimento:dd/MM/yyyy}";
    public string Situacao => _dto.Situacao;
    public bool Ativo => _dto.Situacao == "Ativo";

    [ObservableProperty]
    public partial ImageSource? Qr { get; set; }

    [ObservableProperty]
    public partial string Contagem { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double Progresso { get; set; }

    public void Atualizar(DateTimeOffset agora)
    {
        var passo = QrCarteirinha.PassoDe(agora);
        if (passo != _passoAtual)
        {
            _passoAtual = passo;
            Qr = QrImagem.Gerar(QrCarteirinha.Gerar(_dto.PessoaId, _chave, agora));
        }

        var restantes = QrCarteirinha.SegundosRestantes(agora);
        Contagem = $"Código atualiza em {restantes}s";
        Progresso = restantes / (double)QrCarteirinha.JanelaSegundos;
    }
}
