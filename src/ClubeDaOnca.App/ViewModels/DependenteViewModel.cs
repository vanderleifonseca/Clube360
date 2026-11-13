using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Socios;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

/// <summary>
/// Cadastro/edição de dependente. Rota: dependente?socioId={guid}
/// (+ parâmetros "dependente" ao editar e "titular" com o nome do titular).
/// </summary>
public partial class DependenteViewModel(IClubeApi api) : ObservableObject, IQueryAttributable
{
    private Guid _socioId;
    private Guid? _dependenteId;

    public IReadOnlyList<Opcao> Parentescos => Opcoes.Parentescos;
    public IReadOnlyList<Opcao> Generos => Opcoes.Generos;
    public DateTime Hoje => DateTime.Today;

    [ObservableProperty] public partial string TituloPagina { get; set; } = "Novo dependente";
    [ObservableProperty] public partial string Cabecalho { get; set; } = "Dependente";
    [ObservableProperty] public partial string Nome { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cpf { get; set; } = string.Empty;
    [ObservableProperty] public partial DateTime DataNascimento { get; set; } = DateTime.Today.AddYears(-10);
    [ObservableProperty] public partial Opcao? Parentesco { get; set; } = Opcoes.Parentescos[1];
    [ObservableProperty] public partial Opcao? Genero { get; set; } = Opcoes.Generos[0];
    [ObservableProperty] public partial string Email { get; set; } = string.Empty;
    [ObservableProperty] public partial string Telefone { get; set; } = string.Empty;
    [ObservableProperty] public partial string Profissao { get; set; } = string.Empty;
    [ObservableProperty] public partial bool Ocupado { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("socioId", out var s) && Guid.TryParse(s?.ToString(), out var socioId))
            _socioId = socioId;
        if (query.TryGetValue("titular", out var t) && t is string titular && !string.IsNullOrWhiteSpace(titular))
            Cabecalho = $"Dependente de {titular}";

        if (query.TryGetValue("dependente", out var d) && d is DependenteDto dep)
        {
            _dependenteId = dep.Id;
            TituloPagina = "Editar dependente";
            Nome = dep.Nome;
            Cpf = Opcoes.FormatarCpf(dep.Cpf);
            DataNascimento = dep.DataNascimento.ToDateTime(TimeOnly.MinValue);
            Parentesco = Opcoes.Parentescos.FirstOrDefault(p => p.Valor == dep.Parentesco);
            Genero = Opcoes.Generos.FirstOrDefault(g => g.Valor == dep.Genero) ?? Opcoes.Generos[0];
            Email = dep.Email ?? string.Empty;
            Telefone = dep.Telefone ?? string.Empty;
            Profissao = dep.Profissao ?? string.Empty;
            if (dep.TitularNome is { } nomeTitular) Cabecalho = $"Dependente de {nomeTitular}";
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Erro = null;
        Ocupado = true;
        try
        {
            var req = new SalvarDependenteRequest(Nome, string.IsNullOrWhiteSpace(Cpf) ? null : Cpf,
                DateOnly.FromDateTime(DataNascimento), Parentesco?.Valor ?? "Outro",
                Genero?.Valor ?? "NaoInformado", Email, Telefone, Profissao);

            if (_dependenteId is { } id)
                await api.AtualizarDependenteAsync(_socioId, id, req);
            else
                await api.CriarDependenteAsync(_socioId, req);

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
