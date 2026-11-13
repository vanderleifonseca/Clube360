using ClubeDaOnca.Domain.Reservas;

namespace ClubeDaOnca.Domain.Tests;

public class RegrasReservaTests
{
    // Domingo, 08/11/2026, 10h no fuso do clube.
    private static readonly DateTime Agora = new(2026, 11, 8, 10, 0, 0);
    private static readonly DateOnly Amanha = new(2026, 11, 9);

    private static Espaco Quadra() => new()
    {
        Nome = "Quadra",
        Abertura = new TimeOnly(8, 0),
        Fechamento = new TimeOnly(12, 0),
        DuracaoMinutos = 60,
        DiasSemana = [1, 2, 3, 4, 5], // segunda a sexta
        AntecedenciaMinimaHoras = 2,
        AntecedenciaMaximaDias = 15,
        LimiteReservasAtivas = 2,
        PrazoCancelamentoHoras = 24,
    };

    [Fact]
    public void Gera_horarios_do_dia_conforme_funcionamento()
    {
        var horarios = RegrasReserva.HorariosDoDia(Quadra(), Amanha);
        Assert.Equal(4, horarios.Count);
        Assert.Equal(new TimeOnly(8, 0), horarios[0].Inicio);
        Assert.Equal(new TimeOnly(12, 0), horarios[^1].Fim);
    }

    [Fact]
    public void Sem_horarios_em_dia_fechado_ou_espaco_inativo()
    {
        Assert.Empty(RegrasReserva.HorariosDoDia(Quadra(), new DateOnly(2026, 11, 8))); // domingo
        var inativa = Quadra();
        inativa.Ativo = false;
        Assert.Empty(RegrasReserva.HorariosDoDia(inativa, Amanha));
    }

    [Fact]
    public void Fechamento_a_meia_noite_gera_ultimo_horario()
    {
        var salao = new Espaco { Abertura = new TimeOnly(18, 0), Fechamento = new TimeOnly(0, 0), DuracaoMinutos = 360 };
        var horarios = RegrasReserva.HorariosDoDia(salao, Amanha);
        Assert.Single(horarios);
        Assert.Equal(new TimeOnly(18, 0), horarios[0].Inicio);
    }

    [Fact]
    public void Reserva_valida() =>
        Assert.Null(RegrasReserva.ValidarNovaReserva(Quadra(), Amanha, new TimeOnly(9, 0), Agora, 0, false));

    [Theory]
    [InlineData(9, 30, "fora do funcionamento")]  // não coincide com o início de um horário
    [InlineData(13, 0, "fora do funcionamento")]
    public void Rejeita_horario_invalido(int hora, int minuto, string trecho) =>
        Assert.Contains(trecho, RegrasReserva.ValidarNovaReserva(Quadra(), Amanha, new TimeOnly(hora, minuto), Agora, 0, false));

    [Fact]
    public void Respeita_antecedencia_minima_e_maxima()
    {
        var espaco = Quadra();
        espaco.DiasSemana = [0, 1, 2, 3, 4, 5, 6];
        var hoje = DateOnly.FromDateTime(Agora);

        Assert.Contains("antecedência", RegrasReserva.ValidarNovaReserva(espaco, hoje, new TimeOnly(11, 0), Agora, 0, false));
        Assert.Contains("até 15 dias", RegrasReserva.ValidarNovaReserva(espaco, hoje.AddDays(20), new TimeOnly(9, 0), Agora, 0, false));
    }

    [Fact]
    public void Rejeita_horario_ocupado_e_limite_atingido()
    {
        Assert.Contains("já está reservado", RegrasReserva.ValidarNovaReserva(Quadra(), Amanha, new TimeOnly(9, 0), Agora, 0, true));
        Assert.Contains("Limite", RegrasReserva.ValidarNovaReserva(Quadra(), Amanha, new TimeOnly(9, 0), Agora, 2, false));
    }

    [Fact]
    public void Situacao_dos_horarios_na_agenda()
    {
        var espaco = Quadra();
        var h = new Horario(new TimeOnly(9, 0), new TimeOnly(10, 0));
        Assert.Equal(SituacaoHorario.Livre, RegrasReserva.Situacao(espaco, Amanha, h, Agora, false));
        Assert.Equal(SituacaoHorario.Ocupado, RegrasReserva.Situacao(espaco, Amanha, h, Agora, true));
        Assert.Equal(SituacaoHorario.Indisponivel, RegrasReserva.Situacao(espaco, Amanha, h, Amanha.ToDateTime(new TimeOnly(8, 0)), false));
    }

    [Fact]
    public void Cancelamento_respeita_prazo_para_confirmadas()
    {
        var espaco = Quadra();
        var reserva = new Reserva { Data = Amanha, Inicio = new TimeOnly(9, 0), Status = StatusReserva.Confirmada };

        // Faltam 23 h: fora do prazo de 24 h.
        Assert.NotNull(RegrasReserva.ValidarCancelamentoPeloSocio(espaco, reserva, Agora));
        Assert.Null(RegrasReserva.ValidarCancelamentoPeloSocio(espaco, reserva, Agora.AddHours(-2)));

        // Pendente pode ser cancelada a qualquer momento antes do início.
        reserva.Status = StatusReserva.Pendente;
        Assert.Null(RegrasReserva.ValidarCancelamentoPeloSocio(espaco, reserva, Agora));

        reserva.Status = StatusReserva.Cancelada;
        Assert.NotNull(RegrasReserva.ValidarCancelamentoPeloSocio(espaco, reserva, Agora.AddHours(-2)));
    }
}
