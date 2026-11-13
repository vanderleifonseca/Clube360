using System.Text.Json;
using ClubeDaOnca.Contracts.Auth;
using ClubeDaOnca.Domain.Seguranca;

namespace ClubeDaOnca.App.Services;

/// <summary>Guarda a sessão (tokens + usuário) no SecureStorage e renova o access token.</summary>
public sealed class SessaoService(IAuthApi authApi)
{
    private const string Chave = "sessao";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AuthResponse? _atual;

    public UsuarioDto? Usuario => _atual?.Usuario;
    public bool Autenticado => _atual is not null;
    public bool EhGestao => Usuario?.Perfis.Any(p => Perfis.Gestao.Contains(p) || p == Perfis.SuperAdmin) == true;
    public bool EhSecretaria => TemPerfil(Perfis.Secretaria) || TemPerfil(Perfis.Admin);
    public bool EhTitular => TemPerfil(Perfis.Titular);
    public bool EhAdmin => TemPerfil(Perfis.Admin);
    public bool EhSuperAdmin => TemPerfil(Perfis.SuperAdmin);
    public bool TemCarteirinha =>TemPerfil(Perfis.Titular) || TemPerfil(Perfis.Dependente);
    public bool DeveTrocarSenha => Usuario?.DeveTrocarSenha == true;

    private bool TemPerfil(string perfil) => Usuario?.Perfis.Contains(perfil) == true;

    /// <summary>Atualiza os dados do usuário (ex.: depois de /auth/me ou da troca de senha).</summary>
    public async Task AtualizarUsuarioAsync(UsuarioDto usuario)
    {
        if (_atual is null) return;
        await SalvarAsync(_atual with { Usuario = usuario });
    }

    public event EventHandler? SessaoEncerrada;

    public async Task<bool> RestaurarAsync()
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync(Chave);
            _atual = json is null ? null : JsonSerializer.Deserialize<AuthResponse>(json);
        }
        catch
        {
            _atual = null; // SecureStorage corrompido (ex.: reinstalação): força novo login
        }
        return _atual is not null;
    }

    public async Task EntrarAsync(string login, string senha, CancellationToken ct = default)
    {
        var resp = await authApi.LoginAsync(new LoginRequest(login, senha), ct);
        await SalvarAsync(resp);
    }

    public async Task SairAsync()
    {
        var refresh = _atual?.RefreshToken;
        _atual = null;
        SecureStorage.Default.Remove(Chave);
        CarteirinhaService.LimparCache();
        if (refresh is not null)
        {
            try { await authApi.LogoutAsync(new RefreshRequest(refresh)); } catch { /* offline: ignora */ }
        }
        SessaoEncerrada?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Devolve um access token válido, renovando-o se estiver para expirar.</summary>
    public async Task<string?> ObterAccessTokenAsync(bool forcarRenovacao = false, CancellationToken ct = default)
    {
        if (_atual is null) return null;
        if (!forcarRenovacao && _atual.ExpiraEm > DateTimeOffset.UtcNow.AddSeconds(30))
            return _atual.AccessToken;

        await _lock.WaitAsync(ct);
        try
        {
            if (_atual is null) return null;
            if (!forcarRenovacao && _atual.ExpiraEm > DateTimeOffset.UtcNow.AddSeconds(30))
                return _atual.AccessToken; // outra chamada já renovou

            try
            {
                await SalvarAsync(await authApi.RefreshAsync(new RefreshRequest(_atual.RefreshToken), ct));
                return _atual!.AccessToken;
            }
            catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await SairAsync();
                return null;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SalvarAsync(AuthResponse resp)
    {
        _atual = resp;
        await SecureStorage.Default.SetAsync(Chave, JsonSerializer.Serialize(resp));
    }
}
