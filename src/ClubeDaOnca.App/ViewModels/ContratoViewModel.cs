using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Contratos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

public sealed record DependenteContratoLinha(DependenteContratoDto Dto)
{
    public string Nome => Dto.Nome;
    public string Detalhe => $"{Opcoes.Texto(Opcoes.Parentescos, Dto.Parentesco)} · {Dto.DataNascimento:dd/MM/yyyy}" +
        (Dto.Cpf is null ? "" : $" · CPF {Opcoes.FormatarCpf(Dto.Cpf)}");
}

public sealed record ContratoAnteriorLinha(ContratoDto Dto)
{
    public string Texto => $"Contrato {Dto.Numero} · {Opcoes.StatusContrato(Dto.Status)}" +
        (Dto.VigenciaInicio is { } i ? $" · {i:dd/MM/yyyy} a {Dto.VigenciaFim:dd/MM/yyyy}" : "");
}

/// <summary>
/// Contrato de adesão do sócio: cria, envia a proposta por e-mail, acompanha aceite e aval do presidente,
/// desliga e renova. Rota: contrato?socioId={guid}.
/// </summary>
public partial class ContratoViewModel(IClubeApi api) : ObservableObject, IQueryAttributable
{
    private Guid _socioId;
    private ContratoDto? _atual;

    public IReadOnlyList<Opcao> Categorias => Opcoes.Categorias;
    public ObservableCollection<DependenteContratoLinha> Dependentes { get; } = [];
    public ObservableCollection<string> LinhaDoTempo { get; } = [];
    public ObservableCollection<ContratoAnteriorLinha> Anteriores { get; } = [];
    public ObservableCollection<CobrancaLinha> Parcelas { get; } = [];

    [ObservableProperty] public partial bool TemParcelas { get; set; }
    [ObservableProperty] public partial string ResumoParcelas { get; set; } = string.Empty;

