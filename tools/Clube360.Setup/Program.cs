using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

const string projetoApi = "src/Clube360.Api/Clube360.Api.csproj";
var redefinirAdministrador = args is ["--redefinir-administrador"];
if (args.Length > 0 && !redefinirAdministrador)
{
    Console.Error.WriteLine("Use --redefinir-administrador para alterar a senha do administrador inicial.");
    return 1;
}
if (Console.IsInputRedirected || !File.Exists(projetoApi))
{
    Console.Error.WriteLine("Execute em um terminal interativo, na raiz do Clube360.");
    return 1;
}

var valores = new Dictionary<string, string>();
if (redefinirAdministrador)
{
    var id = XDocument.Load(projetoApi).Descendants("UserSecretsId").Single().Value;
    var configuracao = new ConfigurationBuilder().AddUserSecrets(id).Build();
    var email = configuracao["Acessos:Administrador:Email"];
    if (string.IsNullOrWhiteSpace(email))
    {
        Console.Error.WriteLine("A conta inicial de administrador ainda não tem e-mail configurado.");
        return 1;
    }

    Console.WriteLine($"Administrador inicial: {email}");
    var hash = LerHash("Nova senha do administrador (mínimo 4 caracteres): ");
    if (hash is null)
        return 1;

    valores["Acessos:Administrador:SenhaHash"] = hash;
}
else
{
    var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var perfil in new[] { "Administrador", "Socio", "Plataforma" })
    {
        var padrao = perfil.ToLowerInvariant() + "@clube360.test";
        Console.Write($"E-mail {perfil} (Enter = {padrao}): ");
        var email = Console.ReadLine()?.Trim();
        email = string.IsNullOrEmpty(email) ? padrao : email;
        if (!email.Contains('@') || !emails.Add(email))
        {
            Console.Error.WriteLine("E-mail inválido ou repetido.");
            return 1;
        }

        var hash = LerHash($"Senha de {perfil} (mínimo 4 caracteres): ");
        if (hash is null)
            return 1;

        valores[$"Acessos:{perfil}:Email"] = email;
        valores[$"Acessos:{perfil}:SenhaHash"] = hash;
    }
}

var start = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, UseShellExecute = false };
foreach (var arg in new[] { "user-secrets", "set", "--project", projetoApi })
    start.ArgumentList.Add(arg);
using var process = Process.Start(start)!;
await process.StandardInput.WriteAsync(JsonSerializer.Serialize(valores));
process.StandardInput.Close();
await process.WaitForExitAsync();
if (process.ExitCode != 0)
    return process.ExitCode;

Console.WriteLine(redefinirAdministrador
    ? "Senha do administrador redefinida. Reinicie a API."
    : "Contas de teste configuradas. Reinicie a API.");
return 0;

static string? LerHash(string mensagem)
{
    Console.Write(mensagem);
    var senha = LerSenha();
    if (senha.Length is < 4 or > 256)
    {
        Console.Error.WriteLine("Use entre 4 e 256 caracteres.");
        return null;
    }

    Console.Write("Confirme a senha: ");
    if (LerSenha() != senha)
    {
        Console.Error.WriteLine("As senhas não coincidem.");
        return null;
    }

    return new PasswordHasher<object>().HashPassword(new object(), senha);
}

static string LerSenha()
{
    var sb = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return sb.ToString();
        }
        if (key.Key == ConsoleKey.Backspace)
        {
            if (sb.Length > 0)
                sb.Length--;
        }
        else if (!char.IsControl(key.KeyChar) && sb.Length < 257)
        {
            sb.Append(key.KeyChar);
        }
    }
}
