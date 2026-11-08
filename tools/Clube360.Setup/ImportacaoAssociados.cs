using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Clube360.Contracts;
using Clube360.Domain;
using Microsoft.VisualBasic.FileIO;

internal static class ImportacaoAssociados
{
    private sealed record Linha(int Numero, string Clube, string Nome, string Cpf, DateOnly Nascimento, string Email);
    private sealed record Cadastro(Linha Linha, Guid Clube, PessoaDto? Pessoa, UsuarioDto? Conta);

    public static async Task<int> Executar(string arquivo, Func<string> lerSenha)
    {
        try
        {
            var caminho = Path.GetFullPath(arquivo);
            var relativo = Path.GetRelativePath(Directory.GetCurrentDirectory(), caminho);
            if (!Path.IsPathRooted(relativo) && !relativo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("Coloque o CSV fora da pasta do projeto, por exemplo em Downloads.");

            var linhas = LerLote(caminho);
            using var api = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = new Uri("http://localhost:5187"),
                Timeout = TimeSpan.FromSeconds(15)
            };

            Console.Write("E-mail da conta de gestão: ");
            var email = Console.ReadLine()?.Trim() ?? "";
            Console.Write("Senha da conta de gestão: ");
            using var login = await api.PostAsJsonAsync("/api/v1/login", new LoginRequest(email, lerSenha()));
            if (!login.IsSuccessStatusCode)
                throw new InvalidOperationException($"Login recusado pela API (HTTP {(int)login.StatusCode}).");
            var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
            api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            var sessao = (await api.GetFromJsonAsync<SessaoDto>("/api/v1/sessao"))!;
            var cadastros = await Conferir(api, sessao, linhas);

            var caminhoAcessos = Path.ChangeExtension(caminho, ".acessos.csv");
            var opcoesArquivo = new FileStreamOptions
            {
                Mode = FileMode.OpenOrCreate, Access = FileAccess.ReadWrite, Share = FileShare.None
            };
            if (!OperatingSystem.IsWindows())
                opcoesArquivo.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using var arquivoAcessos = new FileStream(caminhoAcessos, opcoesArquivo);
            var senhas = LerAcessos(arquivoAcessos);
            using var gravador = new StreamWriter(arquivoAcessos, new UTF8Encoding(false), leaveOpen: true);
            if (arquivoAcessos.Length == 0)
                gravador.WriteLine("Email;Senha");

            var criadas = 0;
            foreach (var cadastro in cadastros.Where(c => c.Conta is null))
            {
                var linha = cadastro.Linha;
                var rota = $"/api/v1/clubes/{cadastro.Clube}";
                var pessoa = cadastro.Pessoa;
                if (pessoa is null)
                {
                    using var criarPessoa = await api.PostAsJsonAsync(rota + "/associados", new AssociadoRequest
                    {
                        Nome = linha.Nome, Cpf = linha.Cpf, Email = linha.Email,
                        DataNascimento = linha.Nascimento.ToDateTime(TimeOnly.MinValue)
                    });
                    ExigirSucesso(criarPessoa, linha, "cadastrar associado");
                    pessoa = (await criarPessoa.Content.ReadFromJsonAsync<PessoaDto>())!;
                }

                if (!senhas.TryGetValue(linha.Email, out var senha))
                {
                    senha = Convert.ToHexString(RandomNumberGenerator.GetBytes(10));
                    gravador.WriteLine($"{linha.Email};{senha}");
                    gravador.Flush();
                    arquivoAcessos.Flush(flushToDisk: true);
                }

                using var criarConta = await api.PostAsJsonAsync(rota + "/usuarios",
                    new NovoAcessoRequest(linha.Nome, linha.Email, senha, "Socio", pessoa.Id));
                ExigirSucesso(criarConta, linha, "criar conta de sócio");
                criadas++;
                Console.WriteLine($"Linha {linha.Numero}: cadastro e acesso confirmados.");
            }

            Console.WriteLine($"Lote concluído. {criadas} acessos criados, {cadastros.Count - criadas} já existentes.");
            Console.WriteLine($"Credenciais geradas pelo importador: {caminhoAcessos}");
            return 0;
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException or HttpRequestException
            or InvalidOperationException or OperationCanceledException or MalformedLineException)
        {
            Console.Error.WriteLine(erro.Message);
            return 1;
        }
    }

