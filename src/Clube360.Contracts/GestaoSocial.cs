namespace Clube360.Contracts;
public sealed class ClubeRequest {
    public string Nome { get; set; } = ""; public string Sigla { get; set; } = "";
    public string Endereco { get; set; } = ""; public string Bairro { get; set; } = "";
    public string Cidade { get; set; } = ""; public string Uf { get; set; } = "";
    public string Fone { get; set; } = ""; public string Email { get; set; } = "";
    public decimal ValorMensalPadrao { get; set; }
}
public sealed class AssociadoRequest {
    public string Nome { get; set; } = ""; public string Cpf { get; set; } = "";
    public string Categoria { get; set; } = "Associado";
    public string Endereco { get; set; } = ""; public string Bairro { get; set; } = "";
    public string Sexo { get; set; } = ""; public string EstadoCivil { get; set; } = "";
    public string Cidade { get; set; } = ""; public string Uf { get; set; } = "";
    public string Fone { get; set; } = ""; public string Email { get; set; } = "";
    public DateTime? DataNascimento { get; set; }
    public string[] Interesses { get; set; } = [];
    public Guid? TitularId { get; set; } public string? Parentesco { get; set; }
    public decimal? ValorMensal { get; set; } public bool Isento { get; set; } public bool Ativo { get; set; } = true;
}
public sealed record EspacoRequest(string Nome, decimal TaxaAluguel, string? Tipo = null);
public sealed record EventoRequest(string Nome, string Descricao, DateTimeOffset Inicio, DateTimeOffset Fim,
    string Publico, decimal ValorInscricao, Guid? EspacoId = null);
public sealed record ReservaRequest(Guid EspacoId, DateTimeOffset Inicio, DateTimeOffset Fim, string Finalidade);
public sealed record SolicitacaoRequest(string Tipo, string Titulo, string Descricao);
public sealed record AndamentoRequest(string Status, string Mensagem);
public sealed record ConviteRequest(Guid EventoId, AssociadoRequest Pessoa);
public sealed record DecisaoRequest(bool Aprovar, string Observacao);
public sealed record MensalidadesRequest(int Ano, int Mes, DateOnly Vencimento);
public sealed record NovoAcessoRequest(string Nome, string Email, string Senha, string Perfil, Guid? AssociadoId = null);
public sealed record BoletoDto(bool Disponivel, string? Url, string Mensagem);

public sealed record VisitanteRequest(string Nome,string Cpf,string Email);
