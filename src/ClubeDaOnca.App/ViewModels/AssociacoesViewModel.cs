using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Associacoes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

public sealed record AssociacaoLinha(AssociacaoResumoDto Dto)
{
    public string Nome => Dto.Nome;
    public bool Ativa => Dto.Ativa;
    public string Situacao => Dto.Ativa ? "Ativa" : "Desativada";

    public string Detalhe
    {
        get
        {
            var local = string.Join("/", new[] { Dto.Cidade, Dto.Uf }.Where(p => !string.IsNullOrWhiteSpace(p)));
            var presidente = Dto.Presidente is null
                ? "sem presidente cadastrado"
                : $"Presidente: {Dto.Presidente} (até {Dto.FimMandato:dd/MM/yyyy})";
            return string.Join(" · ", new[] { local, presidente }.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }

    public bool MandatoVencendo => Dto.MandatoVencendo;
}

/// <summary>Lista das associações da rede (somente SuperAdmin). Rota: associacoes.</summary>
public partial class AssociacoesViewModel(IClubeApi api) : ObservableObject
{
    public ObservableCollection<AssociacaoLinha> Itens { get; } = [];

    [ObservableProperty] public partial bool Carregando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Erro = null;
        Carregando = true;
        try
        {
            // Busca antes de limpar: duas cargas simultâneas (Appearing + RefreshView) não duplicam a lista.
            var carregados = await api.AssociacoesAsync();
            Itens.Clear();
            foreach (var a in carregados)
                Itens.Add(new AssociacaoLinha(a));
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
    private Task NovaAsync() => Shell.Current.GoToAsync("associacao");

    [RelayCommand]
    private Task AbrirAsync(AssociacaoLinha? linha) =>
        linha is null ? Task.CompletedTask
            : Shell.Current.GoToAsync("associacao", new ShellNavigationQueryParameters { ["id"] = linha.Dto.Id });
}

public sealed record MandatoLinha(MandatoDto Dto)
{
    public string Nome => Dto.Nome;
    public string Periodo => $"{Dto.Inicio:dd/MM/yyyy} a {Dto.Fim:dd/MM/yyyy}" + (Dto.Vigente ? " · atual" : "");
    public string? Email => Dto.Email;
}

public sealed record DiretorLinha(DiretorDto Dto)
{
    public string Nome => Dto.Nome;
    public string Detalhe => $"{Dto.Cargo} · desde {Dto.Inicio:dd/MM/yyyy}" + (Dto.Fim is { } f ? $" até {f:dd/MM/yyyy}" : "")
        + (Dto.Ativo ? "" : " · encerrado");
}

/// <summary>
/// Cadastro da associação: dados, endereço, presidente (mandato de 2 anos) e diretoria.
/// Rota: associacao (+ "id" para editar; sem "id" o SuperAdmin cadastra uma nova unidade com seu administrador;
/// com "atual" = true abre a associação do usuário logado).
/// </summary>
public partial class AssociacaoViewModel(IClubeApi api, SessaoService sessao) : ObservableObject, IQueryAttributable
{
    private Guid? _id;
    private bool _atual;

    public ObservableCollection<MandatoLinha> Mandatos { get; } = [];
    public ObservableCollection<DiretorLinha> Diretores { get; } = [];

    [ObservableProperty] public partial string TituloPagina { get; set; } = "Nova associação";
    [ObservableProperty] public partial bool Nova { get; set; } = true;
    [ObservableProperty] public partial bool PodeAtivar { get; set; }
    [ObservableProperty] public partial bool Ativa { get; set; } = true;

    [ObservableProperty] public partial string Nome { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cnpj { get; set; } = string.Empty;
    [ObservableProperty] public partial string Email { get; set; } = string.Empty;
    [ObservableProperty] public partial string Telefone { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cep { get; set; } = string.Empty;
    [ObservableProperty] public partial string Logradouro { get; set; } = string.Empty;
    [ObservableProperty] public partial string Numero { get; set; } = string.Empty;
    [ObservableProperty] public partial string Complemento { get; set; } = string.Empty;
    [ObservableProperty] public partial string Bairro { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cidade { get; set; } = string.Empty;
    [ObservableProperty] public partial string Uf { get; set; } = string.Empty;

    // Administrador da nova unidade (só no cadastro).
    [ObservableProperty] public partial string AdminNome { get; set; } = string.Empty;
    [ObservableProperty] public partial string AdminEmail { get; set; } = string.Empty;

    // Presidente
    [ObservableProperty] public partial string PresidenteAtual { get; set; } = "Nenhum presidente com mandato vigente.";
    [ObservableProperty] public partial bool MandatoVencendo { get; set; }
    [ObservableProperty] public partial string NovoPresidenteNome { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoPresidenteCpf { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoPresidenteEmail { get; set; } = string.Empty;
    [ObservableProperty] public partial DateTime NovoPresidenteInicio { get; set; } = DateTime.Today;

    // Diretoria
    [ObservableProperty] public partial string NovoDiretorNome { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoDiretorCargo { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoDiretorEmail { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoDiretorTelefone { get; set; } = string.Empty;
    [ObservableProperty] public partial DateTime NovoDiretorInicio { get; set; } = DateTime.Today;

    [ObservableProperty] public partial bool Ocupado { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? Mensagem { get; set; }

    // Conta do Asaas da unidade (só o administrador da própria unidade vê)
    [ObservableProperty] public partial bool PodePagamentos { get; set; }
    [ObservableProperty] public partial string SituacaoAsaas { get; set; } = string.Empty;
    [ObservableProperty] public partial string WebhookUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovaChaveAsaas { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoTokenWebhook { get; set; } = string.Empty;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var v) && v is Guid id) _id = id;
        _atual = query.TryGetValue("atual", out var a) && a is true;
        Nova = _id is null && !_atual;
        TituloPagina = Nova ? "Nova associação" : "Associação";
        PodeAtivar = sessao.EhSuperAdmin && !Nova;
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        if (Nova) return;
        Erro = null;
        Ocupado = true;
        try
        {
            Preencher(_atual ? await api.AssociacaoAtualAsync() : await api.AssociacaoAsync(_id!.Value));
            await CarregarPagamentosAsync();
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

    private void Preencher(AssociacaoDto a)
    {
        _id = a.Id;
        TituloPagina = a.Nome;
        Ativa = a.Ativa;
        Nome = a.Nome;
        Cnpj = a.Cnpj ?? string.Empty;
        Email = a.Email ?? string.Empty;
        Telefone = a.Telefone ?? string.Empty;
        Cep = a.Endereco.Cep ?? string.Empty;
        Logradouro = a.Endereco.Logradouro ?? string.Empty;
        Numero = a.Endereco.Numero ?? string.Empty;
        Complemento = a.Endereco.Complemento ?? string.Empty;
        Bairro = a.Endereco.Bairro ?? string.Empty;
        Cidade = a.Endereco.Cidade ?? string.Empty;
        Uf = a.Endereco.Uf ?? string.Empty;

        PresidenteAtual = a.PresidenteAtual is { } p
            ? $"{p.Nome} — mandato até {p.Fim:dd/MM/yyyy}"
            : "Nenhum presidente com mandato vigente.";
        MandatoVencendo = a.MandatoVencendo;

        Mandatos.Clear();
        foreach (var m in a.Mandatos) Mandatos.Add(new MandatoLinha(m));
        Diretores.Clear();
        foreach (var d in a.Diretores) Diretores.Add(new DiretorLinha(d));
    }

    private async Task CarregarPagamentosAsync()
    {
        PodePagamentos = sessao.EhAdmin && sessao.Usuario?.AssociacaoId == _id;
        if (!PodePagamentos || _id is not { } id) return;
        MostrarPagamentos(await api.PagamentosAssociacaoAsync(id));
    }

    private void MostrarPagamentos(PagamentosAssociacaoDto p)
    {
        SituacaoAsaas = (p.ContaPropria ? "Conta própria do Asaas configurada." : "Usando a conta geral do servidor (sem chave própria).") +
            (p.WebhookConfigurado ? " Webhook com token próprio." : "") + $" Gateway atual: {p.Gateway}.";
        WebhookUrl = p.WebhookUrl;
    }

    [RelayCommand]
    private async Task SalvarPagamentosAsync()
    {
        if (_id is not { } id) return;
        if (string.IsNullOrWhiteSpace(NovaChaveAsaas) && string.IsNullOrWhiteSpace(NovoTokenWebhook))
        {
            Erro = "Informe a chave da API e/ou o token do webhook.";
            return;
        }
        await ExecutarAsync(async () =>
        {
            MostrarPagamentos(await api.SalvarPagamentosAssociacaoAsync(id, new SalvarPagamentosAssociacaoRequest(
                string.IsNullOrWhiteSpace(NovaChaveAsaas) ? null : NovaChaveAsaas,
                string.IsNullOrWhiteSpace(NovoTokenWebhook) ? null : NovoTokenWebhook)));
            NovaChaveAsaas = NovoTokenWebhook = string.Empty;
            Mensagem = "Conta do Asaas salva (as chaves ficam criptografadas).";
        });
    }

    [RelayCommand]
    private async Task RemoverContaAsaasAsync()
    {
        if (_id is not { } id) return;
        if (!await Shell.Current.DisplayAlertAsync("Conta do Asaas",
                "Remover a conta própria? Os próximos boletos sairão pela conta geral do servidor.", "Remover", "Cancelar")) return;
        await ExecutarAsync(async () =>
            MostrarPagamentos(await api.SalvarPagamentosAssociacaoAsync(id, new SalvarPagamentosAssociacaoRequest("", ""))));
    }

    private EnderecoDto Endereco() => new(Cep, Logradouro, Numero, Complemento, Bairro, Cidade, Uf);

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Erro = null;
        Mensagem = null;
        Ocupado = true;
        try
        {
            if (Nova)
            {
                var criada = await api.CriarAssociacaoAsync(new NovaAssociacaoRequest(
                    Nome, Cnpj, Email, Telefone, Endereco(), AdminNome, AdminEmail));
                await Shell.Current.DisplayAlertAsync("Associação cadastrada",
                    $"Login do administrador: {criada.AdminLogin}\nSenha provisória: {criada.SenhaProvisoria}\n\n" +
                    "Anote e entregue ao administrador. A senha será trocada no primeiro acesso.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            Preencher(await api.AtualizarAssociacaoAsync(_id!.Value, new SalvarAssociacaoRequest(Nome, Cnpj, Email, Telefone, Endereco())));
            Mensagem = "Dados salvos.";
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

    [RelayCommand]
    private async Task AlternarAtivaAsync()
    {
        if (_id is not { } id) return;
        var desativar = Ativa;
        if (desativar && !await Shell.Current.DisplayAlertAsync("Desativar associação",
                "Os usuários desta unidade não conseguirão entrar no app. Os dados são mantidos. Continuar?", "Desativar", "Cancelar"))
            return;
        await ExecutarAsync(async () => Preencher(await api.AlterarAssociacaoAtivaAsync(id, new AlterarAtivaRequest(!desativar))));
    }

    [RelayCommand]
    private async Task AdicionarPresidenteAsync()
    {
        if (_id is not { } id) return;
        await ExecutarAsync(async () =>
        {
            var mandato = await api.CriarMandatoAsync(id, new SalvarMandatoRequest(NovoPresidenteNome, NovoPresidenteCpf, NovoPresidenteEmail,
                DateOnly.FromDateTime(NovoPresidenteInicio)));
            NovoPresidenteNome = NovoPresidenteCpf = NovoPresidenteEmail = string.Empty;
            await RecarregarAsync(id);
            Mensagem = "Mandato cadastrado (2 anos).";
            if (mandato.SenhaProvisoria is not null)
                await Shell.Current.DisplayAlertAsync("Conta do presidente criada",
                    $"Login: {mandato.Login}\nSenha provisória: {mandato.SenhaProvisoria}\n\n" +
                    "Entregue ao presidente. Com ela ele vê e aprova as fichas de novos sócios no app. A senha será trocada no primeiro acesso.",
                    "OK");
        });
    }

    [RelayCommand]
    private async Task RemoverMandatoAsync(MandatoLinha? linha)
    {
        if (_id is not { } id || linha is null) return;
        if (!await Shell.Current.DisplayAlertAsync("Remover mandato", $"Remover o mandato de {linha.Nome}?", "Remover", "Cancelar"))
            return;
        await ExecutarAsync(async () =>
        {
            await api.RemoverMandatoAsync(id, linha.Dto.Id);
            await RecarregarAsync(id);
        });
    }

    [RelayCommand]
    private async Task AdicionarDiretorAsync()
    {
        if (_id is not { } id) return;
        await ExecutarAsync(async () =>
        {
            await api.CriarDiretorAsync(id, new SalvarDiretorRequest(NovoDiretorNome, NovoDiretorCargo, NovoDiretorEmail,
                NovoDiretorTelefone, DateOnly.FromDateTime(NovoDiretorInicio), null));
            NovoDiretorNome = NovoDiretorCargo = NovoDiretorEmail = NovoDiretorTelefone = string.Empty;
            await RecarregarAsync(id);
            Mensagem = "Diretor adicionado.";
        });
    }

    [RelayCommand]
    private async Task RemoverDiretorAsync(DiretorLinha? linha)
    {
        if (_id is not { } id || linha is null) return;
        if (!await Shell.Current.DisplayAlertAsync("Remover diretor", $"Remover {linha.Nome} da diretoria?", "Remover", "Cancelar"))
            return;
        await ExecutarAsync(async () =>
        {
            await api.RemoverDiretorAsync(id, linha.Dto.Id);
            await RecarregarAsync(id);
        });
    }

    private async Task RecarregarAsync(Guid id) =>
        Preencher(_atual ? await api.AssociacaoAtualAsync() : await api.AssociacaoAsync(id));

    private async Task ExecutarAsync(Func<Task> acao)
    {
        Erro = null;
        Mensagem = null;
        Ocupado = true;
        try
        {
            await acao();
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
