using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Clube360.Infrastructure;
using Clube360.Contracts;
using Clube360.Application;

namespace Clube360.Api.Seguranca;

public sealed record ContaLocal(string Id, string Perfil, string Nome, string Email, string SenhaHash, Guid? ClubeId, Guid? AssociadoId);

public sealed class ContasLocais
{
    private readonly object _gate = new();
    private readonly IRepositorioClubes _dados;
    private readonly Dictionary<string, ContaLocal> _iniciais = new(StringComparer.OrdinalIgnoreCase);
    private readonly PasswordHasher<object> _hasher = new();
    private readonly object _usuario = new();
    private readonly string _hashFalso;
    private readonly string _arquivo;
    private readonly ArmazenamentoPostgres? _postgres;
    private List<ContaLocal> _criadas;

    public ContasLocais(IConfiguration config, IWebHostEnvironment env, IRepositorioClubes dados,
        ArmazenamentoPostgres? postgres = null)
    {
        _dados = dados;
        _postgres = postgres;
        _hashFalso = _hasher.HashPassword(_usuario, Guid.NewGuid().ToString("N"));
        _arquivo = config["Dados:ContasArquivo"] ?? Path.Combine(env.ContentRootPath, "App_Data", "contas.json");
        _criadas = postgres is null ? CarregarArquivo(_arquivo) : [];
        var criadas = ContasCriadas();

        Adicionar("Administrador", "Administrador", "Administração foz 2", DadosDemonstracao.ClubeLago, null);
        Adicionar("Socio", "Socio", "Alex Exemplo", DadosDemonstracao.ClubeLago, DadosDemonstracao.Titular);
        Adicionar("Plataforma", "SuperAdmin", "Super administrador", null, null);
        Adicionar("Presidente", "Presidente", "Presidente foz 2", DadosDemonstracao.ClubeLago, null);
        Adicionar("Tesoureiro", "Tesoureiro", "Tesoureiro foz 2", DadosDemonstracao.ClubeLago, null);

        void Adicionar(string chave, string perfil, string nome, Guid? clube, Guid? associado)
        {
            var email = config[$"Acessos:{chave}:Email"]?.Trim();
            var hash = config[$"Acessos:{chave}:SenhaHash"];
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(hash))
                return;

            if (_iniciais.ContainsKey(email) || criadas.Any(c => MesmoEmail(c.Email, email)))
                throw new InvalidOperationException("E-mails locais duplicados.");

            _iniciais.Add(email, new("bootstrap:" + chave, perfil, nome, email, hash, clube, associado));
        }
    }

    public static List<ContaLocal> CarregarArquivo(string arquivo)
    {
        if (!File.Exists(arquivo))
            return [];

        var contas = JsonSerializer.Deserialize<List<ContaLocal>>(File.ReadAllText(arquivo))
            ?? throw new InvalidDataException("Contas locais inválidas.");
        if (contas.Select(c => c.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count() != contas.Count)
            throw new InvalidDataException("O arquivo contém e-mails duplicados.");
        return contas;
    }

    private static bool MesmoEmail(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private List<ContaLocal> ContasCriadas() =>
        _postgres?.Ler<List<ContaLocal>>(ArmazenamentoPostgres.Contas) ?? _criadas;

    private IEnumerable<ContaLocal> Todas() => _iniciais.Values.Concat(ContasCriadas());

    public ContaLocal? Validar(string? email, string? senha)
    {
        if (string.IsNullOrEmpty(senha) || senha.Length > 256)
            return null;

        lock (_gate)
        {
            var conta = Todas().FirstOrDefault(c => MesmoEmail(c.Email, email?.Trim() ?? ""));
            try
            {
                var senhaValida = _hasher.VerifyHashedPassword(_usuario, conta?.SenhaHash ?? _hashFalso, senha)
                    != PasswordVerificationResult.Failed;
                return senhaValida && Elegivel(conta) ? conta : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }

    private bool Elegivel(ContaLocal? c) => c is not null && (c.Perfil != "Socio" ||
        _dados.Ler(s => s.Associados.Any(a => a.Id == c.AssociadoId && a.ClubeId == c.ClubeId && a.Ativo)));

    public ContaLocal? Atual(ClaimsPrincipal user)
    {
        lock (_gate)
        {
            var c = Todas().FirstOrDefault(c => c.Id == user.FindFirstValue(ClaimTypes.NameIdentifier));
            return Elegivel(c) ? c : null;
        }
    }

    public object[] Listar(Guid clube)
    {
        lock (_gate)
            return Todas().Where(c => c.ClubeId == clube)
                .Select(c => (object)new { c.Id, c.Perfil, c.Nome, c.Email, c.AssociadoId }).ToArray();
    }

    public object Criar(Guid clube, NovoAcessoRequest r, ContaLocal autor)
    {
        var permitidos = autor.Perfil switch
        {
            "SuperAdmin" => new[] { "Presidente", "Administrador", "Tesoureiro", "Socio" },
            "Presidente" => new[] { "Administrador", "Tesoureiro", "Socio" },
            _ => new[] { "Socio" }
        };
        if (!permitidos.Contains(r.Perfil))
            throw new UnauthorizedAccessException();
        if (string.IsNullOrWhiteSpace(r.Nome) || !System.Net.Mail.MailAddress.TryCreate(r.Email, out _)
            || r.Senha is null || r.Senha.Length < 4 || r.Senha.Length > 256)
            throw new ArgumentException("Informe nome, e-mail e senha de 4 a 256 caracteres.");
        if (r.Perfil == "Socio" && r.AssociadoId is null)
            throw new ArgumentException("Vincule a conta ao associado ou dependente.");

        lock (_gate)
        {
            var email = r.Email.Trim();
            ContaLocal Adicionar(List<ContaLocal> contas)
            {
                if (_iniciais.ContainsKey(email) || contas.Any(c => MesmoEmail(c.Email, email)))
                    throw new ArgumentException("E-mail já cadastrado. Múltiplos vínculos na mesma conta serão tratados em etapa própria.");

                var conta = new ContaLocal(Guid.NewGuid().ToString(), r.Perfil, r.Nome.Trim(), email,
                    _hasher.HashPassword(_usuario, r.Senha), clube, r.AssociadoId);
                contas.Add(conta);
                return conta;
            }

            ContaLocal c;
            if (_postgres is not null)
            {
                c = _postgres.Alterar<List<ContaLocal>, ContaLocal>(ArmazenamentoPostgres.Contas, Adicionar);
            }
            else
            {
                var novas = _criadas.ToList();
                c = Adicionar(novas);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_arquivo))!);
                var temporario = _arquivo + ".tmp";
                File.WriteAllText(temporario, JsonSerializer.Serialize(novas));
                File.Move(temporario, _arquivo, true);
                _criadas = novas;
            }

            return new { c.Id, c.Perfil, c.Nome, c.Email, c.ClubeId, c.AssociadoId };
        }
    }
}
