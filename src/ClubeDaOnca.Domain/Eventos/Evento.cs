using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Reservas;
using ClubeDaOnca.Domain.Socios;

namespace ClubeDaOnca.Domain.Eventos;

public enum StatusEvento
{
    /// <summary>Em preparação: já ocupa a agenda dos locais, mas os sócios ainda não veem.</summary>
    Rascunho = 1,

    /// <summary>Visível no app e aberto para reservas.</summary>
    Publicado = 2,

    Cancelado = 3,
}

public enum StatusReservaEvento
{
    Confirmada = 1,
    Cancelada = 2,
}

/// <summary>Evento do clube (festa, torneio, show) em um ou mais locais, com ambientes reserváveis.</summary>
public sealed class Evento : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    /// <summary>Início e término no fuso do clube.</summary>
    public DateTime Inicio { get; set; }
    public DateTime Termino { get; set; }

    public StatusEvento Status { get; set; } = StatusEvento.Rascunho;

    /// <summary>Total de pessoas no evento (não pode passar da capacidade somada dos locais).</summary>
    public int Capacidade { get; set; }

    /// <summary>Sócios podem levar convidados e o evento pode ser divulgado nas redes sociais.</summary>
    public bool AbertoAoPublico { get; set; }

    /// <summary>Divulgar aos associados (app, e-mail, WhatsApp — envio na fase de divulgação).</summary>
    public bool DivulgarAssociados { get; set; } = true;

    /// <summary>Divulgar nas redes sociais (só para eventos abertos ao público).</summary>
    public bool DivulgarRedesSociais { get; set; }

    /// <summary>Até quantas horas antes do início o sócio pode cancelar a própria reserva.</summary>
    public int PrazoCancelamentoHoras { get; set; } = 24;

    public string? MotivoCancelamento { get; set; }
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    public List<EventoLocal> Locais { get; set; } = [];
    public List<AmbienteEvento> Ambientes { get; set; } = [];

    public bool OcupaAgenda => Status != StatusEvento.Cancelado;
}

/// <summary>Local (espaço) usado pelo evento: fica bloqueado na agenda durante o evento.</summary>
public sealed class EventoLocal
{
    public Guid EventoId { get; set; }
    public Guid EspacoId { get; set; }
    public Espaco? Espaco { get; set; }
}

/// <summary>Tipo de ambiente reservável dentro do evento (ex.: "Mesa para 8" com 40 vagas, "Quiosque" com 6 vagas).</summary>
public sealed class AmbienteEvento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventoId { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Máximo de pessoas por reserva deste ambiente.</summary>
    public int PessoasPorReserva { get; set; } = 1;

    /// <summary>Quantidade de reservas disponíveis (limite de reservas do tipo de ambiente).</summary>
    public int Vagas { get; set; }

    /// <summary>Quantas reservas cada título pode fazer neste ambiente.</summary>
    public int LimitePorSocio { get; set; } = 1;

    public decimal Valor { get; set; }

    /// <summary>Reservas confirmadas (controle de concorrência: duas pessoas não pegam a última vaga).</summary>
    public int Reservadas { get; set; }
}

/// <summary>Reserva de um sócio em um ambiente do evento.</summary>
public sealed class ReservaEvento : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }
    public Guid EventoId { get; set; }
    public Evento? Evento { get; set; }
    public Guid AmbienteId { get; set; }
    public AmbienteEvento? Ambiente { get; set; }
    public Guid SocioId { get; set; }
    public Socio? Socio { get; set; }

    public int Pessoas { get; set; }
    public decimal Valor { get; set; }
    public string? Observacao { get; set; }
    public StatusReservaEvento Status { get; set; } = StatusReservaEvento.Confirmada;
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CanceladaEm { get; set; }
}

/// <summary>Ocupação de um local num período (reserva comum ou outro evento), para checar a agenda.</summary>
public sealed record OcupacaoLocal(Guid EspacoId, string Espaco, DateTime Inicio, DateTime Fim, string Descricao);

public static class RegrasEvento
{
    public static bool Sobrepoe(DateTime inicioA, DateTime fimA, DateTime inicioB, DateTime fimB) =>
        inicioA < fimB && inicioB < fimA;

    /// <summary>Período de uma reserva comum (o fim 23:59 representa a meia-noite).</summary>
    public static (DateTime Inicio, DateTime Fim) Periodo(Reserva r)
    {
        var inicio = r.Data.ToDateTime(r.Inicio);
        var fim = r.Fim == new TimeOnly(23, 59) ? r.Data.AddDays(1).ToDateTime(TimeOnly.MinValue) : r.Data.ToDateTime(r.Fim);
        if (fim <= inicio) fim = fim.AddDays(1); // atravessa a meia-noite
        return (inicio, fim);
    }

