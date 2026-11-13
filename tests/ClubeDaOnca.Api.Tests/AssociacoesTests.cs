using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClubeDaOnca.Contracts.Associacoes;
using ClubeDaOnca.Contracts.Auth;
using ClubeDaOnca.Contracts.Reservas;
using ClubeDaOnca.Contracts.Socios;
using ClubeDaOnca.Domain.Associacoes;

namespace ClubeDaOnca.Api.Tests;

public class AssociacoesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(HttpClient Cliente, AuthResponse Auth)> ClienteAsync(string login, string senha)
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/auth/login", new LoginRequest(login, senha));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private async Task<HttpClient> SuperAdminAsync() => (await ClienteAsync(ApiFactory.AdminEmail, ApiFactory.AdminSenha)).Cliente;

    private static async Task<AssociacaoCriadaResponse> NovaAssociacaoAsync(HttpClient super)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var resp = await super.PostAsJsonAsync("/associacoes", new NovaAssociacaoRequest(
            $"Unidade {sufixo}", null, null, null, new EnderecoDto(Cidade: "Curitiba", Uf: "pr"),
            "Admin da Unidade", $"admin{sufixo}@teste.local"));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<AssociacaoCriadaResponse>())!;
    }

    private static async Task<SocioCriadoResponse> CriarSocioAsync(HttpClient admin, string cpf, string email)
    {
        var resp = await admin.PostAsJsonAsync("/socios", new SalvarSocioRequest(
            "Fulano de Tal", cpf, email, null, new DateOnly(1980, 1, 1), "Individual"));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<SocioCriadoResponse>())!;
    }

    [Fact]
    public async Task Login_do_admin_inicial_leva_para_a_associacao_padrao()
    {
        var (_, auth) = await ClienteAsync(ApiFactory.AdminEmail, ApiFactory.AdminSenha);
        Assert.Equal(Associacao.PadraoId, auth.Usuario.AssociacaoId);
        Assert.Equal("Clube da Onça", auth.Usuario.Associacao);
        Assert.Contains("SuperAdmin", auth.Usuario.Perfis);
    }

    [Fact]
    public async Task Dados_de_uma_associacao_nao_aparecem_na_outra()
    {
        var super = await SuperAdminAsync();
        var nova = await NovaAssociacaoAsync(super);
        var (adminB, authB) = await ClienteAsync(nova.AdminLogin, nova.SenhaProvisoria);
        Assert.Equal(nova.Associacao.Id, authB.Usuario.AssociacaoId);
        Assert.True(authB.Usuario.DeveTrocarSenha);

        // Sócio cadastrado na associação padrão.
        var cpf = CpfTeste.Proximo();
        var socioA = await CriarSocioAsync(super, cpf, $"a{cpf}@teste.local");

        // A outra unidade não enxerga nem acessa o sócio, nem os espaços da padrão.
        var listaB = (await adminB.GetFromJsonAsync<PaginaDto<SocioResumoDto>>("/socios"))!;
        Assert.DoesNotContain(listaB.Itens, s => s.Id == socioA.Socio.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await adminB.GetAsync($"/socios/{socioA.Socio.Id}")).StatusCode);
        Assert.Empty((await adminB.GetFromJsonAsync<List<EspacoDto>>("/espacos"))!);

        // A conta (login) é única na rede: a mesma pessoa em outra unidade gera conflito claro.
        var resp = await adminB.PostAsJsonAsync("/socios", new SalvarSocioRequest(
            "Fulano de Tal", cpf, $"b{cpf}@teste.local", null, new DateOnly(1980, 1, 1), "Individual"));
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);

        // Sócio novo da unidade B fica só nela.
        var cpfB = CpfTeste.Proximo();
        var socioB = await CriarSocioAsync(adminB, cpfB, $"b{cpfB}@teste.local");
        Assert.Equal(HttpStatusCode.NotFound, (await super.GetAsync($"/socios/{socioB.Socio.Id}")).StatusCode);
    }

    [Fact]
    public async Task Admin_de_unidade_nao_cadastra_associacoes_nem_gere_outra()
    {
        var super = await SuperAdminAsync();
        var nova = await NovaAssociacaoAsync(super);
        var (adminB, _) = await ClienteAsync(nova.AdminLogin, nova.SenhaProvisoria);

        Assert.Equal(HttpStatusCode.Forbidden, (await adminB.GetAsync("/associacoes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adminB.GetAsync($"/associacoes/{Associacao.PadraoId}")).StatusCode);

        var atual = (await adminB.GetFromJsonAsync<AssociacaoDto>("/associacoes/atual"))!;
        Assert.Equal(nova.Associacao.Id, atual.Id);
        Assert.Equal("PR", atual.Endereco.Uf);
    }

    [Fact]
    public async Task Presidente_tem_mandato_de_dois_anos_sem_sobreposicao()
    {
        var super = await SuperAdminAsync();
        var nova = await NovaAssociacaoAsync(super);
        var (adminB, _) = await ClienteAsync(nova.AdminLogin, nova.SenhaProvisoria);
        var id = nova.Associacao.Id;
        var inicio = DateOnly.FromDateTime(DateTime.Today).AddMonths(-3);

        var resp = await adminB.PostAsJsonAsync($"/associacoes/{id}/mandatos",
            new SalvarMandatoRequest("José Presidente", null, "presidente@teste.local", inicio));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var mandato = (await resp.Content.ReadFromJsonAsync<MandatoDto>())!;
        Assert.Equal(inicio.AddYears(2).AddDays(-1), mandato.Fim);
        Assert.True(mandato.Vigente);

        var sobreposto = await adminB.PostAsJsonAsync($"/associacoes/{id}/mandatos",
            new SalvarMandatoRequest("Outro Presidente", null, null, inicio.AddYears(1)));
        Assert.Equal(HttpStatusCode.Conflict, sobreposto.StatusCode);

        var diretor = await adminB.PostAsJsonAsync($"/associacoes/{id}/diretores",
            new SalvarDiretorRequest("Ana Diretora", "Diretora financeira", null, null, inicio, null));
        Assert.Equal(HttpStatusCode.Created, diretor.StatusCode);

        var atual = (await adminB.GetFromJsonAsync<AssociacaoDto>("/associacoes/atual"))!;
        Assert.Equal("José Presidente", atual.PresidenteAtual?.Nome);
        Assert.Single(atual.Diretores);
    }

    [Fact]
    public async Task Associacao_desativada_bloqueia_login()
    {
        var super = await SuperAdminAsync();
        var nova = await NovaAssociacaoAsync(super);
        var resp = await super.PatchAsJsonAsync($"/associacoes/{nova.Associacao.Id}/ativa", new AlterarAtivaRequest(false));
        resp.EnsureSuccessStatusCode();

        var login = await factory.CreateClient().PostAsJsonAsync("/auth/login", new LoginRequest(nova.AdminLogin, nova.SenhaProvisoria));
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }
}
