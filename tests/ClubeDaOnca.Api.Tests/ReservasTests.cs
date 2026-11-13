using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClubeDaOnca.Contracts.Auth;
using ClubeDaOnca.Contracts.Reservas;
using ClubeDaOnca.Contracts.Socios;

namespace ClubeDaOnca.Api.Tests;

public class ReservasTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Daqui a 3 dias: longe da antecedência mínima e independente do fuso de quem roda os testes.
    private static readonly DateOnly Dia = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
    private static readonly TimeOnly Nove = new(9, 0);

    private async Task<HttpClient> ClienteAsync(string login, string senha)
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/auth/login", new LoginRequest(login, senha));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private Task<HttpClient> AdminAsync() => ClienteAsync(ApiFactory.AdminEmail, ApiFactory.AdminSenha);

    private async Task<(HttpClient Cliente, SocioDto Socio)> NovoSocioAsync(HttpClient admin)
    {
        var cpf = CpfTeste.Proximo();
        var resp = await admin.PostAsJsonAsync("/socios", new SalvarSocioRequest(
            $"Sócio {cpf[..3]}", cpf, $"r{cpf}@teste.local", null, new DateOnly(1980, 1, 1), "Individual"));
        resp.EnsureSuccessStatusCode();
        var criado = (await resp.Content.ReadFromJsonAsync<SocioCriadoResponse>())!;
        await SocioTeste.AtivarAsync(admin, criado.Socio.Id);
        return (await ClienteAsync(criado.Login, criado.SenhaProvisoria!), criado.Socio);
    }

    private static async Task<EspacoDto> NovoEspacoAsync(HttpClient admin, bool exigeAprovacao = false, int limite = 2)
    {
        var resp = await admin.PostAsJsonAsync("/espacos", new SalvarEspacoRequest(
            $"Espaço {Guid.NewGuid():N}"[..20], "Teste", true, new TimeOnly(8, 0), new TimeOnly(12, 0), 60,
            [0, 1, 2, 3, 4, 5, 6], 1, 30, limite, 50m, exigeAprovacao, 24));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<EspacoDto>())!;
    }

    private static Task<HttpResponseMessage> ReservarAsync(HttpClient c, Guid espacoId, TimeOnly inicio) =>
        c.PostAsJsonAsync("/reservas", new CriarReservaRequest(espacoId, Dia, inicio, "Aniversário"));

    [Fact]
    public async Task Reserva_confirmada_ocupa_o_horario_na_agenda()
    {
        var admin = await AdminAsync();
        var espaco = await NovoEspacoAsync(admin);
        var (socio, _) = await NovoSocioAsync(admin);

        var agenda = await socio.GetFromJsonAsync<AgendaDto>($"/espacos/{espaco.Id}/agenda?data={Dia:yyyy-MM-dd}");
        Assert.Equal(4, agenda!.Horarios.Count);
        Assert.All(agenda.Horarios, h => Assert.Equal("Livre", h.Situacao));

        var resp = await ReservarAsync(socio, espaco.Id, Nove);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var reserva = (await resp.Content.ReadFromJsonAsync<ReservaDto>())!;
        Assert.Equal("Confirmada", reserva.Status);
        Assert.Equal(new TimeOnly(10, 0), reserva.Fim);
        Assert.Equal(50m, reserva.Taxa);
        Assert.True(reserva.PodeCancelar);

        agenda = await socio.GetFromJsonAsync<AgendaDto>($"/espacos/{espaco.Id}/agenda?data={Dia:yyyy-MM-dd}");
        Assert.Equal("Ocupado", agenda!.Horarios.Single(h => h.Inicio == Nove).Situacao);

        var minhas = await socio.GetFromJsonAsync<List<ReservaDto>>("/reservas/minhas");
        Assert.Single(minhas!);
    }

    [Fact]
    public async Task Horario_ocupado_e_limite_por_socio_sao_respeitados()
    {
        var admin = await AdminAsync();
        var espaco = await NovoEspacoAsync(admin, limite: 1);
        var (socio1, _) = await NovoSocioAsync(admin);
        var (socio2, _) = await NovoSocioAsync(admin);

        Assert.Equal(HttpStatusCode.Created, (await ReservarAsync(socio1, espaco.Id, Nove)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ReservarAsync(socio2, espaco.Id, Nove)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ReservarAsync(socio1, espaco.Id, new TimeOnly(10, 0)))!.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ReservarAsync(socio2, espaco.Id, new TimeOnly(9, 30))).StatusCode);
    }

    [Fact]
    public async Task Pedidos_simultaneos_no_mesmo_horario_geram_uma_unica_reserva()
    {
        var admin = await AdminAsync();
        var espaco = await NovoEspacoAsync(admin);
        var (socio1, _) = await NovoSocioAsync(admin);
        var (socio2, _) = await NovoSocioAsync(admin);

        var respostas = await Task.WhenAll(ReservarAsync(socio1, espaco.Id, Nove), ReservarAsync(socio2, espaco.Id, Nove));
        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.Created);

        var lista = await admin.GetFromJsonAsync<List<ReservaDto>>($"/reservas?data={Dia:yyyy-MM-dd}");
        Assert.Single(lista!, r => r.EspacoId == espaco.Id);
    }

    [Fact]
    public async Task Espaco_com_aprovacao_gera_pendente_que_a_secretaria_aprova_ou_recusa()
    {
        var admin = await AdminAsync();
        var espaco = await NovoEspacoAsync(admin, exigeAprovacao: true);
        var (socio1, _) = await NovoSocioAsync(admin);
        var (socio2, _) = await NovoSocioAsync(admin);

        var r1 = (await (await ReservarAsync(socio1, espaco.Id, Nove)).Content.ReadFromJsonAsync<ReservaDto>())!;
        var r2 = (await (await ReservarAsync(socio2, espaco.Id, new TimeOnly(10, 0))).Content.ReadFromJsonAsync<ReservaDto>())!;
        Assert.Equal("Pendente", r1.Status);

        var pendentes = await admin.GetFromJsonAsync<List<ReservaDto>>("/reservas?status=Pendente");
        Assert.Contains(pendentes!, r => r.Id == r1.Id && r.NumeroTitulo > 0);

        var aprovada = await (await admin.PostAsync($"/reservas/{r1.Id}/aprovar", null)).Content.ReadFromJsonAsync<ReservaDto>();
        Assert.Equal("Confirmada", aprovada!.Status);

        var semMotivo = await admin.PostAsJsonAsync($"/reservas/{r2.Id}/recusar", new RecusarReservaRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, semMotivo.StatusCode);
        var recusada = await (await admin.PostAsJsonAsync($"/reservas/{r2.Id}/recusar", new RecusarReservaRequest("Data reservada para evento do clube")))
            .Content.ReadFromJsonAsync<ReservaDto>();
        Assert.Equal("Recusada", recusada!.Status);

        // Recusada libera o horário.
        var agenda = await socio1.GetFromJsonAsync<AgendaDto>($"/espacos/{espaco.Id}/agenda?data={Dia:yyyy-MM-dd}");
        Assert.Equal("Livre", agenda!.Horarios.Single(h => h.Inicio == new TimeOnly(10, 0)).Situacao);
    }

    [Fact]
    public async Task Socio_cancela_a_propria_reserva_mas_nao_a_de_outro()
    {
        var admin = await AdminAsync();
        var espaco = await NovoEspacoAsync(admin);
        var (socio1, _) = await NovoSocioAsync(admin);
        var (socio2, _) = await NovoSocioAsync(admin);

        var reserva = (await (await ReservarAsync(socio1, espaco.Id, Nove)).Content.ReadFromJsonAsync<ReservaDto>())!;

        Assert.Equal(HttpStatusCode.NotFound, (await socio2.PostAsync($"/reservas/{reserva.Id}/cancelar", null)).StatusCode);

        var cancelada = await (await socio1.PostAsync($"/reservas/{reserva.Id}/cancelar", null)).Content.ReadFromJsonAsync<ReservaDto>();
        Assert.Equal("Cancelada", cancelada!.Status);

        // O horário volta a ficar livre para outro sócio.
        Assert.Equal(HttpStatusCode.Created, (await ReservarAsync(socio2, espaco.Id, Nove)).StatusCode);
    }

    [Fact]
    public async Task Socio_suspenso_nao_reserva_e_socio_nao_cadastra_espaco()
    {
        var admin = await AdminAsync();
        var espaco = await NovoEspacoAsync(admin);
        var (socio, dados) = await NovoSocioAsync(admin);

        var criarEspaco = await socio.PostAsJsonAsync("/espacos", new SalvarEspacoRequest(
            "Piscina", null, true, new TimeOnly(8, 0), new TimeOnly(18, 0), 60, [6], 1, 7, 1, 0, false, 1));
        Assert.Equal(HttpStatusCode.Forbidden, criarEspaco.StatusCode);

        await admin.PatchAsJsonAsync($"/socios/{dados.Id}/situacao", new AlterarSituacaoRequest("Suspenso"));
        Assert.Equal(HttpStatusCode.BadRequest, (await ReservarAsync(socio, espaco.Id, Nove)).StatusCode);
    }

    [Fact]
    public async Task Espaco_inativo_some_da_lista_do_socio()
    {
        var admin = await AdminAsync();
        var espaco = await NovoEspacoAsync(admin);
        var (socio, _) = await NovoSocioAsync(admin);

        var req = new SalvarEspacoRequest(espaco.Nome, null, false, espaco.Abertura, espaco.Fechamento, espaco.DuracaoMinutos,
            espaco.DiasSemana, 1, 30, 2, 0, false, 24);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/espacos/{espaco.Id}", req)).StatusCode);

        Assert.DoesNotContain(await socio.GetFromJsonAsync<List<EspacoDto>>("/espacos") ?? [], e => e.Id == espaco.Id);
        Assert.Contains(await admin.GetFromJsonAsync<List<EspacoDto>>("/espacos?todos=true") ?? [], e => e.Id == espaco.Id);
    }
}
