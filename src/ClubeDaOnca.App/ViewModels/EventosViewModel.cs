using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Eventos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

public sealed record EventoLinha(EventoDto Dto)
{
    public string Titulo => Dto.Titulo;
    public string Quando => Dto.Inicio.Date == Dto.Termino.Date
        ? $"{Dto.Inicio:ddd, dd/MM/yyyy} · {Dto.Inicio:HH:mm} às {Dto.Termino:HH:mm}"
        : $"{Dto.Inicio:dd/MM/yyyy HH:mm} até {Dto.Termino:dd/MM/yyyy HH:mm}";
    public string Onde => string.Join(", ", Dto.Locais.Select(l => l.Nome));
    public string Ocupacao => $"{Dto.PessoasReservadas} de {Dto.Capacidade} pessoas" +
        (Dto.MinhasReservas > 0 ? $" · você tem {Dto.MinhasReservas} reserva(s)" : "");
    public string Status => Opcoes.StatusEvento(Dto.Status);
    public bool Cancelado => Dto.Status == "Cancelado";
    public bool Rascunho => Dto.Status == "Rascunho";
}

/// <summary>Lista de eventos: o sócio vê os publicados; a gestão vê todos. Rotas: aba "eventos" e "eventos-gestao".</summary>
public partial class EventosViewModel(IClubeApi api) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<EventoLinha> Itens { get; } = [];
    public ObservableCollection<ReservaEventoLinha> MinhasReservas { get; } = [];

    [ObservableProperty] public partial bool Gestao { get; set; }
    [ObservableProperty] public partial bool TemMinhasReservas { get; set; }
    [ObservableProperty] public partial bool SemEventos { get; set; }
    [ObservableProperty] public partial bool Carregando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        Gestao = query.TryGetValue("gestao", out var g) && g is true;

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Erro = null;
        Carregando = true;
        try
        {
            // Busca antes de limpar: duas cargas simultâneas (Appearing + RefreshView) não duplicam a lista.
            var carregados = await api.EventosAsync(Gestao ? true : null);
            Itens.Clear();
            foreach (var e in carregados) Itens.Add(new EventoLinha(e));
            var minhas = Gestao ? [] : (await api.MinhasReservasEventoAsync()).Where(r => r.Status == "Confirmada").ToList();
            MinhasReservas.Clear();
            foreach (var r in minhas) MinhasReservas.Add(new ReservaEventoLinha(r));
            TemMinhasReservas = MinhasReservas.Count > 0;
            SemEventos = Itens.Count == 0;
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
    private Task NovoAsync() => Shell.Current.GoToAsync("evento");

    [RelayCommand]
    private Task AbrirAsync(EventoLinha? e) => e is null ? Task.CompletedTask
        : Shell.Current.GoToAsync(Gestao ? "evento" : "evento-detalhe", new ShellNavigationQueryParameters { ["evento"] = e.Dto });

    [RelayCommand]
    private async Task CancelarReservaAsync(ReservaEventoLinha? r)
    {
        if (r is null) return;
        if (!await Shell.Current.DisplayAlertAsync("Cancelar reserva", $"Cancelar a reserva de {r.Dto.Ambiente} em {r.Dto.Evento}?",
                "Cancelar reserva", "Voltar")) return;
        try
        {
            await api.CancelarMinhaReservaEventoAsync(r.Dto.Id);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
    }
}

public sealed record ReservaEventoLinha(ReservaEventoDto Dto)
{
    public string Titulo => $"{Dto.Evento} · {Dto.Ambiente}";
    public string Detalhe => $"{Dto.EventoInicio:dd/MM/yyyy HH:mm} · {Dto.Pessoas} pessoa(s)" +
        (Dto.Valor > 0 ? $" · {Opcoes.Moeda(Dto.Valor)}" : "") + (Dto.Status == "Cancelada" ? " · cancelada" : "");
    public string Socio => $"{Dto.Socio} · título {Dto.NumeroTitulo}";
    public bool PodeCancelar => Dto.PodeCancelar;
}

public sealed record AmbienteLinha(AmbienteEventoDto Dto)
{
    public string Nome => Dto.Nome;
    public string Detalhe => $"Até {Dto.PessoasPorReserva} pessoa(s) · {Dto.VagasRestantes} de {Dto.Vagas} vaga(s) livre(s)" +
        (Dto.Valor > 0 ? $" · {Opcoes.Moeda(Dto.Valor)}" : " · gratuito");
    public bool Esgotado => Dto.VagasRestantes <= 0;
}

/// <summary>Evento para o sócio: detalhes, ambientes com vagas e reserva. Rota: evento-detalhe.</summary>
public partial class EventoDetalheViewModel(IClubeApi api) : ObservableObject, IQueryAttributable
{
    private Guid _id;

    public ObservableCollection<AmbienteLinha> Ambientes { get; } = [];

    [ObservableProperty] public partial string Titulo { get; set; } = "Evento";
    [ObservableProperty] public partial string Descricao { get; set; } = string.Empty;
    [ObservableProperty] public partial string Quando { get; set; } = string.Empty;
    [ObservableProperty] public partial string Onde { get; set; } = string.Empty;
    [ObservableProperty] public partial string Ocupacao { get; set; } = string.Empty;
    [ObservableProperty] public partial string Avisos { get; set; } = string.Empty;
    [ObservableProperty] public partial bool Carregando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("evento", out var v) && v is EventoDto e) Mostrar(e);
    }

    private void Mostrar(EventoDto e)
    {
        _id = e.Id;
        var linha = new EventoLinha(e);
        Titulo = e.Titulo;
        Descricao = e.Descricao ?? string.Empty;
        Quando = linha.Quando;
        Onde = linha.Onde;
        Ocupacao = linha.Ocupacao;
        Avisos = (e.AbertoAoPublico ? "Aberto ao público: você pode levar convidados. " : "Exclusivo para sócios e dependentes. ") +
                 $"Cancelamento da reserva pelo app até {e.PrazoCancelamentoHoras} h antes.";
        Ambientes.Clear();
        foreach (var a in e.Ambientes) Ambientes.Add(new AmbienteLinha(a));
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        if (_id == Guid.Empty) return;
        try { Mostrar(await api.EventoAsync(_id)); }
        catch (Exception ex) { Erro = ErroApi.Mensagem(ex); }
    }

    [RelayCommand]
    private async Task ReservarAsync(AmbienteLinha? a)
    {
        if (a is null) return;
        var resposta = await Shell.Current.DisplayPromptAsync($"Reservar: {a.Nome}",
            $"Quantas pessoas? (até {a.Dto.PessoasPorReserva})" + (a.Dto.Valor > 0 ? $"\nValor: {Opcoes.Moeda(a.Dto.Valor)}, cobrado no seu financeiro." : ""),
            "Reservar", "Cancelar", initialValue: a.Dto.PessoasPorReserva.ToString(), keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(resposta)) return;
        if (!int.TryParse(resposta, out var pessoas))
        {
            Erro = "Informe um número de pessoas.";
            return;
        }

        Erro = null;
        Carregando = true;
        try
        {
            await api.ReservarEventoAsync(_id, new ReservarEventoRequest(a.Dto.Id, pessoas, null));
            await Shell.Current.DisplayAlertAsync("Reserva confirmada",
                $"{a.Nome} reservado para {pessoas} pessoa(s)." + (a.Dto.Valor > 0 ? " O valor está no seu Financeiro." : ""), "OK");
            Mostrar(await api.EventoAsync(_id));
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

public partial class AmbienteEdicao : ObservableObject
{
    public Guid? Id { get; init; }
    [ObservableProperty] public partial string Nome { get; set; } = string.Empty;
    [ObservableProperty] public partial string PessoasPorReserva { get; set; } = "1";
    [ObservableProperty] public partial string Vagas { get; set; } = "10";
    [ObservableProperty] public partial string LimitePorSocio { get; set; } = "1";
    [ObservableProperty] public partial string Valor { get; set; } = "0";
    public int Reservadas { get; init; }
    public string Resumo => Reservadas > 0 ? $"{Reservadas} reserva(s) feitas" : string.Empty;
}

public partial class LocalEscolha(Guid id, string nome, int capacidade, bool marcado) : ObservableObject
{
    public Guid Id { get; } = id;
    public string Texto { get; } = capacidade > 0 ? $"{nome} ({capacidade} pessoas)" : nome;
    public int Capacidade { get; } = capacidade;

    [ObservableProperty] public partial bool Marcado { get; set; } = marcado;
}

/// <summary>Cadastro do evento pela secretaria, com checagem da agenda dos locais. Rota: evento (+ "evento" ao editar).</summary>
public partial class EventoViewModel(IClubeApi api) : ObservableObject, IQueryAttributable
{
    private EventoDto? _evento;

    public ObservableCollection<LocalEscolha> Locais { get; } = [];
    public ObservableCollection<AmbienteEdicao> Ambientes { get; } = [];
    public ObservableCollection<string> Conflitos { get; } = [];
    public ObservableCollection<ReservaEventoLinha> Reservas { get; } = [];

    [ObservableProperty] public partial string TituloPagina { get; set; } = "Novo evento";
    [ObservableProperty] public partial string Titulo { get; set; } = string.Empty;
    [ObservableProperty] public partial string Descricao { get; set; } = string.Empty;
    [ObservableProperty] public partial DateTime DataInicio { get; set; } = DateTime.Today.AddDays(7);
    [ObservableProperty] public partial TimeSpan HoraInicio { get; set; } = new(19, 0, 0);
    [ObservableProperty] public partial DateTime DataTermino { get; set; } = DateTime.Today.AddDays(7);
    [ObservableProperty] public partial TimeSpan HoraTermino { get; set; } = new(23, 0, 0);
    [ObservableProperty] public partial string Capacidade { get; set; } = "100";
    [ObservableProperty] public partial bool AbertoAoPublico { get; set; }
    [ObservableProperty] public partial bool DivulgarAssociados { get; set; } = true;
    [ObservableProperty] public partial bool DivulgarRedesSociais { get; set; }
    [ObservableProperty] public partial string PrazoCancelamentoHoras { get; set; } = "24";

    [ObservableProperty] public partial string Status { get; set; } = string.Empty;
    [ObservableProperty] public partial bool Existe { get; set; }
    [ObservableProperty] public partial bool PodePublicar { get; set; }
    [ObservableProperty] public partial bool PodeCancelar { get; set; }
    [ObservableProperty] public partial bool Editavel { get; set; } = true;
    [ObservableProperty] public partial string? Agenda { get; set; }
    [ObservableProperty] public partial bool AgendaLivre { get; set; }
    [ObservableProperty] public partial bool TemReservas { get; set; }
    [ObservableProperty] public partial bool SemReservas { get; set; }
    [ObservableProperty] public partial bool Ocupado { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    public DateTime Hoje => DateTime.Today;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("evento", out var v) && v is EventoDto e) _evento = e;
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        await Executar(async () =>
        {
            var espacos = await api.EspacosAsync(todos: true);
            if (_evento is { } atual) _evento = await api.EventoAsync(atual.Id);
            Locais.Clear();
            foreach (var e in espacos.Where(e => e.Ativo || _evento?.Locais.Any(l => l.Id == e.Id) == true))
                Locais.Add(new LocalEscolha(e.Id, e.Nome, e.Capacidade, _evento?.Locais.Any(l => l.Id == e.Id) == true));
            if (_evento is not null) Mostrar(_evento);
        });
    }

    private void Mostrar(EventoDto e)
    {
        _evento = e;
        TituloPagina = e.Titulo;
        Titulo = e.Titulo;
        Descricao = e.Descricao ?? string.Empty;
        DataInicio = e.Inicio.Date;
        HoraInicio = e.Inicio.TimeOfDay;
        DataTermino = e.Termino.Date;
        HoraTermino = e.Termino.TimeOfDay;
        Capacidade = e.Capacidade.ToString();
        AbertoAoPublico = e.AbertoAoPublico;
        DivulgarAssociados = e.DivulgarAssociados;
        DivulgarRedesSociais = e.DivulgarRedesSociais;
        PrazoCancelamentoHoras = e.PrazoCancelamentoHoras.ToString();
        Status = Opcoes.StatusEvento(e.Status) + (e.MotivoCancelamento is null ? "" : $": {e.MotivoCancelamento}") +
                 $" · {e.PessoasReservadas} de {e.Capacidade} pessoas reservadas";
        Existe = true;
        Editavel = e.Status != "Cancelado";
        PodePublicar = e.Status == "Rascunho";
        PodeCancelar = e.Status != "Cancelado";
        PodePostRedes = e.AbertoAoPublico && e.Status != "Cancelado";
        Ambientes.Clear();
        foreach (var a in e.Ambientes)
            Ambientes.Add(new AmbienteEdicao
            {
                Id = a.Id, Nome = a.Nome, PessoasPorReserva = a.PessoasPorReserva.ToString(), Vagas = a.Vagas.ToString(),
                LimitePorSocio = a.LimitePorSocio.ToString(), Valor = a.Valor.ToString("0.00", Opcoes.PtBr),
                Reservadas = a.Vagas - a.VagasRestantes,
            });
    }

    private DateTime Inicio => DataInicio.Date + HoraInicio;
    private DateTime Termino => DataTermino.Date + HoraTermino;
    private List<Guid> LocaisMarcados => Locais.Where(l => l.Marcado).Select(l => l.Id).ToList();

    [RelayCommand]
    private void AdicionarAmbiente() => Ambientes.Add(new AmbienteEdicao { Nome = $"Ambiente {Ambientes.Count + 1}" });

    [RelayCommand]
    private void RemoverAmbiente(AmbienteEdicao? a)
    {
        if (a is null) return;
        if (a.Reservadas > 0)
        {
            Erro = $"\"{a.Nome}\" já tem reservas e não pode ser removido.";
            return;
        }
        Ambientes.Remove(a);
    }

    [RelayCommand]
    private Task VerificarAgendaAsync() => Executar(async () =>
    {
        var r = await api.VerificarAgendaAsync(new VerificarAgendaRequest(LocaisMarcados, Inicio, Termino, _evento?.Id));
        MostrarConflitos(r.Disponivel, r.Conflitos);
    });

    private void MostrarConflitos(bool livre, IEnumerable<ConflitoAgendaDto> conflitos)
    {
        Conflitos.Clear();
        foreach (var c in conflitos) Conflitos.Add($"{c.Local}: {c.Inicio:dd/MM HH:mm}–{c.Fim:dd/MM HH:mm} · {c.Descricao}");
        AgendaLivre = livre;
        Agenda = livre ? "✅ Agenda livre: os locais estão disponíveis no período." : "⛔ Locais ocupados no período:";
    }

    [RelayCommand]
    private Task SalvarAsync() => Executar(async () =>
    {
        if (!int.TryParse(Capacidade, out var capacidade) || !int.TryParse(PrazoCancelamentoHoras, out var prazo))
        {
            Erro = "Preencha a capacidade e o prazo de cancelamento com números.";
            return;
        }
        var ambientes = new List<SalvarAmbienteRequest>();
        foreach (var a in Ambientes)
        {
            if (!int.TryParse(a.PessoasPorReserva, out var pessoas) || !int.TryParse(a.Vagas, out var vagas)
                || !int.TryParse(a.LimitePorSocio, out var limite)
                || !decimal.TryParse(a.Valor.Replace("R$", "").Trim(), System.Globalization.NumberStyles.Number, Opcoes.PtBr, out var valor))
            {
                Erro = $"Confira os números do ambiente \"{a.Nome}\" (use vírgula para centavos).";
                return;
            }
            ambientes.Add(new SalvarAmbienteRequest(a.Id, a.Nome, pessoas, vagas, limite, valor));
        }

        var req = new SalvarEventoRequest(Titulo, Descricao, Inicio, Termino, capacidade, AbertoAoPublico, DivulgarAssociados,
            AbertoAoPublico && DivulgarRedesSociais, prazo, LocaisMarcados, ambientes);
        try
        {
            Mostrar(_evento is { } e ? await api.AtualizarEventoAsync(e.Id, req) : await api.CriarEventoAsync(req));
            MostrarConflitos(true, []);
            await Shell.Current.DisplayAlertAsync("Evento salvo",
                PodePublicar ? "O evento já ocupa a agenda dos locais. Publique quando quiser abrir as reservas aos sócios." : "Alterações salvas.", "OK");
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict && ex.Content?.Contains("conflitos") == true)
        {
            var r = await api.VerificarAgendaAsync(new VerificarAgendaRequest(LocaisMarcados, Inicio, Termino, _evento?.Id));
            MostrarConflitos(false, r.Conflitos);
            Erro = "Os locais não estão livres nesse período. Resolva as reservas listadas ou escolha outro horário.";
        }
    });

    [RelayCommand]
    private Task PublicarAsync() => Executar(async () =>
    {
        if (_evento is null) return;
        if (!await Shell.Current.DisplayAlertAsync("Publicar evento", "O evento aparecerá para os sócios, que já poderão reservar. Continuar?",
                "Publicar", "Voltar")) return;
        Mostrar(await api.PublicarEventoAsync(_evento.Id));
    });

    [RelayCommand]
    private Task CancelarEventoAsync() => Executar(async () =>
    {
        if (_evento is null) return;
        var motivo = await Shell.Current.DisplayPromptAsync("Cancelar evento",
            "Informe o motivo. As reservas serão canceladas, as taxas não pagas também, e a agenda dos locais será liberada.",
            "Cancelar evento", "Voltar", "Motivo", maxLength: 500);
        if (string.IsNullOrWhiteSpace(motivo)) return;
        Mostrar(await api.CancelarEventoAsync(_evento.Id, new CancelarEventoRequest(motivo)));
    });

    [ObservableProperty] public partial bool PodePostRedes { get; set; }

    [RelayCommand]
    private Task DivulgarAgoraAsync() => Executar(async () =>
    {
        if (_evento is null) return;
        var texto = await Shell.Current.DisplayPromptAsync("Divulgar aos associados",
            "Mensagem (app, e-mail e WhatsApp). Quem desligou um canal não recebe por ele.", "Enviar", "Cancelar",
            initialValue: $"{_evento.Titulo}: {new EventoLinha(_evento).Quando}. Reserve pelo app, na aba Eventos.", maxLength: 2000);
        if (string.IsNullOrWhiteSpace(texto)) return;
        await api.CriarDivulgacaoAsync(new Contracts.Comunicacao.NovaDivulgacaoRequest(_evento.Titulo, texto, ["App", "Email", "WhatsApp"], _evento.Id));
        await Shell.Current.DisplayAlertAsync("Divulgação", "Enviando aos associados. Acompanhe em Gestão → Comunicados.", "OK");
    });

    /// <summary>Baixa a imagem e o texto prontos e abre o compartilhamento (Instagram, Facebook, WhatsApp...).</summary>
    [RelayCommand]
    private Task PostRedesAsync() => Executar(async () =>
    {
        if (_evento is null) return;
        var post = await api.PostEventoAsync(_evento.Id);
        using var resp = await api.ImagemPostEventoAsync(_evento.Id);
        resp.EnsureSuccessStatusCode();
        var arquivo = Path.Combine(FileSystem.CacheDirectory, $"evento-{_evento.Inicio:yyyy-MM-dd}.png");
        await File.WriteAllBytesAsync(arquivo, await resp.Content.ReadAsByteArrayAsync());
        await Clipboard.Default.SetTextAsync(post.Texto);
        await Shell.Current.DisplayAlertAsync("Post pronto",
            "A legenda foi copiada. Escolha onde publicar a imagem e cole a legenda.\n\n" + post.Texto, "Compartilhar");
        await Share.Default.RequestAsync(new ShareFileRequest { Title = _evento.Titulo, File = new ShareFile(arquivo, "image/png") });
    });

    [RelayCommand]
    private Task VerReservasAsync() => Executar(async () =>
    {
        if (_evento is null) return;
        // Busca antes de limpar: duas cargas simultâneas (Appearing + RefreshView) não duplicam a lista.
        var carregados = await api.ReservasDoEventoAsync(_evento.Id);
        Reservas.Clear();
        foreach (var r in carregados) Reservas.Add(new ReservaEventoLinha(r));
        TemReservas = true;
        SemReservas = Reservas.Count == 0;
    });

    [RelayCommand]
    private Task CancelarReservaAsync(ReservaEventoLinha? r) => Executar(async () =>
    {
        if (r is null || _evento is null) return;
        if (!await Shell.Current.DisplayAlertAsync("Cancelar reserva", $"Cancelar a reserva de {r.Dto.Socio} ({r.Dto.Ambiente})?",
                "Cancelar reserva", "Voltar")) return;
        await api.CancelarReservaEventoAsync(r.Dto.Id);
        await VerReservasAsync();
        Mostrar(await api.EventoAsync(_evento.Id));
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
