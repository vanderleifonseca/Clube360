using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClubeDaOnca.Contracts.Associacoes;
using ClubeDaOnca.Contracts.Auth;
using ClubeDaOnca.Contracts.Eventos;
using ClubeDaOnca.Contracts.Financeiro;
using ClubeDaOnca.Contracts.Reservas;
using ClubeDaOnca.Contracts.Socios;

namespace ClubeDaOnca.Api.Tests;

public class EventosTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Unidade(HttpClient Admin, HttpClient Socio, HttpClient Socio2);

    private async Task<HttpClient> ClienteAsync(string login, string senha)
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/auth/login", new LoginRequest(login, senha));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<HttpClient> SocioAtivoAsync(EventosTests t, HttpClient admin)
    {
        var cpf = CpfTeste.Proximo();
        var resp = await admin.PostAsJsonAsync("/socios", new SalvarSocioRequest(
            $"Sócio {cpf[..4]}", cpf, $"ev{cpf}@teste.local", null, new DateOnly(1985, 1, 1), "Individual"));
        resp.EnsureSuccessStatusCode();
        var criado = (await resp.Content.ReadFromJsonAsync<SocioCriadoResponse>())!;
        await SocioTeste.AtivarAsync(admin, criado.Socio.Id);
        return await t.ClienteAsync(criado.Login, criado.SenhaProvisoria!);
    }

    /// <summary>Unidade nova (isolada dos outros testes), com dois sócios ativos.</summary>
    private async Task<Unidade> UnidadeAsync()
    {
        var super = await ClienteAsync(ApiFactory.AdminEmail, ApiFactory.AdminSenha);
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var resp = await super.PostAsJsonAsync("/associacoes", new NovaAssociacaoRequest(
            $"Clube Ev {sufixo}", null, null, null, null, "Admin", $"ev{sufixo}@teste.local"));
        var u = (await resp.Content.ReadFromJsonAsync<AssociacaoCriadaResponse>())!;
        var admin = await ClienteAsync(u.AdminLogin, u.SenhaProvisoria);
        return new Unidade(admin, await SocioAtivoAsync(this, admin), await SocioAtivoAsync(this, admin));
    }

    private static async Task<EspacoDto> LocalAsync(HttpClient admin, string nome, int capacidade = 100)
    {
        var resp = await admin.PostAsJsonAsync("/espacos", new SalvarEspacoRequest(
            nome, null, true, new TimeOnly(8, 0), new TimeOnly(0, 0), 60, [0, 1, 2, 3, 4, 5, 6], 0, 60, 5, 0m, false, 0,
            "Salao", capacidade));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<EspacoDto>())!;
    }

    private static DateTime Dia(int dias, int hora) => DateTime.Today.AddDays(dias).AddHours(hora);

    private static SalvarEventoRequest Evento(Guid local, DateTime inicio, DateTime termino, int capacidade = 50,
        params SalvarAmbienteRequest[] ambientes) => new(
        "Festa da Onça", "Música ao vivo", inicio, termino, capacidade, true, true, true, 24, [local],
        ambientes.Length > 0 ? ambientes : [new SalvarAmbienteRequest(null, "Mesa para 4", 4, 2, 1, 0m)]);

    [Fact]
    public async Task Evento_bloqueado_por_reserva_comum_no_horario()
    {
        var u = await UnidadeAsync();
        var salao = await LocalAsync(u.Admin, "Salão de festas");
        var dia = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        (await u.Socio.PostAsJsonAsync("/reservas", new CriarReservaRequest(salao.Id, dia, new TimeOnly(19, 0), null))).EnsureSuccessStatusCode();

        var disp = (await (await u.Admin.PostAsJsonAsync("/eventos/disponibilidade",
            new VerificarAgendaRequest([salao.Id], Dia(5, 18), Dia(5, 23)))).Content.ReadFromJsonAsync<DisponibilidadeDto>())!;
        Assert.False(disp.Disponivel);
        Assert.Contains(disp.Conflitos, c => c.Descricao.Contains("Reserva de"));

        var resp = await u.Admin.PostAsJsonAsync("/eventos", Evento(salao.Id, Dia(5, 18), Dia(5, 23)));
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("conflitos", await resp.Content.ReadAsStringAsync());

        // Em outro horário, livre.
        Assert.Equal(HttpStatusCode.Created, (await u.Admin.PostAsJsonAsync("/eventos", Evento(salao.Id, Dia(5, 8), Dia(5, 12)))).StatusCode);
    }

    [Fact]
    public async Task Evento_ocupa_a_agenda_do_local()
    {
        var u = await UnidadeAsync();
        var quadra = await LocalAsync(u.Admin, "Quadra coberta");
        (await u.Admin.PostAsJsonAsync("/eventos", Evento(quadra.Id, Dia(6, 14), Dia(6, 18)))).EnsureSuccessStatusCode();

        var dia = DateOnly.FromDateTime(DateTime.Today.AddDays(6));
        var agenda = (await u.Socio.GetFromJsonAsync<AgendaDto>($"/espacos/{quadra.Id}/agenda?data={dia:yyyy-MM-dd}"))!;
        Assert.Equal("Ocupado", agenda.Horarios.Single(h => h.Inicio == new TimeOnly(15, 0)).Situacao);
        Assert.Equal("Livre", agenda.Horarios.Single(h => h.Inicio == new TimeOnly(19, 0)).Situacao);

        var reserva = await u.Socio.PostAsJsonAsync("/reservas", new CriarReservaRequest(quadra.Id, dia, new TimeOnly(15, 0), null));
        Assert.Equal(HttpStatusCode.BadRequest, reserva.StatusCode);
        Assert.Contains("evento", await reserva.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Capacidade_do_evento_nao_passa_da_do_local()
    {
        var u = await UnidadeAsync();
        var local = await LocalAsync(u.Admin, "Quiosque 1", capacidade: 30);
        var resp = await u.Admin.PostAsJsonAsync("/eventos", Evento(local.Id, Dia(7, 10), Dia(7, 14), capacidade: 80));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Reservas_respeitam_vagas_limite_e_cobram_taxa()
    {
        var u = await UnidadeAsync();
        var local = await LocalAsync(u.Admin, "Salão nobre");
        var criado = await u.Admin.PostAsJsonAsync("/eventos", Evento(local.Id, Dia(10, 19), Dia(10, 23), 50,
            new SalvarAmbienteRequest(null, "Mesa para 4", 4, 2, 1, 0m),
            new SalvarAmbienteRequest(null, "Camarote", 10, 1, 1, 120m)));
        var evento = (await criado.Content.ReadFromJsonAsync<EventoDto>())!;

        // Rascunho: o sócio não vê nem reserva.
        Assert.DoesNotContain((await u.Socio.GetFromJsonAsync<List<EventoDto>>("/eventos"))!, e => e.Id == evento.Id);
        var mesa = evento.Ambientes.Single(a => a.Nome == "Mesa para 4");
        Assert.Equal(HttpStatusCode.Conflict,
            (await u.Socio.PostAsJsonAsync($"/eventos/{evento.Id}/reservas", new ReservarEventoRequest(mesa.Id, 4, null))).StatusCode);

        (await u.Admin.PostAsync($"/eventos/{evento.Id}/publicar", null)).EnsureSuccessStatusCode();
        Assert.Contains((await u.Socio.GetFromJsonAsync<List<EventoDto>>("/eventos"))!, e => e.Id == evento.Id);

        // Mesa: 2 vagas, 1 por sócio, até 4 pessoas.
        Assert.Equal(HttpStatusCode.Conflict,
            (await u.Socio.PostAsJsonAsync($"/eventos/{evento.Id}/reservas", new ReservarEventoRequest(mesa.Id, 5, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await u.Socio.PostAsJsonAsync($"/eventos/{evento.Id}/reservas", new ReservarEventoRequest(mesa.Id, 4, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await u.Socio.PostAsJsonAsync($"/eventos/{evento.Id}/reservas", new ReservarEventoRequest(mesa.Id, 2, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await u.Socio2.PostAsJsonAsync($"/eventos/{evento.Id}/reservas", new ReservarEventoRequest(mesa.Id, 3, null))).StatusCode);

        var atualizado = (await u.Socio.GetFromJsonAsync<EventoDto>($"/eventos/{evento.Id}"))!;
        Assert.Equal(0, atualizado.Ambientes.Single(a => a.Id == mesa.Id).VagasRestantes);
        Assert.Equal(7, atualizado.PessoasReservadas);
        Assert.Equal(1, atualizado.MinhasReservas);

        // Camarote com valor: gera a taxa no financeiro; cancelar a reserva cancela a taxa.
        var camarote = evento.Ambientes.Single(a => a.Nome == "Camarote");
        var resp = await u.Socio.PostAsJsonAsync($"/eventos/{evento.Id}/reservas", new ReservarEventoRequest(camarote.Id, 6, null));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var reserva = (await resp.Content.ReadFromJsonAsync<ReservaEventoDto>())!;
        var taxa = (await u.Socio.GetFromJsonAsync<ResumoFinanceiroDto>("/financeiro/minhas"))!
            .Cobrancas.Single(c => c.Tipo == "TaxaEvento");
        Assert.Equal(120m, taxa.Valor);
        Assert.Equal("Aberta", taxa.Status);

        (await u.Socio.PostAsync($"/eventos/minhas-reservas/{reserva.Id}/cancelar", null)).EnsureSuccessStatusCode();
        taxa = (await u.Socio.GetFromJsonAsync<ResumoFinanceiroDto>("/financeiro/minhas"))!.Cobrancas.Single(c => c.Tipo == "TaxaEvento");
        Assert.Equal("Cancelada", taxa.Status);
        Assert.Equal(1, (await u.Socio.GetFromJsonAsync<EventoDto>($"/eventos/{evento.Id}"))!
            .Ambientes.Single(a => a.Id == camarote.Id).VagasRestantes);
    }

    [Fact]
    public async Task Cancelar_evento_cancela_reservas_e_libera_agenda()
    {
        var u = await UnidadeAsync();
        var local = await LocalAsync(u.Admin, "Campo de futebol");
        var evento = (await (await u.Admin.PostAsJsonAsync("/eventos", Evento(local.Id, Dia(12, 9), Dia(12, 12))))
            .Content.ReadFromJsonAsync<EventoDto>())!;
        (await u.Admin.PostAsync($"/eventos/{evento.Id}/publicar", null)).EnsureSuccessStatusCode();
        (await u.Socio.PostAsJsonAsync($"/eventos/{evento.Id}/reservas",
            new ReservarEventoRequest(evento.Ambientes[0].Id, 2, null))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Conflict, // motivo é obrigatório
            (await u.Admin.PostAsJsonAsync($"/eventos/{evento.Id}/cancelar", new CancelarEventoRequest(""))).StatusCode);
        var cancelado = (await (await u.Admin.PostAsJsonAsync($"/eventos/{evento.Id}/cancelar", new CancelarEventoRequest("Chuva forte")))
            .Content.ReadFromJsonAsync<EventoDto>())!;
        Assert.Equal("Cancelado", cancelado.Status);

        var reservas = (await u.Admin.GetFromJsonAsync<List<ReservaEventoDto>>($"/eventos/{evento.Id}/reservas"))!;
        Assert.All(reservas, r => Assert.Equal("Cancelada", r.Status));
        var disp = (await (await u.Admin.PostAsJsonAsync("/eventos/disponibilidade",
            new VerificarAgendaRequest([local.Id], Dia(12, 9), Dia(12, 12)))).Content.ReadFromJsonAsync<DisponibilidadeDto>())!;
        Assert.True(disp.Disponivel);
    }

    [Fact]
    public async Task Evento_de_outra_associacao_nao_aparece()
    {
        var a = await UnidadeAsync();
        var b = await UnidadeAsync();
        var local = await LocalAsync(a.Admin, "Piscina");
        var evento = (await (await a.Admin.PostAsJsonAsync("/eventos", Evento(local.Id, Dia(15, 9), Dia(15, 12))))
            .Content.ReadFromJsonAsync<EventoDto>())!;
        (await a.Admin.PostAsync($"/eventos/{evento.Id}/publicar", null)).EnsureSuccessStatusCode();

        Assert.DoesNotContain((await b.Socio.GetFromJsonAsync<List<EventoDto>>("/eventos"))!, e => e.Id == evento.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Socio.GetAsync($"/eventos/{evento.Id}")).StatusCode);
    }
}
