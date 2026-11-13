namespace ClubeDaOnca.Contracts.Associacoes;

public sealed record EnderecoDto(
    string? Cep = null,
    string? Logradouro = null,
    string? Numero = null,
    string? Complemento = null,
    string? Bairro = null,
    string? Cidade = null,
    string? Uf = null);

public sealed record AssociacaoResumoDto(
    Guid Id,
    string Nome,
    string? Cnpj,
    string? Cidade,
    string? Uf,
    bool Ativa,
    string? Presidente,
    DateOnly? FimMandato,
    bool MandatoVencendo);

/// <param name="Vigente">Mandato em curso hoje.</param>
/// <param name="Login">Preenchido só quando o cadastro do mandato criou a conta do presidente.</param>
/// <param name="SenhaProvisoria">Senha da conta criada (exibida uma única vez).</param>
public sealed record MandatoDto(
    Guid Id,
    string Nome,
    string? Cpf,
    string? Email,
    DateOnly Inicio,
    DateOnly Fim,
    bool Vigente,
    string? Login = null,
    string? SenhaProvisoria = null);

public sealed record DiretorDto(
    Guid Id,
    string Nome,
    string Cargo,
    string? Email,
    string? Telefone,
    DateOnly Inicio,
    DateOnly? Fim,
    bool Ativo);

/// <param name="MandatoVencendo">O mandato do presidente termina em até 60 dias e não há sucessor cadastrado.</param>
public sealed record AssociacaoDto(
    Guid Id,
    string Nome,
    string? Cnpj,
    string? Email,
    string? Telefone,
    EnderecoDto Endereco,
    bool Ativa,
    MandatoDto? PresidenteAtual,
    bool MandatoVencendo,
    IReadOnlyList<MandatoDto> Mandatos,
    IReadOnlyList<DiretorDto> Diretores);

public sealed record SalvarAssociacaoRequest(
    string Nome,
    string? Cnpj,
    string? Email,
    string? Telefone,
    EnderecoDto? Endereco);

/// <summary>Cadastro de uma nova unidade, já com a conta do administrador dela.</summary>
public sealed record NovaAssociacaoRequest(
    string Nome,
    string? Cnpj,
    string? Email,
    string? Telefone,
    EnderecoDto? Endereco,
    string AdminNome,
    string AdminEmail);

/// <param name="SenhaProvisoria">Senha do administrador da nova unidade (trocada no primeiro acesso).</param>
public sealed record AssociacaoCriadaResponse(AssociacaoDto Associacao, string AdminLogin, string SenhaProvisoria);

public sealed record AlterarAtivaRequest(bool Ativa);

/// <summary>Situação da conta do Asaas da unidade (as chaves nunca voltam pela API).</summary>
/// <param name="ContaPropria">A unidade tem chave própria; senão usa a conta geral do servidor.</param>
/// <param name="WebhookUrl">Endereço a cadastrar no painel do Asaas da unidade (Integrações → Webhooks).</param>
public sealed record PagamentosAssociacaoDto(bool ContaPropria, bool WebhookConfigurado, string WebhookUrl, string Gateway);

/// <param name="ApiKey">Nova chave da API do Asaas (nula mantém a atual; vazia remove e volta para a conta geral).</param>
/// <param name="WebhookToken">Token de autenticação dos webhooks (mesmas regras).</param>
public sealed record SalvarPagamentosAssociacaoRequest(string? ApiKey, string? WebhookToken);

/// <summary>O fim do mandato é calculado: início + 2 anos.</summary>
/// <param name="Email">Conta do presidente na associação (recebe as fichas de novos sócios para aprovar).</param>
public sealed record SalvarMandatoRequest(string Nome, string? Cpf, string? Email, DateOnly Inicio);

public sealed record SalvarDiretorRequest(
    string Nome,
    string Cargo,
    string? Email,
    string? Telefone,
    DateOnly Inicio,
    DateOnly? Fim);
