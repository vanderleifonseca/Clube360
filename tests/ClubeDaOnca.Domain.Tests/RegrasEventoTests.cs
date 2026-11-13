using ClubeDaOnca.Domain.Eventos;
using ClubeDaOnca.Domain.Reservas;

namespace ClubeDaOnca.Domain.Tests;

public class RegrasEventoTests
{
    private static readonly DateTime Agora = new(2026, 11, 11, 10, 0, 0);

    private static Evento Evento(int capacidade = 100) => new()
    {
        Titulo = "Festa junina", Inicio = new DateTime(2026, 12, 5, 18, 0, 0), Termino = new DateTime(2026, 12, 5, 23, 0, 0),
        Capacidade = capacidade, Status = StatusEvento.Publicado, PrazoCancelamentoHoras = 24,
        Ambientes = [new AmbienteEvento { Nome = "Mesa", PessoasPorReserva = 8, Vagas = 2, LimitePorSocio = 1 }],
    };

    [Fact]
    public void Sobreposicao_de_periodos()
    {
        var ini = new DateTime(2026, 12, 5, 18, 0, 0);
        var fim = ini.AddHours(5);
        Assert.True(RegrasEvento.Sobrepoe(ini, fim, ini.AddHours(4), ini.AddHours(6)));
        Assert.False(RegrasEvento.Sobrepoe(ini, fim, fim, fim.AddHours(1))); // encosta, não sobrepõe
    }

    [Fact]
    public void Periodo_da_reserva_que_termina_a_meia_noite()
    {
        var r = new Reserva { Data = new DateOnly(2026, 12, 5), Inicio = new TimeOnly(18, 0), Fim = new TimeOnly(23, 59) };
        var (ini, fim) = RegrasEvento.Periodo(r);
        Assert.Equal(new DateTime(2026, 12, 5, 18, 0, 0), ini);
        Assert.Equal(new DateTime(2026, 12, 6, 0, 0, 0), fim);
    }

    [Fact]
    public void Capacidade_nao_passa_da_dos_locais_e_redes_sociais_so_se_aberto()
    {
        var e = Evento(capacidade: 300);
        e.DivulgarRedesSociais = true;
        var locais = new[] { new Espaco { Nome = "Salão", Capacidade = 120 }, new Espaco { Nome = "Quadra", Capacidade = 100 } };
        var erros = RegrasEvento.ValidarCadastro(e, locais, Agora, novo: true);
        Assert.Contains("capacidade", erros.Keys);
        Assert.Contains("divulgarRedesSociais", erros.Keys);

        e.Capacidade = 220;
        e.AbertoAoPublico = true;
        Assert.Empty(RegrasEvento.ValidarCadastro(e, locais, Agora, novo: true));
    }

    [Fact]
    public void Reserva_respeita_vagas_limite_pessoas_e_capacidade()
    {
        var e = Evento(capacidade: 10);
        var mesa = e.Ambientes[0];
        Assert.Null(RegrasEvento.ValidarReserva(e, mesa, 8, 0, 0, Agora));
        Assert.Contains("até 8", RegrasEvento.ValidarReserva(e, mesa, 9, 0, 0, Agora));
        Assert.Contains("Limite", RegrasEvento.ValidarReserva(e, mesa, 2, 1, 0, Agora));
        Assert.Contains("capacidade", RegrasEvento.ValidarReserva(e, mesa, 4, 0, 8, Agora));
        mesa.Reservadas = 2;
        Assert.Contains("Não há mais vagas", RegrasEvento.ValidarReserva(e, mesa, 2, 0, 0, Agora));
        e.Status = StatusEvento.Rascunho;
        Assert.Contains("não está aberto", RegrasEvento.ValidarReserva(e, mesa, 2, 0, 0, Agora));
    }

    [Fact]
    public void Cancelamento_pelo_socio_respeita_o_prazo()
    {
        var e = Evento();
        var r = new ReservaEvento { Status = StatusReservaEvento.Confirmada };
        Assert.Null(RegrasEvento.ValidarCancelamentoPeloSocio(e, r, Agora));
        Assert.NotNull(RegrasEvento.ValidarCancelamentoPeloSocio(e, r, e.Inicio.AddHours(-2)));
    }
}
