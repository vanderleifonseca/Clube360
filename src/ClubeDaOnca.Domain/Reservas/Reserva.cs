using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Socios;

namespace ClubeDaOnca.Domain.Reservas;

public enum StatusReserva
{
    Pendente = 1,
    Confirmada = 2,
    Cancelada = 3,
    Recusada = 4,
}

/// <summary>Tipo do local (para organizar a lista e os eventos).</summary>
public enum TipoLocal
{
    Outro = 0,
    Quadra = 1,
    Quiosque = 2,
    Churrasqueira = 3,
    Salao = 4,
    Piscina = 5,
    Campo = 6,
    Auditorio = 7,
}

/// <summary>Espaço reservável (quadra, churrasqueira, salão) com as regras de uso.</summary>
public sealed class Espaco : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;
    public TipoLocal Tipo { get; set; }

    /// <summary>Capacidade de pessoas do local (0 = não informada).</summary>
    public int Capacidade { get; set; }

    public TimeOnly Abertura { get; set; } = new(8, 0);
    public TimeOnly Fechamento { get; set; } = new(22, 0);

    /// <summary>Duração de cada horário (uma reserva ocupa um horário).</summary>
    public int DuracaoMinutos { get; set; } = 60;

    /// <summary>Dias de funcionamento (0 = domingo ... 6 = sábado).</summary>
    public int[] DiasSemana { get; set; } = [0, 1, 2, 3, 4, 5, 6];

    public int AntecedenciaMinimaHoras { get; set; } = 2;
    public int AntecedenciaMaximaDias { get; set; } = 30;

    /// <summary>Máximo de reservas futuras (pendentes ou confirmadas) por título neste espaço.</summary>
    public int LimiteReservasAtivas { get; set; } = 2;

    public decimal Taxa { get; set; }
    public bool ExigeAprovacao { get; set; }

    /// <summary>Até quantas horas antes do início o sócio pode cancelar uma reserva confirmada.</summary>
    public int PrazoCancelamentoHoras { get; set; } = 24;

    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Reserva : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }
    public Guid EspacoId { get; set; }
    public Espaco? Espaco { get; set; }
    public Guid SocioId { get; set; }
    public Socio? Socio { get; set; }

    /// <summary>Data e horários no fuso do clube.</summary>
    public DateOnly Data { get; set; }
    public TimeOnly Inicio { get; set; }
    public TimeOnly Fim { get; set; }

    public StatusReserva Status { get; set; } = StatusReserva.Pendente;

    /// <summary>Taxa cobrada, copiada do espaço no momento da reserva.</summary>
    public decimal Taxa { get; set; }

    public string? Observacao { get; set; }
    public string? MotivoRecusa { get; set; }
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AtualizadoEm { get; set; }

    public bool Ativa => Status is StatusReserva.Pendente or StatusReserva.Confirmada;
    public DateTime InicioLocal => Data.ToDateTime(Inicio);
}