    private static List<Linha> LerLote(string caminho)
    {
        using var csv = new TextFieldParser(caminho, Encoding.UTF8);
        csv.SetDelimiters(";");
        if (!(csv.ReadFields() ?? []).SequenceEqual(new[] { "Clube", "Nome", "Cpf", "DataNascimento", "Email" }))
            throw new InvalidOperationException("Cabeçalho esperado: Clube;Nome;Cpf;DataNascimento;Email.");

        var linhas = new List<Linha>();
        var cpfs = new HashSet<(string Clube, string Cpf)>();
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!csv.EndOfData)
        {
            var numero = (int)csv.LineNumber;
            if (csv.ReadFields() is not [var clube, var nome, var cpf, var nascimento, var email] ||
                string.IsNullOrWhiteSpace(clube) || string.IsNullOrWhiteSpace(nome) ||
                !DocumentoCpf.Valido(cpf) || !System.Net.Mail.MailAddress.TryCreate(email, out var endereco) ||
                endereco.Address != email || email.Contains(';') ||
                !DateOnly.TryParseExact(nascimento, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ||
                data > DateOnly.FromDateTime(DateTime.UtcNow))
                throw new InvalidOperationException($"Dados inválidos na linha {numero}. Use data no formato AAAA-MM-DD.");

            cpf = DocumentoCpf.Normalizar(cpf);
            if (!cpfs.Add((clube.ToUpperInvariant(), cpf)) || !emails.Add(email))
                throw new InvalidOperationException($"CPF repetido no clube ou e-mail repetido na linha {numero}.");
            linhas.Add(new(numero, clube, nome, cpf, data, email));
        }
        if (linhas.Count == 0)
            throw new InvalidOperationException("O lote está vazio.");
        return linhas;
    }

    private static async Task<List<Cadastro>> Conferir(HttpClient api, SessaoDto sessao, List<Linha> linhas)
    {
        var cadastros = new List<Cadastro>();
        foreach (var grupo in linhas.GroupBy(l => l.Clube, StringComparer.OrdinalIgnoreCase))
        {
            var clube = sessao.Clubes.SingleOrDefault(c => Igual(c.Nome, grupo.Key))
                ?? throw new InvalidOperationException($"O clube {grupo.Key} não está disponível para esta conta.");
            var rota = $"/api/v1/clubes/{clube.Id}";
            var pessoas = (await api.GetFromJsonAsync<List<PessoaDto>>(rota + "/associados"))!;
            var contas = (await api.GetFromJsonAsync<List<UsuarioDto>>(rota + "/usuarios"))!;
            foreach (var linha in grupo)
            {
                var pessoa = pessoas.SingleOrDefault(p => p.Cpf == linha.Cpf);
                var conta = contas.SingleOrDefault(c => Igual(c.Email, linha.Email));
                if ((pessoa is not null && (!Igual(pessoa.Nome, linha.Nome) || !Igual(pessoa.Email, linha.Email) ||
                        pessoa.DataNascimento != linha.Nascimento || pessoa.Categoria != "Associado" || !pessoa.Ativo)) ||
                    (conta is not null && (pessoa is null || conta.AssociadoId != pessoa.Id || conta.Perfil != "Socio")) ||
                    pessoas.Any(p => p.Id != pessoa?.Id && Igual(p.Email, linha.Email)))
                    throw new InvalidOperationException($"Linha {linha.Numero} conflita com um cadastro existente.");

                cadastros.Add(new(linha, clube.Id, pessoa, conta));
            }
        }
        return cadastros;
    }

    private static Dictionary<string, string> LerAcessos(FileStream arquivo)
    {
        var senhas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (arquivo.Length == 0)
            return senhas;

        using var leitor = new StreamReader(arquivo, leaveOpen: true);
        if (leitor.ReadLine() != "Email;Senha")
            throw new InvalidOperationException("O arquivo de credenciais existente tem um cabeçalho inválido.");
        while (leitor.ReadLine() is { } linha)
        {
            if (linha.Split(';') is not [var email, var senha] ||
                senha.Length != 20 || !senha.All(Uri.IsHexDigit) || !senhas.TryAdd(email, senha))
                throw new InvalidOperationException("O arquivo de credenciais existente contém uma linha inválida.");
        }
        return senhas;
    }

    private static bool Igual(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static void ExigirSucesso(HttpResponseMessage resposta, Linha linha, string acao)
    {
        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Linha {linha.Numero}: não foi possível {acao} (HTTP {(int)resposta.StatusCode}).");
    }
}
