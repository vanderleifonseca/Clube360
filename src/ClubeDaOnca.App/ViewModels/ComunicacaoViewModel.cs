using System.Collections.ObjectModel;
using ClubeDaOnca.App.Services;
using ClubeDaOnca.Contracts.Comunicacao;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClubeDaOnca.App.ViewModels;

public sealed record AvisoLinha(AvisoDto Dto)
{
    public string Titulo => (Dto.Lido ? "" : "● ") + Dto.Titulo;
    public string Mensagem => Dto.Mensagem;
    public string Quando => Dto.CriadoEm.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + " · " + Dto.Tipo switch
    {
        "Evento" => "Evento",
        "Cobranca" => "Financeiro",
        "Contrato" => "Contrato",
        _ => "Comunicado",
    };
}

/// <summary>Central de avisos do sócio (sininho) e preferências de canais. Rota: avisos.</summary>
public partial class AvisosViewModel(IClubeApi api) : ObservableObject
{
    private bool _carregandoPreferencias;

    public ObservableCollection<AvisoLinha> Itens { get; } = [];

    [ObservableProperty] public partial bool SemAvisos { get; set; }
    [ObservableProperty] public partial bool AvisosApp { get; set; } = true;
    [ObservableProperty] public partial bool Email { get; set; } = true;
    [ObservableProperty] public partial bool WhatsApp { get; set; } = true;
    [ObservableProperty] public partial bool Carregando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? Mensagem { get; set; }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Erro = null;
        Carregando = true;
        try
        {
            // Busca antes de limpar: duas cargas simultâneas (Appearing + RefreshView) não duplicam a lista.
            var carregados = await api.AvisosAsync();
            Itens.Clear();
            foreach (var a in carregados) Itens.Add(new AvisoLinha(a));
            SemAvisos = Itens.Count == 0;
            if (Itens.Any(a => !a.Dto.Lido)) await api.MarcarAvisosLidosAsync(); // abriu a central: tudo lido

            _carregandoPreferencias = true;
            var p = await api.PreferenciasComunicacaoAsync();
            AvisosApp = p.AvisosApp;
            Email = p.Email;
            WhatsApp = p.WhatsApp;
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
        finally
        {
            _carregandoPreferencias = false;
            Carregando = false;
        }
    }

    partial void OnAvisosAppChanged(bool value) => _ = SalvarPreferenciasAsync();
    partial void OnEmailChanged(bool value) => _ = SalvarPreferenciasAsync();
    partial void OnWhatsAppChanged(bool value) => _ = SalvarPreferenciasAsync();

    private async Task SalvarPreferenciasAsync()
    {
        if (_carregandoPreferencias) return;
        try
        {
            await api.SalvarPreferenciasComunicacaoAsync(new PreferenciasComunicacaoDto(AvisosApp, Email, WhatsApp));
            Mensagem = "Preferências salvas.";
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
    }
}

public sealed record DivulgacaoLinha(DivulgacaoDto Dto)
{
    public string Titulo => Dto.Titulo;
    public string Detalhe => $"{Dto.CriadoEm.ToLocalTime():dd/MM/yyyy HH:mm} · {Dto.CriadoPor} · " +
        (Dto.Status == "Concluida" ? "concluída" : "enviando…");
    public string Totais => string.Join("   ", Dto.Totais.Select(t =>
        $"{NomeCanal(t.Canal)}: {t.Enviados} enviado(s)" + (t.Falhas > 0 ? $", {t.Falhas} falha(s)" : "") +
        (t.Ignorados > 0 ? $", {t.Ignorados} não enviado(s)" : "")));

    public static string NomeCanal(string canal) => canal switch { "App" => "App", "Email" => "E-mail", _ => canal };
}

/// <summary>Comunicados da secretaria aos associados. Rota: comunicados.</summary>
public partial class ComunicadosViewModel(IClubeApi api) : ObservableObject
{
    public ObservableCollection<DivulgacaoLinha> Itens { get; } = [];

    [ObservableProperty] public partial string Titulo { get; set; } = string.Empty;
    [ObservableProperty] public partial string Texto { get; set; } = string.Empty;
    [ObservableProperty] public partial bool CanalApp { get; set; } = true;
    [ObservableProperty] public partial bool CanalEmail { get; set; } = true;
    [ObservableProperty] public partial bool CanalWhatsApp { get; set; }
    [ObservableProperty] public partial bool WhatsAppDisponivel { get; set; }
    [ObservableProperty] public partial string SituacaoCanais { get; set; } = string.Empty;
    [ObservableProperty] public partial bool Ocupado { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Erro = null;
        try
        {
            var canais = await api.CanaisComunicacaoAsync();
            WhatsAppDisponivel = canais.WhatsApp;
            CanalWhatsApp = CanalWhatsApp && canais.WhatsApp;
            SituacaoCanais = $"E-mail: {(canais.Email == "Smtp" ? "envio real" : $"modo {canais.Email} (teste, não sai de verdade)")}. " +
                             $"WhatsApp: {(canais.WhatsApp ? "ativo" : "não configurado")}" +
                             (canais.WhatsAppDetalhe is null ? "." : $" — {canais.WhatsAppDetalhe}");
            // Busca antes de limpar: duas cargas simultâneas (Appearing + RefreshView) não duplicam a lista.
            var carregados = await api.DivulgacoesAsync();
            Itens.Clear();
            foreach (var d in carregados) Itens.Add(new DivulgacaoLinha(d));
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
    }

    [RelayCommand]
    private async Task EnviarAsync()
    {
        var canais = new List<string>();
        if (CanalApp) canais.Add("App");
        if (CanalEmail) canais.Add("Email");
        if (CanalWhatsApp) canais.Add("WhatsApp");
        if (!await Shell.Current.DisplayAlertAsync("Enviar comunicado",
                $"Enviar \"{Titulo}\" a todos os associados ativos ({string.Join(", ", canais.Select(DivulgacaoLinha.NomeCanal))})? " +
                "Quem desligou um canal nas preferências não recebe por ele.", "Enviar", "Cancelar")) return;

        Erro = null;
        Ocupado = true;
        try
        {
            await api.CriarDivulgacaoAsync(new NovaDivulgacaoRequest(Titulo, Texto, canais));
            Titulo = Texto = string.Empty;
            await CarregarAsync();
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
    private async Task DetalharAsync(DivulgacaoLinha? d)
    {
        if (d is null) return;
        try
        {
            var lista = await api.EnviosNaoRecebidosAsync(d.Dto.Id);
            var texto = lista.Count == 0
                ? "Todos receberam pelos canais escolhidos."
                : string.Join("\n", lista.Take(40).Select(f => $"• {f.Socio} (título {f.NumeroTitulo}) — {DivulgacaoLinha.NomeCanal(f.Canal)}: {f.Detalhe}"))
                  + (lista.Count > 40 ? $"\n… e mais {lista.Count - 40}." : "");
            await Shell.Current.DisplayAlertAsync("Quem não recebeu", texto, "OK");
        }
        catch (Exception ex)
        {
            Erro = ErroApi.Mensagem(ex);
        }
    }
}
