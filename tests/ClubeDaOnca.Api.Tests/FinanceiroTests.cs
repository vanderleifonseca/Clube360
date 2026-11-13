using ClubeDaOnca.Domain.Associacoes;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClubeDaOnca.Api.Infrastructure;
using ClubeDaOnca.Contracts.Auth;
using ClubeDaOnca.Contracts.Carteirinha;
using ClubeDaOnca.Contracts.Financeiro;
using ClubeDaOnca.Contracts.Reservas;
using ClubeDaOnca.Contracts.Socios;
using ClubeDaOnca.Domain.Carteirinha;
using ClubeDaOnca.Domain.Financeiro;
using Microsoft.Extensions.DependencyInjection;

namespace ClubeDaOnca.Api.Tests;

public class FinanceiroTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.UtcNow);

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

    private async Task<(HttpClient Cliente, SocioDto Socio)> NovoSocioAsync(HttpClient admin, string categoria = "Individual")
    {
        var cpf = CpfTeste.Proximo();
        var resp = await admin.PostAsJsonAsync("/socios", new SalvarSocioRequest(
            $"Sócio {cpf[..4]}", cpf, $"f{cpf}@teste.local", null, new DateOnly(1980, 1, 1), categoria));
        resp.EnsureSuccessStatusCode();
        var criado = (await resp.Content.ReadFromJsonAsync<SocioCriadoResponse>())!;
        await SocioTeste.AtivarAsync(admin, criado.Socio.Id);
        return (await ClienteAsync(criado.Login, criado.SenhaProvisoria!), criado.Socio);
    }

    /// <summary>Cria uma cobrança direto no banco (ex.: mensalidade antiga, já vencida).</summary>
    private async Task<Guid> CobrancaNoBancoAsync(Guid socioId, decimal valor, DateOnly vencimento)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var c = new Cobranca
        {
            AssociacaoId = Associacao.PadraoId,
            SocioId = socioId, Tipo = TipoCobranca.Mensalidade, Descricao = "Mensalidade antiga",
            Valor = valor, Vencimento = vencimento,
        };
        db.Cobrancas.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    private static async Task<List<CobrancaDto>> CobrancasDoSocioAsync(HttpClient admin, Guid socioId) =>
        (await admin.GetFromJsonAsync<List<CobrancaDto>>($"/financeiro/cobrancas?socioId={socioId}"))!;

    [Fact]
    public async Task Gera_mensalidades_por_categoria_sem_duplicar()
    {
        var admin = await AdminAsync();
        var (_, individual) = await NovoSocioAsync(admin, "Individual");
        var (_, familiar) = await NovoSocioAsync(admin, "Familiar");
        var (_, remido) = await NovoSocioAsync(admin, "Remido");
        var competencia = new DateOnly(2030, 3, 1);

        var r1 = await (await admin.PostAsJsonAsync("/financeiro/mensalidades/gerar", new GerarMensalidadesRequest(competencia)))
            .Content.ReadFromJsonAsync<GerarMensalidadesResponse>();
        Assert.True(r1!.Geradas >= 2);

        var r2 = await (await admin.PostAsJsonAsync("/financeiro/mensalidades/gerar", new GerarMensalidadesRequest(competencia)))
            .Content.ReadFromJsonAsync<GerarMensalidadesResponse>();
        Assert.Equal(0, r2!.Geradas);

        var ind = Assert.Single(await CobrancasDoSocioAsync(admin, individual.Id));
        Assert.Equal(150m, ind.Valor);
        Assert.Equal(new DateOnly(2030, 3, 10), ind.Vencimento);
        Assert.Equal(250m, Assert.Single(await CobrancasDoSocioAsync(admin, familiar.Id)).Valor);
        Assert.Empty(await CobrancasDoSocioAsync(admin, remido.Id));
    }

    [Fact]
    public async Task Socio_paga_com_pix_e_a_confirmacao_da_baixa()
    {
        var admin = await AdminAsync();
        var (socio, dados) = await NovoSocioAsync(admin);
        var cobrancaId = await CobrancaNoBancoAsync(dados.Id, 150m, Hoje.AddDays(5));

        var resumo = await socio.GetFromJsonAsync<ResumoFinanceiroDto>("/financeiro/minhas");
        Assert.Equal(150m, resumo!.TotalEmAberto);
        Assert.False(resumo.Inadimplente);

        var pix = await (await socio.PostAsJsonAsync($"/financeiro/cobrancas/{cobrancaId}/pagar", new PagarRequest("Pix")))
            .Content.ReadFromJsonAsync<PagamentoDto>();
        Assert.False(string.IsNullOrEmpty(pix!.PixCopiaECola));
        Assert.True(pix.Simulado);

        // Pedir de novo reaproveita o mesmo Pix em vez de gerar outro no gateway.
        var deNovo = await (await socio.PostAsJsonAsync($"/financeiro/cobrancas/{cobrancaId}/pagar", new PagarRequest("Pix")))
            .Content.ReadFromJsonAsync<PagamentoDto>();
        Assert.Equal(pix.Id, deNovo!.Id);

        var boleto = await (await socio.PostAsJsonAsync($"/financeiro/cobrancas/{cobrancaId}/pagar", new PagarRequest("Boleto")))
            .Content.ReadFromJsonAsync<PagamentoDto>();
        Assert.False(string.IsNullOrEmpty(boleto!.LinhaDigitavel));

        Assert.Equal(HttpStatusCode.NoContent, (await socio.PostAsync($"/financeiro/pagamentos/{pix.Id}/simular-confirmacao", null)).StatusCode);

        resumo = await socio.GetFromJsonAsync<ResumoFinanceiroDto>("/financeiro/minhas");
        var paga = resumo!.Cobrancas.Single(c => c.Id == cobrancaId);
        Assert.Equal("Paga", paga.Status);
        Assert.Equal("Pix", paga.FormaPagamento);
        Assert.Equal(0m, resumo.TotalEmAberto);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await socio.PostAsJsonAsync($"/financeiro/cobrancas/{cobrancaId}/pagar", new PagarRequest("Pix"))).StatusCode);
    }

    [Fact]
    public async Task Socio_nao_paga_nem_ve_cobranca_de_outro_titulo()
    {
        var admin = await AdminAsync();
        var (_, dono) = await NovoSocioAsync(admin);
        var (intruso, _) = await NovoSocioAsync(admin);
        var cobrancaId = await CobrancaNoBancoAsync(dono.Id, 150m, Hoje.AddDays(5));

        Assert.Equal(HttpStatusCode.NotFound,
            (await intruso.PostAsJsonAsync($"/financeiro/cobrancas/{cobrancaId}/pagar", new PagarRequest("Pix"))).StatusCode);
        Assert.DoesNotContain((await intruso.GetFromJsonAsync<ResumoFinanceiroDto>("/financeiro/minhas"))!.Cobrancas, c => c.Id == cobrancaId);
        Assert.Equal(HttpStatusCode.Forbidden, (await intruso.GetAsync("/financeiro/cobrancas")).StatusCode);
    }

    [Fact]
    public async Task Webhook_do_asaas_da_baixa_e_exige_token()
    {
        var admin = await AdminAsync();
        var (_, dados) = await NovoSocioAsync(admin);
        var cobrancaId = await CobrancaNoBancoAsync(dados.Id, 150m, Hoje.AddDays(5));
        var idExterno = $"pay_{Guid.NewGuid():N}";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Pagamentos.Add(new Pagamento { AssociacaoId = Associacao.PadraoId, CobrancaId = cobrancaId, Gateway = "Asaas", IdExterno = idExterno, Forma = FormaPagamento.Boleto, Valor = 150m });
            await db.SaveChangesAsync();
        }

        var corpo = new { @event = "PAYMENT_RECEIVED", payment = new { id = idExterno, status = "RECEIVED", value = 150m, clientPaymentDate = Hoje.ToString("yyyy-MM-dd") } };
        var anonimo = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/webhooks/asaas", corpo)).StatusCode);

        for (var i = 0; i < 2; i++) // o Asaas pode reenviar o mesmo evento
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/webhooks/asaas") { Content = JsonContent.Create(corpo) };
            req.Headers.Add("asaas-access-token", ApiFactory.WebhookToken);
            Assert.Equal(HttpStatusCode.OK, (await anonimo.SendAsync(req)).StatusCode);
        }

        var cobranca = Assert.Single(await CobrancasDoSocioAsync(admin, dados.Id));
        Assert.Equal("Paga", cobranca.Status);
        Assert.Equal("Boleto", cobranca.FormaPagamento);
    }

    [Fact]
    public async Task Inadimplente_aparece_na_lista_e_e_tratado_na_portaria_e_nas_reservas()
    {
        var admin = await AdminAsync();
        var (socio, dados) = await NovoSocioAsync(admin);
        await CobrancaNoBancoAsync(dados.Id, 150m, Hoje.AddDays(-30));

        var inadimplentes = await admin.GetFromJsonAsync<List<InadimplenteDto>>("/financeiro/inadimplentes");
        Assert.Contains(inadimplentes!, i => i.SocioId == dados.Id && i.ValorVencido == 150m);
        Assert.True((await socio.GetFromJsonAsync<ResumoFinanceiroDto>("/financeiro/minhas"))!.Inadimplente);

        // Padrão: a portaria libera, mas avisa o porteiro.
        var carteirinha = (await socio.GetFromJsonAsync<List<CarteirinhaDto>>("/carteirinha"))![0];
        string Qr() => QrCarteirinha.Gerar(carteirinha.PessoaId, Convert.FromBase64String(carteirinha.ChaveQr), DateTimeOffset.UtcNow);
        var acesso = await (await admin.PostAsJsonAsync("/portaria/validar", new ValidarQrRequest(Qr()))).Content.ReadFromJsonAsync<ResultadoAcessoDto>();
        Assert.True(acesso!.Liberado);
        Assert.True(acesso.Inadimplente);

        // Com bloqueio ligado, a entrada é negada.
        var cfg = (await admin.GetFromJsonAsync<ConfiguracaoFinanceiraDto>("/financeiro/configuracao"))!;
        await admin.PutAsJsonAsync("/financeiro/configuracao", cfg with { BloquearInadimplentesNaPortaria = true });
        acesso = await (await admin.PostAsJsonAsync("/portaria/validar", new ValidarQrRequest(Qr()))).Content.ReadFromJsonAsync<ResultadoAcessoDto>();
        Assert.False(acesso!.Liberado);
        await admin.PutAsJsonAsync("/financeiro/configuracao", cfg);

        // Reservas bloqueadas por padrão.
        var espaco = (await (await admin.PostAsJsonAsync("/espacos", new SalvarEspacoRequest($"Quadra {Guid.NewGuid():N}"[..15], null, true,
            new TimeOnly(8, 0), new TimeOnly(12, 0), 60, [0, 1, 2, 3, 4, 5, 6], 1, 30, 2, 0, false, 24))).Content.ReadFromJsonAsync<EspacoDto>())!;
        var reserva = await socio.PostAsJsonAsync("/reservas", new CriarReservaRequest(espaco.Id, Hoje.AddDays(3), new TimeOnly(9, 0), null));
        Assert.Equal(HttpStatusCode.BadRequest, reserva.StatusCode);
    }

    [Fact]
    public async Task Reserva_com_taxa_gera_cobranca_que_e_cancelada_com_a_reserva()
    {
        var admin = await AdminAsync();
        var (socio, dados) = await NovoSocioAsync(admin);
        var espaco = (await (await admin.PostAsJsonAsync("/espacos", new SalvarEspacoRequest($"Churrasq {Guid.NewGuid():N}"[..15], null, true,
            new TimeOnly(10, 0), new TimeOnly(22, 0), 720, [0, 1, 2, 3, 4, 5, 6], 1, 30, 1, 80m, false, 0))).Content.ReadFromJsonAsync<EspacoDto>())!;

        var reserva = (await (await socio.PostAsJsonAsync("/reservas", new CriarReservaRequest(espaco.Id, Hoje.AddDays(10), new TimeOnly(10, 0), null)))
            .Content.ReadFromJsonAsync<ReservaDto>())!;

        var taxa = Assert.Single(await CobrancasDoSocioAsync(admin, dados.Id));
        Assert.Equal("TaxaReserva", taxa.Tipo);
        Assert.Equal(80m, taxa.Valor);
        Assert.Equal("Aberta", taxa.Status);
        Assert.True(taxa.Vencimento <= Hoje.AddDays(10));

        await socio.PostAsync($"/reservas/{reserva.Id}/cancelar", null);
        Assert.Equal("Cancelada", Assert.Single(await CobrancasDoSocioAsync(admin, dados.Id)).Status);
    }

    [Fact]
    public async Task Secretaria_da_baixa_manual_e_cancela_cobranca()
    {
        var admin = await AdminAsync();
        var (_, dados) = await NovoSocioAsync(admin);
        var c1 = await CobrancaNoBancoAsync(dados.Id, 150m, Hoje.AddDays(-2));

        var baixa = await (await admin.PostAsJsonAsync($"/financeiro/cobrancas/{c1}/baixa", new BaixaManualRequest("Manual", "Pago no balcão")))
            .Content.ReadFromJsonAsync<CobrancaDto>();
        Assert.Equal("Paga", baixa!.Status);
        Assert.Contains("balcão", baixa.Observacao);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/financeiro/cobrancas/{c1}/cancelar", null)).StatusCode);
    }

    [Fact]
    public async Task Financeiro_lanca_cobranca_avulsa_que_o_socio_ve_e_paga_pelo_app()
    {
        var admin = await AdminAsync();
        var (socio, dto) = await NovoSocioAsync(admin);

        var resp = await admin.PostAsJsonAsync("/financeiro/cobrancas",
            new LancarCobrancaRequest(dto.Id, "Multa por dano à quadra", 120.50m, Hoje.AddDays(7)));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var criada = (await resp.Content.ReadFromJsonAsync<CobrancaDto>())!;
        Assert.Equal("Avulsa", criada.Tipo);
        Assert.Equal("Aberta", criada.Status);
        Assert.Equal(120.50m, criada.Valor);

        // O sócio vê a cobrança e recebe o aviso no app.
        var minhas = (await socio.GetFromJsonAsync<ResumoFinanceiroDto>("/financeiro/minhas"))!;
        Assert.Contains(minhas.Cobrancas, c => c.Id == criada.Id && c.Descricao == "Multa por dano à quadra");
        var avisos = await socio.GetStringAsync("/avisos");
        Assert.Contains("Nova cobran", avisos);

        // E paga pelo app, como a mensalidade.
        var pagar = await socio.PostAsJsonAsync($"/financeiro/cobrancas/{criada.Id}/pagar", new PagarRequest("Pix"));
        Assert.Equal(HttpStatusCode.OK, pagar.StatusCode);
    }

    [Fact]
    public async Task Cobranca_avulsa_invalida_ou_por_socio_e_recusada()
    {
        var admin = await AdminAsync();
        var (socio, dto) = await NovoSocioAsync(admin);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/financeiro/cobrancas",
            new LancarCobrancaRequest(dto.Id, "Consumo", 0m, Hoje.AddDays(7)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/financeiro/cobrancas",
            new LancarCobrancaRequest(dto.Id, "Consumo", 10m, Hoje.AddDays(-3)))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/financeiro/cobrancas",
            new LancarCobrancaRequest(Guid.NewGuid(), "Consumo", 10m, Hoje.AddDays(7)))).StatusCode);

        // Sócio não lança cobrança.
        Assert.Equal(HttpStatusCode.Forbidden, (await socio.PostAsJsonAsync("/financeiro/cobrancas",
            new LancarCobrancaRequest(dto.Id, "Consumo", 10m, Hoje.AddDays(7)))).StatusCode);
    }
}
