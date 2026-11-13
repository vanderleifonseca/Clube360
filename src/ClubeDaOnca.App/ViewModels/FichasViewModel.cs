using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Contratos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

/// <summary>Ficha de um candidato para o aval do presidente: dados do cadastro, dependentes e proposta.</summary>
public sealed record FichaLinha(FichaAprovacaoDto Dto)
{
    public Guid ContratoId => Dto.Contrato.Id;
    public string Nome => Dto.Socio.Nome;
    public string Resumo => $"Contrato {Dto.Contrato.Numero} · {Dto.Contrato.Categoria} · {Opcoes.Moeda(Dto.Contrato.ValorMensalidade)}/mês" +
        (Dto.Contrato.AceiteEm is { } a ? $" · aceite em {a.ToLocalTime():dd/MM/yyyy HH:mm}" : "");

    public string Dados
    {
        get
        {
            var s = Dto.Socio;
            var e = s.Endereco;
            var linhas = new List<string>
            {
                $"CPF {Opcoes.FormatarCpf(s.Cpf)} · RG {s.Rg}",
                $"Nascimento {s.DataNascimento:dd/MM/yyyy} · {Opcoes.Texto(Opcoes.Generos, s.Genero)} · {Opcoes.Texto(Opcoes.EstadosCivis, s.EstadoCivil)}",
                $"{s.Nacionalidade} · {s.Profissao}",
                $"{s.Email} · {s.Telefone}",
            };
            if (e is not null)
                linhas.Add($"{e.Logradouro}, {e.Numero}{(string.IsNullOrWhiteSpace(e.Complemento) ? "" : " - " + e.Complemento)}, {e.Bairro}, {e.Cidade}/{e.Uf}");
            return string.Join("\n", linhas);
        }
    }

    public string DependentesTexto => Dto.Contrato.Dependentes.Count == 0
        ? "Sem dependentes"
        : string.Join("\n", Dto.Contrato.Dependentes.Select(d =>
            $"• {d.Nome} ({Opcoes.Texto(Opcoes.Parentescos, d.Parentesco)}, {d.DataNascimento:dd/MM/yyyy})"));
}

/// <summary>Fichas aguardando o aval do presidente. Rota: fichas.</summary>
public partial class FichasViewModel(IClubeApi api) : ObservableObject
{
    public ObservableCollection<FichaLinha> Itens { get; } = [];

    [ObservableProperty] public partial bool Carregando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string Resumo { get; set; } = string.Empty;

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Erro = null;
        Carregando = true;
        try
        {
            // Busca antes de limpar: duas cargas simultâneas (Appearing + RefreshView) não duplicam a lista.
            var carregados = await api.FichasAsync();
            Itens.Clear();
            foreach (var f in carregados) Itens.Add(new FichaLinha(f));
            Resumo = Itens.Count switch
            {
                0 => "Nenhuma ficha aguardando o seu aval.",
                1 => "1 ficha aguardando o seu aval.",
                var n => $"{n} fichas aguardando o seu aval.",
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
    private async Task VerContratoAsync(FichaLinha? f)
    {
        if (f is null) return;
        try
        {
            using var resp = await api.DocumentoFichaAsync(f.ContratoId);
            await DocumentoService.AbrirAsync(resp, $"Contrato-{f.Dto.Contrato.Numero.Replace('/', '-')}");
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
    }

    [RelayCommand]
    private async Task AprovarAsync(FichaLinha? f)
    {
        if (f is null) return;
        if (!await Shell.Current.DisplayAlertAsync("Aprovar associação",
                $"Aprovar a associação de {f.Nome}? O título passa a ficar ativo.", "Aprovar", "Cancelar")) return;
        await Decidir(() => api.AprovarFichaAsync(f.ContratoId), $"{f.Nome} foi aprovado(a).");
    }

    [RelayCommand]
    private async Task ReprovarAsync(FichaLinha? f)
    {
        if (f is null) return;
        var motivo = await Shell.Current.DisplayPromptAsync("Reprovar associação",
            $"Informe o motivo da reprovação de {f.Nome}:", "Reprovar", "Cancelar", "Motivo", maxLength: 500);
        if (string.IsNullOrWhiteSpace(motivo)) return;
        await Decidir(() => api.ReprovarFichaAsync(f.ContratoId, new MotivoRequest(motivo)), $"A associação de {f.Nome} foi reprovada.");
    }

    private async Task Decidir(Func<Task<ContratoDto>> acao, string mensagem)
    {
        Erro = null;
        Carregando = true;
        try
        {
            await acao();
            await Shell.Current.DisplayAlertAsync("Ficha avaliada", mensagem, "OK");
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
        finally
        {
            Carregando = false;
        }
        await CarregarAsync();
    }
}