    [ObservableProperty] public partial string Titulo { get; set; } = "Contrato de adesão";
    [ObservableProperty] public partial string SocioNome { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TemContrato { get; set; }
    [ObservableProperty] public partial string Numero { get; set; } = string.Empty;
    [ObservableProperty] public partial string Status { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial Color CorStatus { get; set; } = Colors.Gray;
    [ObservableProperty] public partial string Vigencia { get; set; } = string.Empty;

    // Campos editáveis (rascunho / aguardando aceite)
    [ObservableProperty] public partial Opcao? Categoria { get; set; } = Opcoes.Categorias[0];
    [ObservableProperty] public partial string Valor { get; set; } = string.Empty;
    [ObservableProperty] public partial string DiaVencimento { get; set; } = "10";
    [ObservableProperty] public partial DateTime PrimeiroVencimento { get; set; } = DateTime.Today.AddMonths(1);
    [ObservableProperty] public partial bool RenovacaoAutomatica { get; set; } = true;

    // Ações disponíveis conforme o status
    [ObservableProperty] public partial bool Editavel { get; set; }
    [ObservableProperty] public partial bool PodeEnviar { get; set; }
    [ObservableProperty] public partial string TextoEnviar { get; set; } = "Gerar contrato e enviar ao candidato";
    [ObservableProperty] public partial bool PodeDesligar { get; set; }
    [ObservableProperty] public partial bool PodeRenovar { get; set; }
    [ObservableProperty] public partial bool PodeCriar { get; set; }
    [ObservableProperty] public partial bool TemDocumento { get; set; }
    [ObservableProperty] public partial bool TemAnteriores { get; set; }
    [ObservableProperty] public partial bool SemDependentes { get; set; }

    [ObservableProperty] public partial bool Ocupado { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? Aviso { get; set; }

    public DateTime Hoje => DateTime.Today;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("socioId", out var v) && Guid.TryParse(v?.ToString(), out var id)) _socioId = id;
    }

    [RelayCommand]
    private Task CarregarAsync() => Executar(RecarregarAsync);

    private async Task RecarregarAsync()
    {
        var socio = await api.SocioAsync(_socioId);
        SocioNome = $"{socio.Nome} · Título nº {socio.NumeroTitulo} · {Opcoes.Texto(Opcoes.Situacoes, socio.Situacao)}";
        var contratos = await api.ContratosDoSocioAsync(_socioId);
        _atual = contratos.FirstOrDefault();
        Anteriores.Clear();
        foreach (var c in contratos.Skip(1)) Anteriores.Add(new ContratoAnteriorLinha(c));
        TemAnteriores = Anteriores.Count > 0;

        if (_atual is null)
        {
            TemContrato = false;
            PodeCriar = true;
            Editavel = PodeEnviar = PodeDesligar = PodeRenovar = TemDocumento = false;
            Categoria = Opcoes.Categorias.FirstOrDefault(c => c.Valor == socio.Categoria) ?? Opcoes.Categorias[0];
            Aviso = "Este sócio ainda não tem contrato. Confira o valor e o vencimento e toque em \"Criar contrato\".";
            return;
        }
        Mostrar(_atual);
        await CarregarParcelasAsync();
    }

    private async Task CarregarParcelasAsync()
    {
        Parcelas.Clear();
        if (_atual is null || _atual.Status is "Rascunho" or "AguardandoAceite" or "EmAnalise" or "Reprovado")
        {
            TemParcelas = false;
            return;
        }
        foreach (var p in await api.ParcelasContratoAsync(_atual.Id)) Parcelas.Add(new CobrancaLinha(p));
        TemParcelas = Parcelas.Count > 0;
        var pagas = Parcelas.Count(p => p.Dto.Status == "Paga");
        var semBoleto = Parcelas.Count(p => p.Aberta && string.IsNullOrEmpty(p.Dto.LinhaDigitavel));
        ResumoParcelas = $"{pagas} de {Parcelas.Count} paga(s)" + (semBoleto > 0 ? $" · {semBoleto} sem boleto emitido" : "");
    }

    [RelayCommand]
    private Task EmitirBoletosAsync() => Executar(async () =>
    {
        if (_atual is null) return;
        var r = await api.EmitirBoletosAsync(_atual.Id);
        await CarregarParcelasAsync();
        await Shell.Current.DisplayAlertAsync("Boletos",
            r.Falhas > 0 ? $"{r.Emitidos} emitido(s) e {r.Falhas} com falha. Tente de novo em instantes."
                : r.Emitidos > 0 ? $"{r.Emitidos} boleto(s) emitido(s)." : "Todos os boletos já estavam emitidos.", "OK");
    });

    private void Mostrar(ContratoDto c)
    {
        _atual = c;
        TemContrato = true;
        Titulo = $"Contrato {c.Numero}";
        Numero = c.Numero;
        Status = c.Status;
        StatusTexto = Opcoes.StatusContrato(c.Status);
        CorStatus = c.Status switch
        {
            "Aprovado" => Color.FromArgb("#1B873F"),
            "Reprovado" or "Desligado" => Color.FromArgb("#B3261E"),
            "AguardandoAceite" or "EmAnalise" => Color.FromArgb("#B45309"),
            _ => Colors.Gray,
        };
        Categoria = Opcoes.Categorias.FirstOrDefault(o => o.Valor == c.Categoria) ?? Opcoes.Categorias[0];
        Valor = c.ValorMensalidade.ToString("0.00", Opcoes.PtBr);
        DiaVencimento = c.DiaVencimento.ToString();
        PrimeiroVencimento = c.PrimeiroVencimento.ToDateTime(TimeOnly.MinValue);
        RenovacaoAutomatica = c.RenovacaoAutomatica;
        Vigencia = c.VigenciaInicio is { } i
            ? $"{i:dd/MM/yyyy} a {c.VigenciaFim:dd/MM/yyyy}" + (c.RenovacaoAutomatica ? " · renovação automática" : "")
            : "12 meses a partir da aprovação do presidente";

        Dependentes.Clear();
        foreach (var d in c.Dependentes) Dependentes.Add(new DependenteContratoLinha(d));
        SemDependentes = Dependentes.Count == 0;

        LinhaDoTempo.Clear();
        LinhaDoTempo.Add($"Criado em {c.CriadoEm.ToLocalTime():dd/MM/yyyy HH:mm}");
        if (c.EnviadoEm is { } env) LinhaDoTempo.Add($"Proposta enviada para {c.EnviadoPara} em {env.ToLocalTime():dd/MM/yyyy HH:mm}");
        if (c.LinkExpiraEm is { } exp) LinhaDoTempo.Add($"Link de aceite válido até {exp.ToLocalTime():dd/MM/yyyy HH:mm}");
        if (c.AceiteEm is { } ac) LinhaDoTempo.Add($"Candidato concordou em {ac.ToLocalTime():dd/MM/yyyy HH:mm} (IP {c.AceiteIp})");
        if (c.Status == "EmAnalise") LinhaDoTempo.Add("Aguardando o aval do presidente");
        if (c.AprovadoEm is { } ap)
            LinhaDoTempo.Add(c.Status == "Reprovado"
                ? $"Reprovado por {c.AprovadoPor} em {ap.ToLocalTime():dd/MM/yyyy HH:mm}: {c.MotivoReprovacao}"
                : $"Aprovado por {c.AprovadoPor} em {ap.ToLocalTime():dd/MM/yyyy HH:mm}");
        if (c.DesligadoEm is { } dl) LinhaDoTempo.Add($"Desligado em {dl.ToLocalTime():dd/MM/yyyy HH:mm}: {c.MotivoDesligamento}");

        Editavel = c.Status is "Rascunho" or "AguardandoAceite";
        PodeEnviar = Editavel;
        TextoEnviar = c.Status == "AguardandoAceite" ? "Reenviar proposta ao candidato" : "Gerar contrato e enviar ao candidato";
        PodeDesligar = PodeRenovar = c.Status == "Aprovado";
        PodeCriar = c.Status is "Reprovado" or "Desligado";
        TemDocumento = c.DocumentoTipo is not null;
        Aviso = c.Status switch
        {
            "AguardandoAceite" => "Proposta enviada. Aguardando o candidato concordar pelo link recebido por e-mail.",
            "EmAnalise" => "O candidato concordou. A ficha está com o presidente para aprovação.",
            "Rascunho" => "Confira os dados e envie a proposta. O candidato recebe o contrato e o link de aceite por e-mail.",
            _ => null,
        };
    }

    private SalvarContratoRequest? Requisicao()
    {
        if (!decimal.TryParse(Valor.Replace("R$", "").Trim(), System.Globalization.NumberStyles.Number, Opcoes.PtBr, out var valor)
            || !int.TryParse(DiaVencimento, out var dia))
        {
            Erro = "Preencha o valor (use vírgula para centavos) e o dia de vencimento.";
            return null;
        }
        return new SalvarContratoRequest(Categoria?.Valor, valor, dia, DateOnly.FromDateTime(PrimeiroVencimento), RenovacaoAutomatica);
    }

    [RelayCommand]
    private Task CriarAsync() => Executar(async () =>
    {
        // Sem contrato ainda: cria com os padrões da configuração financeira (ajustáveis depois, no rascunho).
        Mostrar(await api.CriarContratoAsync(_socioId, new SalvarContratoRequest(Categoria?.Valor)));
        await RecarregarAsync();
    });

    [RelayCommand]
    private Task SalvarAsync() => Executar(async () =>
    {
        if (_atual is null || Requisicao() is not { } req) return;
        Mostrar(await api.AtualizarContratoAsync(_atual.Id, req));
        Aviso = Status == "Rascunho" && _atual.EnviadoEm is not null
            ? "Alterações salvas. O link enviado antes deixou de valer: reenvie a proposta."
            : "Alterações salvas.";
    });

    [RelayCommand]
    private Task EnviarAsync() => Executar(async () =>
    {
        if (_atual is null || Requisicao() is not { } req) return;
        var ok = await Shell.Current.DisplayAlertAsync("Enviar proposta",
            "O sistema vai gerar o contrato e enviá-lo por e-mail ao candidato, com o link para ele concordar. Continuar?",
            "Enviar", "Cancelar");
        if (!ok) return;
        if (_atual.Status == "Rascunho") await api.AtualizarContratoAsync(_atual.Id, req);
        Mostrar(await api.EnviarContratoAsync(_atual.Id));
        await Shell.Current.DisplayAlertAsync("Proposta enviada", $"Contrato {Numero} enviado para {_atual.EnviadoPara}.", "OK");
    });

    [RelayCommand]
    private Task DesligarAsync() => Executar(async () =>
    {
        if (_atual is null) return;
        var motivo = await Shell.Current.DisplayPromptAsync("Desligar associado",
            "Informe o motivo. As mensalidades que ainda vão vencer serão canceladas e o título ficará Cancelado.",
            "Desligar", "Cancelar", "Motivo do desligamento", maxLength: 500);
        if (string.IsNullOrWhiteSpace(motivo)) return;
        Mostrar(await api.DesligarContratoAsync(_atual.Id, new MotivoRequest(motivo)));
        await RecarregarAsync();
    });

    [RelayCommand]
    private Task RenovarAsync() => Executar(async () =>
    {
        if (_atual is null) return;
        var ok = await Shell.Current.DisplayAlertAsync("Renovar contrato",
            "Será criado um novo contrato com os dados atuais do cadastro. Ele passa de novo pelo aceite do sócio e pelo aval do presidente. Continuar?",
            "Renovar", "Cancelar");
        if (!ok) return;
        await api.RenovarContratoAsync(_atual.Id);
        await RecarregarAsync();
    });

    [RelayCommand]
    private Task VerDocumentoAsync() => Executar(async () =>
    {
        if (_atual is null) return;
        using var resp = await api.DocumentoContratoAsync(_atual.Id);
        await DocumentoService.AbrirAsync(resp, $"Contrato-{_atual.Numero.Replace('/', '-')}");
    });

    private async Task Executar(Func<Task> acao)
    {
        Erro = null;
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
