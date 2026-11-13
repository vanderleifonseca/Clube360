using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClubeDaOnca.Contracts.Associacoes;
using ClubeDaOnca.Contracts.Auth;
using ClubeDaOnca.Contracts.Socios;

namespace ClubeDaOnca.Api.Tests;

public class CadastroHistoricoTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
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

    private static SalvarSocioRequest Requisicao(string cpf, string genero = "Feminino", string telefone = "(45) 3000-0000") => new(
        "Ana Paula Souza", cpf, $"ana{cpf}@teste.local", telefone, new DateOnly(1990, 3, 15), "Familiar",
        genero, "Engenheira civil",
        new EnderecoDto("85851-000", "Av. Brasil", "1200", "Ap 31", "Centro", "Foz do Iguaçu", "pr"),
        new DateOnly(2015, 6, 1),
        "Brasileira", "Casado", "12.345.678-9 SSP/PR");

    private static async Task<SocioDto> CriarAsync(HttpClient admin, SalvarSocioRequest req)
    {
        var resp = await admin.PostAsJsonAsync("/socios", req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<SocioCriadoResponse>())!.Socio;
    }

    [Fact]
    public async Task Cadastro_completo_e_salvo_e_devolvido()
    {
        var admin = await AdminAsync();
        var socio = await CriarAsync(admin, Requisicao(CpfTeste.Proximo()));

        var lido = (await admin.GetFromJsonAsync<SocioDto>($"/socios/{socio.Id}"))!;
        Assert.Equal("Titular", lido.Tipo);
        Assert.Equal("Feminino", lido.Genero);
        Assert.Equal("Engenheira civil", lido.Profissao);
        Assert.Equal("Brasileira", lido.Nacionalidade);
        Assert.Equal("Casado", lido.EstadoCivil);
        Assert.Equal("12.345.678-9 SSP/PR", lido.Rg);
        Assert.Equal(new DateOnly(2015, 6, 1), lido.DataCadastro);
        Assert.Equal("85851000", lido.Endereco?.Cep);
        Assert.Equal("PR", lido.Endereco?.Uf);
        Assert.Equal("Foz do Iguaçu", lido.Endereco?.Cidade);
        Assert.Equal("Administrador", lido.UltimaAlteracaoPor);
    }

    [Fact]
    public async Task Cep_e_genero_invalidos_sao_recusados()
    {
        var admin = await AdminAsync();
        var req = Requisicao(CpfTeste.Proximo(), genero: "Qualquer") with { Endereco = new EnderecoDto(Cep: "123") };
        var resp = await admin.PostAsJsonAsync("/socios", req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Alteracoes_ficam_no_historico_com_quem_alterou()
    {
        var admin = await AdminAsync();
        var cpf = CpfTeste.Proximo();
        var socio = await CriarAsync(admin, Requisicao(cpf));

        var resp = await admin.PutAsJsonAsync($"/socios/{socio.Id}", Requisicao(cpf, telefone: "(45) 99999-1234")
            with { Endereco = new EnderecoDto("85851-000", "Av. Brasil", "1500", "Ap 31", "Centro", "Foz do Iguaçu", "PR") });
        resp.EnsureSuccessStatusCode();

        var dep = await admin.PostAsJsonAsync($"/socios/{socio.Id}/dependentes",
            new SalvarDependenteRequest("Pedro Souza", null, new DateOnly(2015, 1, 1), "Filho", "Masculino"));
        Assert.Equal(HttpStatusCode.Created, dep.StatusCode);
        var dependente = (await dep.Content.ReadFromJsonAsync<DependenteDto>())!;
        Assert.Equal("Dependente", dependente.Tipo);
        Assert.Equal("Ana Paula Souza", dependente.TitularNome);
        (await admin.DeleteAsync($"/socios/{socio.Id}/dependentes/{dependente.Id}")).EnsureSuccessStatusCode();

        var historico = (await admin.GetFromJsonAsync<List<HistoricoDto>>($"/socios/{socio.Id}/historico"))!;
        Assert.Contains(historico, h => h.Acao == "Criado" && h.ValorNovo == "Cadastro criado");
        var tel = Assert.Single(historico, h => h.Campo == "Telefone");
        Assert.Equal("(45) 3000-0000", tel.ValorAnterior);
        Assert.Equal("(45) 99999-1234", tel.ValorNovo);
        Assert.Equal("Administrador", tel.UsuarioNome);
        var numero = Assert.Single(historico, h => h.Campo == "Endereço — número");
        Assert.Equal(("1200", "1500"), (numero.ValorAnterior, numero.ValorNovo));
        Assert.DoesNotContain(historico, h => h.Campo == "Endereço — CEP"); // não mudou
        Assert.Contains(historico, h => h.Descricao == "Dependente Pedro Souza" && h.Acao == "Criado");
        Assert.Contains(historico, h => h.Descricao == "Dependente Pedro Souza" && h.Campo == "Ativo" && h.ValorNovo == "Não");
    }

    [Fact]
    public async Task Filtro_por_genero()
    {
        var admin = await AdminAsync();
        var feminina = await CriarAsync(admin, Requisicao(CpfTeste.Proximo(), genero: "Feminino"));
        var masculino = await CriarAsync(admin, Requisicao(CpfTeste.Proximo(), genero: "Masculino"));

        var pagina = (await admin.GetFromJsonAsync<PaginaDto<SocioResumoDto>>("/socios?genero=Feminino&tamanhoPagina=100"))!;
        Assert.All(pagina.Itens, s => Assert.Equal("Feminino", s.Genero));
        Assert.Contains(pagina.Itens, s => s.Id == feminina.Id);
        Assert.DoesNotContain(pagina.Itens, s => s.Id == masculino.Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/socios?genero=xyz")).StatusCode);
    }

    [Fact]
    public async Task Historico_de_outra_associacao_nao_aparece()
    {
        var admin = await AdminAsync();
        var socio = await CriarAsync(admin, Requisicao(CpfTeste.Proximo()));

        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var nova = await admin.PostAsJsonAsync("/associacoes", new NovaAssociacaoRequest(
            $"Unidade {sufixo}", null, null, null, null, "Admin B", $"b{sufixo}@teste.local"));
        var criada = (await nova.Content.ReadFromJsonAsync<AssociacaoCriadaResponse>())!;
        var adminB = await ClienteAsync(criada.AdminLogin, criada.SenhaProvisoria);

        Assert.Equal(HttpStatusCode.NotFound, (await adminB.GetAsync($"/socios/{socio.Id}/historico")).StatusCode);
    }
}
