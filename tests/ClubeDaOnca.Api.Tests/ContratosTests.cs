using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ClubeDaOnca.Api.Infrastructure;
using ClubeDaOnca.Api.Infrastructure.Email;
using ClubeDaOnca.Contracts.Associacoes;
using ClubeDaOnca.Contracts.Auth;
using ClubeDaOnca.Contracts.Contratos;
using ClubeDaOnca.Contracts.Financeiro;
using ClubeDaOnca.Contracts.Socios;
using ClubeDaOnca.Domain.Financeiro;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubeDaOnca.Api.Tests;

public partial class ContratosTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Unidade(Guid Id, HttpClient Admin, HttpClient Presidente);

    private async Task<HttpClient> ClienteAsync(string login, string senha)
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/auth/login", new LoginRequest(login, senha));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    /// <summary>Associação nova, com administrador e presidente (mandato vigente com conta criada automaticamente).</summary>
    private async Task<Unidade> UnidadeAsync()
    {
        var super = await ClienteAsync(ApiFactory.AdminEmail, ApiFactory.AdminSenha);
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var resp = await super.PostAsJsonAsync("/associacoes", new NovaAssociacaoRequest(
            $"Clube {sufixo}", null, null, null,
            new EnderecoDto("85851000", "Rua das Onças", "100", null, "Centro", "Foz do Iguaçu", "PR"),
            "Admin", $"adm{sufixo}@teste.local"));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var unidade = (await resp.Content.ReadFromJsonAsync<AssociacaoCriadaResponse>())!;
        var admin = await ClienteAsync(unidade.AdminLogin, unidade.SenhaProvisoria);

        var mandato = await admin.PostAsJsonAsync($"/associacoes/{unidade.Associacao.Id}/mandatos", new SalvarMandatoRequest(
            "José Presidente", null, $"pres{sufixo}@teste.local", DateOnly.FromDateTime(DateTime.Today).AddMonths(-1)));
        Assert.Equal(HttpStatusCode.Created, mandato.StatusCode);
        var m = (await mandato.Content.ReadFromJsonAsync<MandatoDto>())!;
        Assert.NotNull(m.SenhaProvisoria);
        var presidente = await ClienteAsync(m.Login!, m.SenhaProvisoria!);
        return new Unidade(unidade.Associacao.Id, admin, presidente);
    }

    private static SalvarSocioRequest SocioCompleto(string cpf) => new(
        "Carla Mendes", cpf, $"carla{cpf}@teste.local", "(45) 99999-1111", new DateOnly(1988, 4, 2), "Familiar",
        "Feminino", "Advogada", new EnderecoDto("85851000", "Av. Brasil", "500", null, "Centro", "Foz do Iguaçu", "PR"),
        null, "Brasileira", "Casado", "12.345.678-9 SSP/PR");

    private static async Task<SocioDto> NovoSocioAsync(HttpClient admin, SalvarSocioRequest req)
    {
        var resp = await admin.PostAsJsonAsync("/socios", req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<SocioCriadoResponse>())!.Socio;
    }

    [GeneratedRegex(@"/aceite/([A-Za-z0-9_\-]+)")]
    private static partial Regex LinkAceite();

    private string TokenEnviadoPara(string email)
    {
        var memoria = factory.Services.GetRequiredService<EmailMemoria>();
        var msg = memoria.Enviados.Last(m => m.Para == email);
        return LinkAceite().Match(msg.Html).Groups[1].Value;
    }

    /// <summary>Cria sócio + dependente, gera e envia o contrato; devolve contrato e token do link.</summary>
    private async Task<(SocioDto Socio, ContratoDto Contrato, string Token)> PropostaEnviadaAsync(Unidade u)
    {
        var socio = await NovoSocioAsync(u.Admin, SocioCompleto(CpfTeste.Proximo()));
        Assert.Equal("EmAdesao", socio.Situacao);
        (await u.Admin.PostAsJsonAsync($"/socios/{socio.Id}/dependentes",
            new SalvarDependenteRequest("Lucas Mendes", null, new DateOnly(2016, 8, 1), "Filho"))).EnsureSuccessStatusCode();

        var criado = await u.Admin.PostAsJsonAsync($"/socios/{socio.Id}/contratos", new SalvarContratoRequest());
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        var contrato = (await criado.Content.ReadFromJsonAsync<ContratoDto>())!;
        Assert.Equal("Rascunho", contrato.Status);
        Assert.Equal(250m, contrato.ValorMensalidade); // valor da categoria Familiar na configuração padrão

        var enviado = await u.Admin.PostAsync($"/contratos/{contrato.Id}/enviar", null);
        Assert.Equal(HttpStatusCode.OK, enviado.StatusCode);
        contrato = (await enviado.Content.ReadFromJsonAsync<ContratoDto>())!;
        Assert.Equal("AguardandoAceite", contrato.Status);
        Assert.Single(contrato.Dependentes);
        return (socio, contrato, TokenEnviadoPara(socio.Email!));
    }

    [Fact]
    public async Task Fluxo_completo_envio_aceite_e_aval_do_presidente()
    {
        var u = await UnidadeAsync();
        var (socio, contrato, token) = await PropostaEnviadaAsync(u);
        Assert.Matches(@"^\d{4}/0001$", contrato.Numero);

        // Candidato abre o link (sem login) e concorda.
        var anonimo = factory.CreateClient();
        var pagina = await anonimo.GetStringAsync($"/aceite/{token}");
        Assert.Contains("Concordo", pagina);
        Assert.Contains(contrato.Numero, pagina);
        var aceite = await anonimo.PostAsync($"/aceite/{token}", null);
        Assert.Contains("Aceite registrado", await aceite.Content.ReadAsStringAsync());
        Assert.Contains("não está mais disponível", await (await anonimo.PostAsync($"/aceite/{token}", null)).Content.ReadAsStringAsync());

        // Só o presidente vigente avalia.
        var resumo = (await u.Presidente.GetFromJsonAsync<ResumoPresidenciaDto>("/presidencia/resumo"))!;
        Assert.True(resumo.EhPresidente);
        Assert.Equal(1, resumo.Pendentes);
        Assert.NotNull(resumo.UltimoAceiteEm); // o app usa para avisar o presidente de ficha nova
        Assert.False((await u.Admin.GetFromJsonAsync<ResumoPresidenciaDto>("/presidencia/resumo"))!.EhPresidente);
        Assert.Equal(HttpStatusCode.Forbidden, (await u.Admin.PostAsync($"/presidencia/fichas/{contrato.Id}/aprovar", null)).StatusCode);

        var fichas = (await u.Presidente.GetFromJsonAsync<List<FichaAprovacaoDto>>("/presidencia/fichas"))!;
        var ficha = Assert.Single(fichas);
        Assert.Equal("Advogada", ficha.Socio.Profissao);
        Assert.NotNull(ficha.Contrato.AceiteEm);

        var aprovado = await u.Presidente.PostAsync($"/presidencia/fichas/{contrato.Id}/aprovar", null);
        Assert.Equal(HttpStatusCode.OK, aprovado.StatusCode);
        contrato = (await aprovado.Content.ReadFromJsonAsync<ContratoDto>())!;
        Assert.Equal("Aprovado", contrato.Status);
        Assert.Equal("José Presidente", contrato.AprovadoPor);
        Assert.Equal(contrato.VigenciaInicio!.Value.AddMonths(12).AddDays(-1), contrato.VigenciaFim);

        var socioDepois = (await u.Admin.GetFromJsonAsync<SocioDto>($"/socios/{socio.Id}"))!;
        Assert.Equal("Ativo", socioDepois.Situacao);
        Assert.Equal("Aprovado", socioDepois.StatusContrato);

        // Documento final: sem campos por preencher, com titular, dependente, aceite e aprovação.
        var doc = await u.Admin.GetAsync($"/contratos/{contrato.Id}/documento");
        Assert.Equal(HttpStatusCode.OK, doc.StatusCode);
        using var arquivo = WordprocessingDocument.Open(new MemoryStream(await doc.Content.ReadAsByteArrayAsync()), false);
        var texto = arquivo.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.DoesNotContain("{{", texto);
        Assert.Contains("Carla Mendes", texto);
        Assert.Contains("Lucas Mendes", texto);
        Assert.Contains("casado(a)", texto);
        Assert.Contains("duzentos e cinquenta reais", texto);
        Assert.Contains("José Presidente", texto);
        Assert.DoesNotContain("(pendente)", texto);
    }

    [Fact]
    public async Task Cadastro_incompleto_impede_gerar_contrato()
    {
        var u = await UnidadeAsync();
        var socio = await NovoSocioAsync(u.Admin, new SalvarSocioRequest(
            "Sem Endereço", CpfTeste.Proximo(), $"se{Guid.NewGuid():N}@teste.local", null, new DateOnly(1990, 1, 1), "Individual"));
        var resp = await u.Admin.PostAsJsonAsync($"/socios/{socio.Id}/contratos", new SalvarContratoRequest());
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var corpo = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Falta: RG", corpo);
        Assert.Contains("Falta: Endereço — CEP", corpo);
    }

    [Fact]
    public async Task Reprovacao_exige_motivo_e_cancela_o_candidato()
    {
        var u = await UnidadeAsync();
        var (socio, contrato, token) = await PropostaEnviadaAsync(u);
        await factory.CreateClient().PostAsync($"/aceite/{token}", null);

        Assert.Equal(HttpStatusCode.Conflict,
            (await u.Presidente.PostAsJsonAsync($"/presidencia/fichas/{contrato.Id}/reprovar", new MotivoRequest(" "))).StatusCode);
        var resp = await u.Presidente.PostAsJsonAsync($"/presidencia/fichas/{contrato.Id}/reprovar", new MotivoRequest("Documentação divergente"));
        Assert.Equal("Reprovado", (await resp.Content.ReadFromJsonAsync<ContratoDto>())!.Status);
        Assert.Equal("Cancelado", (await u.Admin.GetFromJsonAsync<SocioDto>($"/socios/{socio.Id}"))!.Situacao);
    }

    [Fact]
    public async Task Link_invalido_ou_alterado_nao_aceita()
    {
        var u = await UnidadeAsync();
        var (_, contrato, token) = await PropostaEnviadaAsync(u);
        var anonimo = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync("/aceite/token-que-nao-existe")).StatusCode);

        // Alterar a proposta invalida o link já enviado.
        (await u.Admin.PutAsJsonAsync($"/contratos/{contrato.Id}", new SalvarContratoRequest(ValorMensalidade: 199.9m))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"/aceite/{token}")).StatusCode);
    }

    [Fact]
    public async Task Desligamento_cancela_mensalidades_futuras()
    {
        var u = await UnidadeAsync();
        var (socio, contrato, token) = await PropostaEnviadaAsync(u);
        await factory.CreateClient().PostAsync($"/aceite/{token}", null);
        (await u.Presidente.PostAsync($"/presidencia/fichas/{contrato.Id}/aprovar", null)).EnsureSuccessStatusCode();

        Guid futura, vencida;
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Cobranca Nova(DateOnly venc) => new()
            {
                AssociacaoId = u.Id, SocioId = socio.Id, Tipo = TipoCobranca.Mensalidade, Descricao = "Mensalidade",
                Valor = 250m, Vencimento = venc,
            };
            var f = Nova(hoje.AddDays(20));
            var v = Nova(hoje.AddDays(-20));
            db.Cobrancas.AddRange(f, v);
            await db.SaveChangesAsync();
            (futura, vencida) = (f.Id, v.Id);
        }

        Assert.Equal(HttpStatusCode.Conflict,
            (await u.Admin.PostAsJsonAsync($"/contratos/{contrato.Id}/desligar", new MotivoRequest(""))).StatusCode);
        var resp = await u.Admin.PostAsJsonAsync($"/contratos/{contrato.Id}/desligar", new MotivoRequest("Mudou de cidade"));
        Assert.Equal("Desligado", (await resp.Content.ReadFromJsonAsync<ContratoDto>())!.Status);
        Assert.Equal("Cancelado", (await u.Admin.GetFromJsonAsync<SocioDto>($"/socios/{socio.Id}"))!.Situacao);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cobrancas = await db.Cobrancas.IgnoreQueryFilters().Where(c => c.Id == futura || c.Id == vencida).ToListAsync();
            Assert.Equal(StatusCobranca.Cancelada, cobrancas.Single(c => c.Id == futura).Status);
            Assert.Equal(StatusCobranca.Aberta, cobrancas.Single(c => c.Id == vencida).Status);
        }
    }

    [Fact]
    public async Task Renovacao_cria_novo_contrato_ligado_ao_anterior()
    {
        var u = await UnidadeAsync();
        var (_, contrato, token) = await PropostaEnviadaAsync(u);
        await factory.CreateClient().PostAsync($"/aceite/{token}", null);
        (await u.Presidente.PostAsync($"/presidencia/fichas/{contrato.Id}/aprovar", null)).EnsureSuccessStatusCode();

        var resp = await u.Admin.PostAsync($"/contratos/{contrato.Id}/renovar", null);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var renovacao = (await resp.Content.ReadFromJsonAsync<ContratoDto>())!;
        Assert.Equal(contrato.Id, renovacao.ContratoAnteriorId);
        Assert.Equal("Rascunho", renovacao.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await u.Admin.PostAsync($"/contratos/{contrato.Id}/renovar", null)).StatusCode);
    }

    private async Task<(Unidade U, SocioDto Socio, ContratoDto Contrato)> ContratoAprovadoAsync()
    {
        var u = await UnidadeAsync();
        var (socio, contrato, token) = await PropostaEnviadaAsync(u);
        await factory.CreateClient().PostAsync($"/aceite/{token}", null);
        var resp = await u.Presidente.PostAsync($"/presidencia/fichas/{contrato.Id}/aprovar", null);
        resp.EnsureSuccessStatusCode();
        return (u, socio, (await resp.Content.ReadFromJsonAsync<ContratoDto>())!);
    }

    [Fact]
    public async Task Aprovacao_gera_doze_parcelas_com_boleto()
    {
        var (u, socio, contrato) = await ContratoAprovadoAsync();

        var parcelas = (await u.Admin.GetFromJsonAsync<List<CobrancaDto>>($"/contratos/{contrato.Id}/parcelas"))!;
        Assert.Equal(12, parcelas.Count);
        Assert.Equal(Enumerable.Range(1, 12), parcelas.Select(p => p.Parcela!.Value));
        Assert.All(parcelas, p =>
        {
            Assert.Equal("Aberta", p.Status);
            Assert.Equal(250m, p.Valor);
            Assert.Equal(contrato.Numero, p.ContratoNumero);
            Assert.NotNull(p.EmitidaEm);
            Assert.False(string.IsNullOrEmpty(p.BoletoUrl));
            Assert.False(string.IsNullOrEmpty(p.LinhaDigitavel));
        });
        Assert.Equal(contrato.PrimeiroVencimento, parcelas[0].Vencimento);

        // Emitir de novo não duplica.
        var reemissao = (await (await u.Admin.PostAsync($"/contratos/{contrato.Id}/boletos", null))
            .Content.ReadFromJsonAsync<EmissaoBoletosDto>())!;
        Assert.Equal(0, reemissao.Emitidos);

        // O sócio vê as parcelas no app, com o boleto.
        var minhas = await u.Admin.GetFromJsonAsync<List<CobrancaDto>>($"/financeiro/cobrancas?socioId={socio.Id}");
        Assert.Equal(12, minhas!.Count(c => c.Parcela is not null));

        // O job mensal não gera mensalidade avulsa para quem já tem as parcelas do contrato.
        var gerar = await u.Admin.PostAsJsonAsync("/financeiro/mensalidades/gerar", new GerarMensalidadesRequest(parcelas[1].Vencimento));
        gerar.EnsureSuccessStatusCode();
        var depois = (await u.Admin.GetFromJsonAsync<List<CobrancaDto>>($"/financeiro/cobrancas?socioId={socio.Id}"))!;
        Assert.Equal(12, depois.Count);
    }

    [Fact]
    public async Task Desligamento_cancela_as_parcelas_futuras_do_contrato()
    {
        var (u, _, contrato) = await ContratoAprovadoAsync();
        (await u.Admin.PostAsJsonAsync($"/contratos/{contrato.Id}/desligar", new MotivoRequest("Pediu desligamento"))).EnsureSuccessStatusCode();
        var parcelas = (await u.Admin.GetFromJsonAsync<List<CobrancaDto>>($"/contratos/{contrato.Id}/parcelas"))!;
        Assert.All(parcelas, p => Assert.Equal("Cancelada", p.Status));
        Assert.All(parcelas, p => Assert.Null(p.BoletoUrl)); // boletos cancelados também no gateway
    }

    [Fact]
    public async Task Conta_asaas_da_unidade_fica_criptografada()
    {
        var u = await UnidadeAsync();
        var vazio = (await u.Admin.GetFromJsonAsync<PagamentosAssociacaoDto>($"/associacoes/{u.Id}/pagamentos"))!;
        Assert.False(vazio.ContaPropria);
        Assert.EndsWith($"/webhooks/asaas/{u.Id}", vazio.WebhookUrl);

        var resp = await u.Admin.PutAsJsonAsync($"/associacoes/{u.Id}/pagamentos",
            new SalvarPagamentosAssociacaoRequest("$aact_chave_de_teste_123", "token-webhook-unidade"));
        var salvo = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"contaPropria\":true", salvo);
        Assert.DoesNotContain("chave_de_teste", salvo);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gravada = await db.Associacoes.Where(a => a.Id == u.Id).Select(a => a.AsaasApiKeyProtegida).SingleAsync();
        Assert.DoesNotContain("chave_de_teste", gravada);

        // Webhook da unidade valida o token dela.
        var anonimo = factory.CreateClient();
        var corpo = new { @event = "PAYMENT_RECEIVED", payment = new { id = "pay_inexistente" } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync($"/webhooks/asaas/{u.Id}", corpo)).StatusCode);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/webhooks/asaas/{u.Id}") { Content = JsonContent.Create(corpo) };
        req.Headers.Add("asaas-access-token", "token-webhook-unidade");
        Assert.Equal(HttpStatusCode.OK, (await anonimo.SendAsync(req)).StatusCode);
    }
}
