using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClubeDaOnca.Contracts.Auth;

namespace ClubeDaOnca.Api.Tests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<AuthResponse> LoginAsync()
    {
        var resp = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminSenha));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    [Fact]
    public async Task Login_valido_retorna_tokens_e_perfis()
    {
        var auth = await LoginAsync();
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.Contains("Admin", auth.Usuario.Perfis);
    }

    [Fact]
    public async Task Senha_errada_retorna_401()
    {
        var resp = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(ApiFactory.AdminEmail, "errada123"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Me_exige_token()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/auth/me")).StatusCode);

        var auth = await LoginAsync();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var me = await (await _client.SendAsync(req)).Content.ReadFromJsonAsync<UsuarioDto>();
        Assert.Equal(ApiFactory.AdminEmail, me!.Email);
    }

    [Fact]
    public async Task Refresh_rotaciona_e_token_antigo_nao_pode_ser_reusado()
    {
        var auth = await LoginAsync();

        var r1 = await _client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        var r2 = await _client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, r2.StatusCode);
    }

    [Fact]
    public async Task Logout_revoga_refresh_token()
    {
        var auth = await LoginAsync();
        await _client.PostAsJsonAsync("/auth/logout", new RefreshRequest(auth.RefreshToken));

        var r = await _client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }
}
