using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClubeDaOnca.Contracts.Auth;
using ClubeDaOnca.Contracts.Carteirinha;
using ClubeDaOnca.Contracts.Socios;
using ClubeDaOnca.Domain.Carteirinha;

namespace ClubeDaOnca.Api.Tests;

public class SociosCarteirinhaTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string ProximoCpf() => CpfTeste.Proximo();

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

    private static async Task<SocioCriadoResponse> CriarSocioAsync(HttpClient admin, string? cpf = null)
    {
        cpf ??= ProximoCpf();
        var resp = await admin.PostAsJsonAsync("/socios", new SalvarSocioRequest(
            "Maria da Silva", cpf, $"socio{cpf}@teste.local", "(45) 99999-0000", new DateOnly(1985, 5, 20), "Familiar"));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var criado = (await resp.Content.ReadFromJsonAsync<SocioCriadoResponse>())!;
        await SocioTeste.AtivarAsync(admin, criado.Socio.Id);
        return criado;
    }

    [Fact]
    public async Task Cadastro_exige_perfil_de_secretaria()
    {
        var anonimo = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/socios")).StatusCode);

        var admin = await AdminAsync();
        var criado = await CriarSocioAsync(admin);

        // O sócio recém-criado é Titular e não enxerga o cadastro.
        var socio = await ClienteAsync(criado.Login, criado.SenhaProvisoria!);
        Assert.Equal(HttpStatusCode.Forbidden, (await socio.GetAsync("/socios")).StatusCode);
    }

    [Fact]
    public async Task Cria_socio_com_numero_de_titulo_e_conta_de_acesso()
    {
        var admin = await AdminAsync();
        var criado = await CriarSocioAsync(admin);

        Assert.True(criado.Socio.NumeroTitulo >= 1000);
        Assert.True(criado.Socio.PossuiAcessoAoApp);
        Assert.False(string.IsNullOrEmpty(criado.SenhaProvisoria));

        // Primeiro acesso: o app deve pedir a troca da senha provisória.
        var login = await factory.CreateClient().PostAsJsonAsync("/auth/login",
            new LoginRequest(criado.Socio.Cpf, criado.SenhaProvisoria!));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.True(auth.Usuario.DeveTrocarSenha);
        Assert.Contains("Titular", auth.Usuario.Perfis);

        var socio = await ClienteAsync(criado.Login, criado.SenhaProvisoria!);
        var troca = await socio.PostAsJsonAsync("/auth/alterar-senha", new AlterarSenhaRequest(criado.SenhaProvisoria!, "NovaSenha123"));
        Assert.Equal(HttpStatusCode.NoContent, troca.StatusCode);

        var me = await socio.GetFromJsonAsync<UsuarioDto>("/auth/me");
        Assert.False(me!.DeveTrocarSenha);
    }

    [Fact]
    public async Task Rejeita_cpf_invalido_e_duplicado()
    {
        var admin = await AdminAsync();

        var invalido = await admin.PostAsJsonAsync("/socios", new SalvarSocioRequest(
            "Fulano de Tal", "123.456.789-00", "fulano@teste.local", null, new DateOnly(1990, 1, 1), "Individual"));
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);

        var criado = await CriarSocioAsync(admin);
        var duplicado = await admin.PostAsJsonAsync("/socios", new SalvarSocioRequest(
            "Outra Pessoa", criado.Socio.Cpf, "outra@teste.local", null, new DateOnly(1990, 1, 1), "Individual"));
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
    }

    [Fact]
    public async Task Carteirinha_lista_titular_e_dependentes_ativos()
    {
        var admin = await AdminAsync();
        var criado = await CriarSocioAsync(admin);

        var dep = await admin.PostAsJsonAsync($"/socios/{criado.Socio.Id}/dependentes",
            new SalvarDependenteRequest("João da Silva", null, new DateOnly(2015, 3, 10), "Filho"));
        Assert.Equal(HttpStatusCode.Created, dep.StatusCode);
        var inativo = (await (await admin.PostAsJsonAsync($"/socios/{criado.Socio.Id}/dependentes",
            new SalvarDependenteRequest("Ana da Silva", null, new DateOnly(2012, 7, 1), "Filho")))
            .Content.ReadFromJsonAsync<DependenteDto>())!;
        await admin.DeleteAsync($"/socios/{criado.Socio.Id}/dependentes/{inativo.Id}");

        var socio = await ClienteAsync(criado.Login, criado.SenhaProvisoria!);
        var carteirinhas = await socio.GetFromJsonAsync<List<CarteirinhaDto>>("/carteirinha");

        Assert.Equal(2, carteirinhas!.Count);
        Assert.Equal("Titular", carteirinhas[0].Tipo);
        Assert.Equal("João da Silva", carteirinhas[1].Nome);
        Assert.All(carteirinhas, c => Assert.False(string.IsNullOrEmpty(c.ChaveQr)));
    }

    [Fact]
    public async Task Portaria_libera_qr_valido_e_bloqueia_titulo_suspenso()
    {
        var admin = await AdminAsync();
        var criado = await CriarSocioAsync(admin);
        var socio = await ClienteAsync(criado.Login, criado.SenhaProvisoria!);
        var carteirinha = (await socio.GetFromJsonAsync<List<CarteirinhaDto>>("/carteirinha"))![0];

        // O app gera o QR localmente com a chave recebida.
        string GerarQr() => QrCarteirinha.Gerar(carteirinha.PessoaId, Convert.FromBase64String(carteirinha.ChaveQr), DateTimeOffset.UtcNow);

        var liberado = await Validar(admin, GerarQr());
        Assert.True(liberado.Liberado);
        Assert.Equal("Maria da Silva", liberado.Nome);
        Assert.Equal(criado.Socio.NumeroTitulo, liberado.NumeroTitulo);

        await admin.PatchAsJsonAsync($"/socios/{criado.Socio.Id}/situacao", new AlterarSituacaoRequest("Suspenso"));
        var bloqueado = await Validar(admin, GerarQr());
        Assert.False(bloqueado.Liberado);
        Assert.Equal("Título suspenso", bloqueado.Motivo);

        // Sócio comum não pode usar o modo portaria.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await socio.PostAsJsonAsync("/portaria/validar", new ValidarQrRequest(GerarQr()))).StatusCode);

        var acessos = await admin.GetFromJsonAsync<List<RegistroAcessoDto>>("/portaria/acessos");
        Assert.Contains(acessos!, a => a.Nome == "Maria da Silva" && a.Liberado);
        Assert.Contains(acessos!, a => a.Nome == "Maria da Silva" && !a.Liberado);
    }

    [Fact]
    public async Task Portaria_rejeita_qr_falso_e_qr_de_chave_renovada()
    {
        var admin = await AdminAsync();
        var criado = await CriarSocioAsync(admin);
        var socio = await ClienteAsync(criado.Login, criado.SenhaProvisoria!);
        var carteirinha = (await socio.GetFromJsonAsync<List<CarteirinhaDto>>("/carteirinha"))![0];
        var qrAntigo = QrCarteirinha.Gerar(carteirinha.PessoaId, Convert.FromBase64String(carteirinha.ChaveQr), DateTimeOffset.UtcNow);

        Assert.False((await Validar(admin, "texto qualquer")).Liberado);

        var chaveFalsa = new byte[32];
        Assert.False((await Validar(admin, QrCarteirinha.Gerar(carteirinha.PessoaId, chaveFalsa, DateTimeOffset.UtcNow))).Liberado);

        await admin.PostAsync($"/socios/{criado.Socio.Id}/renovar-qr", null);
        var resultado = await Validar(admin, qrAntigo);
        Assert.False(resultado.Liberado);
        Assert.Equal("QR Code inválido", resultado.Motivo);
    }

    private static async Task<ResultadoAcessoDto> Validar(HttpClient client, string token)
    {
        var resp = await client.PostAsJsonAsync("/portaria/validar", new ValidarQrRequest(token));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ResultadoAcessoDto>())!;
    }
}
