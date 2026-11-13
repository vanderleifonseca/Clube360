namespace ClubeDaOnca.Domain.Reservas;

public sealed record Horario(TimeOnly Inicio, TimeOnly Fim);

public enum SituacaoHorario
{
    Livre,
    Ocupado,

    /// <summary>Já passou ou está fora da antecedência mínima/máxima.</summary>
    Indisponivel,
}

/// <summary>Regras de reserva. Os horários são sempre no fuso do clube (<c>agoraLocal</c>).</summary>
public static class RegrasReserva
{
    public static IReadOnlyList<Horario> HorariosDoDia(Espaco espaco, DateOnly data)
    {
        if (!espaco.Ativo || espaco.DuracaoMinutos <= 0 || !espaco.DiasSemana.Contains((int)data.DayOfWeek))
            return [];

        var lista = new List<Horario>();
        var abertura = (int)espaco.Abertura.ToTimeSpan().TotalMinutes;
        var fechamento = (int)espaco.Fechamento.ToTimeSpan().TotalMinutes;
        if (fechamento == 0) fechamento = 24 * 60; // fechamento à meia-noite

        for (var m = abertura; m + espaco.DuracaoMinutos <= fechamento; m += espaco.DuracaoMinutos)
        {
            var fim = m + espaco.DuracaoMinutos;
            lista.Add(new Horario(Hora(m), Hora(fim == 24 * 60 ? 24 * 60 - 1 : fim)));
        }
        return lista;
    }

    public static SituacaoHorario Situacao(Espaco espaco, DateOnly data, Horario horario, DateTime agoraLocal, bool ocupado)
    {
        if (ocupado) return SituacaoHorario.Ocupado;
        return ForaDaAntecedencia(espaco, data, horario.Inicio, agoraLocal) is null
            ? SituacaoHorario.Livre
            : SituacaoHorario.Indisponivel;
    }

    /// <summary>Devolve a mensagem de erro, ou nulo se a reserva pode ser feita.</summary>
    public static string? ValidarNovaReserva(Espaco espaco, DateOnly data, TimeOnly inicio, DateTime agoraLocal,
        int reservasAtivasDoTitulo, bool horarioOcupado)
    {
        if (!espaco.Ativo)
            return "Este espaço não está disponível para reservas.";
        if (!HorariosDoDia(espaco, data).Any(h => h.Inicio == inicio))
            return "Horário fora do funcionamento do espaço.";
        if (ForaDaAntecedencia(espaco, data, inicio, agoraLocal) is { } erro)
            return erro;
        if (horarioOcupado)
            return "Este horário já está reservado.";
        if (reservasAtivasDoTitulo >= espaco.LimiteReservasAtivas)
            return $"Limite de {espaco.LimiteReservasAtivas} reserva(s) ativa(s) neste espaço atingido.";
        return null;
    }

    /// <summary>O sócio cancela pendentes a qualquer momento e confirmadas até o prazo do espaço.</summary>
    public static string? ValidarCancelamentoPeloSocio(Espaco espaco, Reserva reserva, DateTime agoraLocal)
    {
        if (!reserva.Ativa)
            return "Esta reserva não está ativa.";
        if (reserva.InicioLocal <= agoraLocal)
            return "A reserva já começou ou terminou.";
        if (reserva.Status == StatusReserva.Confirmada
            && reserva.InicioLocal - agoraLocal < TimeSpan.FromHours(espaco.PrazoCancelamentoHoras))
            return $"O cancelamento só é permitido até {espaco.PrazoCancelamentoHoras} h antes. Fale com a secretaria.";
        return null;
    }

    private static string? ForaDaAntecedencia(Espaco espaco, DateOnly data, TimeOnly inicio, DateTime agoraLocal)
    {
        var inicioLocal = data.ToDateTime(inicio);
        if (inicioLocal < agoraLocal.AddHours(espaco.AntecedenciaMinimaHoras))
            return espaco.AntecedenciaMinimaHoras > 0
                ? $"Reservas precisam de pelo menos {espaco.AntecedenciaMinimaHoras} h de antecedência."
                : "Este horário já passou.";
        if (data > DateOnly.FromDateTime(agoraLocal).AddDays(espaco.AntecedenciaMaximaDias))
            return $"Reservas podem ser feitas com até {espaco.AntecedenciaMaximaDias} dias de antecedência.";
        return null;
    }

    private static TimeOnly Hora(int minutos) => new(minutos / 60, minutos % 60);
}
