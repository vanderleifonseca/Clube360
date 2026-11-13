using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Socios;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

public sealed record SocioLinha(Guid Id, string Nome, string Detalhe, string Situacao)
{
    public bool Ativo => Situacao == "Ativo";

    public static SocioLinha De(SocioResumoDto s) => new(
        s.Id,
        s.Nome,
        $"Título {s.NumeroTitulo} · CPF {Opcoes.FormatarCpf(s.Cpf)} · {s.Categoria}" +
            (s.Genero is "NaoInformado" ? "" : $" · {Opcoes.Texto(Opcoes.Generos, s.Genero)}") +
            (s.QuantidadeDependentes > 0 ? $" · {s.QuantidadeDependentes} dependente(s)" : "") +
            (s.StatusContrato is null ? "" : $" · Contrato: {Opcoes.StatusContrato(s.StatusContrato)}"),
        Opcoes.Texto(Opcoes.Situacoes, s.Situacao));
}

public partial class SociosViewModel(IClubeApi api) : ObservableObject
{
    public ObservableCollection<SocioLinha> Itens { get; } = [];
    public IReadOnlyList<Opcao> Generos => Opcoes.FiltroGeneros;
    public IReadOnlyList<Opcao> Situacoes => Opcoes.FiltroSituacoes;

    [ObservableProperty]
    public partial Opcao? Genero { get; set; } = Opcoes.FiltroGeneros[0];

    [ObservableProperty]
    public partial Opcao? Situacao { get; set; } = Opcoes.FiltroSituacoes[0];

    // Trocar o filtro já refaz a busca.
    partial void OnGeneroChanged(Opcao? value) => BuscarCommand.Execute(null);
    partial void OnSituacaoChanged(Opcao? value) => BuscarCommand.Execute(null);

    [ObservableProperty]
    public partial string Busca { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Resumo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? Erro { get; set; }

    [RelayCommand]
    private async Task BuscarAsync()
    {
        Erro = null;
        Carregando = true;
        try
        {
            var pagina = await api.SociosAsync(string.IsNullOrWhiteSpace(Busca) ? null : Busca.Trim(),
                string.IsNullOrEmpty(Genero?.Valor) ? null : Genero.Valor,
                string.IsNullOrEmpty(Situacao?.Valor) ? null : Situacao.Valor, 1, 100);
            Itens.Clear();
            foreach (var s in pagina.Itens)
                Itens.Add(SocioLinha.De(s));

            Resumo = pagina.Total switch
            {
                0 => "Nenhum sócio encontrado.",
                1 => "1 sócio",
                var n when n > pagina.Itens.Count => $"Mostrando {pagina.Itens.Count} de {n} sócios. Refine a busca.",
                var n => $"{n} sócios",
            };
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
    private Task NovoAsync() => Shell.Current.GoToAsync("socio");

    [RelayCommand]
    private Task AbrirAsync(SocioLinha? linha) =>
        linha is null ? Task.CompletedTask : Shell.Current.GoToAsync($"socio?id={linha.Id}");
}