    public static Dictionary<string, string[]> ValidarCadastro(Evento e, IReadOnlyCollection<Espaco> locais, DateTime agoraLocal, bool novo)
    {
        var erros = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(e.Titulo) || e.Titulo.Trim().Length < 3) erros["titulo"] = ["Informe o título do evento."];
        if (e.Termino <= e.Inicio) erros["termino"] = ["O término deve ser depois do início."];
        else if (e.Termino - e.Inicio > TimeSpan.FromDays(31)) erros["termino"] = ["O evento pode durar no máximo 31 dias."];
        if (novo && e.Inicio < agoraLocal) erros["inicio"] = ["O início não pode estar no passado."];
        if (locais.Count == 0) erros["locais"] = ["Escolha pelo menos um local."];
        if (e.Capacidade <= 0) erros["capacidade"] = ["Informe a capacidade de pessoas do evento."];

        var capacidadeLocais = locais.Sum(l => l.Capacidade);
        if (locais.Count > 0 && locais.All(l => l.Capacidade > 0) && e.Capacidade > capacidadeLocais)
            erros["capacidade"] = [$"A capacidade do evento ({e.Capacidade}) passa da capacidade dos locais ({capacidadeLocais} pessoas)."];

        if (e.DivulgarRedesSociais && !e.AbertoAoPublico)
            erros["divulgarRedesSociais"] = ["Só eventos abertos ao público podem ser divulgados nas redes sociais."];
        if (e.PrazoCancelamentoHoras is < 0 or > 720) erros["prazoCancelamentoHoras"] = ["Prazo de cancelamento inválido."];

        var nomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in e.Ambientes)
        {
            if (string.IsNullOrWhiteSpace(a.Nome)) { erros["ambientes"] = ["Informe o nome de cada ambiente."]; break; }
            if (!nomes.Add(a.Nome.Trim())) { erros["ambientes"] = [$"Ambiente repetido: {a.Nome}."]; break; }
            if (a.Vagas <= 0 || a.PessoasPorReserva <= 0 || a.LimitePorSocio <= 0 || a.Valor < 0)
            { erros["ambientes"] = [$"Ambiente \"{a.Nome}\": vagas, pessoas por reserva e limite por sócio devem ser maiores que zero."]; break; }
            if (a.Vagas < a.Reservadas)
            { erros["ambientes"] = [$"Ambiente \"{a.Nome}\" já tem {a.Reservadas} reserva(s): as vagas não podem ser menores."]; break; }
        }
        return erros;
    }

    /// <summary>Mensagem de erro da nova reserva, ou nulo se pode reservar.</summary>
    public static string? ValidarReserva(Evento e, AmbienteEvento ambiente, int pessoas, int reservasDoSocioNoAmbiente,
        int pessoasJaReservadasNoEvento, DateTime agoraLocal)
    {
        if (e.Status != StatusEvento.Publicado) return "Este evento não está aberto para reservas.";
        if (e.Inicio <= agoraLocal) return "O evento já começou.";
        if (pessoas <= 0) return "Informe quantas pessoas vão.";
        if (pessoas > ambiente.PessoasPorReserva) return $"Cada reserva de \"{ambiente.Nome}\" é para até {ambiente.PessoasPorReserva} pessoa(s).";
        if (ambiente.Reservadas >= ambiente.Vagas) return $"Não há mais vagas em \"{ambiente.Nome}\".";
        if (reservasDoSocioNoAmbiente >= ambiente.LimitePorSocio)
            return $"Limite de {ambiente.LimitePorSocio} reserva(s) por título em \"{ambiente.Nome}\".";
        if (pessoasJaReservadasNoEvento + pessoas > e.Capacidade)
            return $"O evento atingiu a capacidade de {e.Capacidade} pessoas.";
        return null;
    }

    public static string? ValidarCancelamentoPeloSocio(Evento e, ReservaEvento r, DateTime agoraLocal)
    {
        if (r.Status != StatusReservaEvento.Confirmada) return "Esta reserva já foi cancelada.";
        if (e.Inicio <= agoraLocal) return "O evento já começou.";
        if (e.Inicio - agoraLocal < TimeSpan.FromHours(e.PrazoCancelamentoHoras))
            return $"O cancelamento só é permitido até {e.PrazoCancelamentoHoras} h antes do evento. Fale com a secretaria.";
        return null;
    }
}
