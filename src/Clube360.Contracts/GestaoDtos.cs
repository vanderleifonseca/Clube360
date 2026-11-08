namespace Clube360.Contracts;
public sealed record PessoaDto(Guid Id,Guid ClubeId,string Nome,string Categoria,bool DireitoVoto,bool Isento,
    Guid? TitularId,string Cpf,string Endereco,string Bairro,string Sexo,string EstadoCivil,string Cidade,
    string Uf,string Fone,string Email,DateOnly? DataNascimento,string[]? Interesses,decimal? ValorMensal,bool Ativo,string? Parentesco = null);
public sealed record EspacoDto(Guid Id, Guid ClubeId, string Nome, decimal TaxaAluguel, bool Ativo, string? Tipo = null)
{
    public string? TipoEfetivo => TiposAreaSocial.Identificar(Tipo, Nome);
    public bool Reservavel => TiposAreaSocial.Reservavel(TipoEfetivo);
}
public sealed record EventoDto(Guid Id,Guid ClubeId,string Nome,string Descricao,DateTimeOffset Inicio,DateTimeOffset Fim,
    string Publico,decimal ValorInscricao,Guid? EspacoId,bool Ativo);
public sealed record AgendaDto(Guid Id,string Tipo,string Nome,Guid? EspacoId,DateTimeOffset Inicio,DateTimeOffset Fim);
public sealed record ReservaDto(Guid Id,Guid ClubeId,Guid EspacoId,Guid SolicitanteId,Guid TitularId,DateTimeOffset Inicio,DateTimeOffset Fim,
    string Finalidade,decimal Valor,string Situacao);
public sealed record AndamentoDto(DateTimeOffset Data,string Status,string Mensagem);
public sealed record SolicitacaoDto(Guid Id,Guid ClubeId,Guid AssociadoId,string Tipo,string Titulo,string Descricao,string Status,List<AndamentoDto> Historico);
public sealed record ConviteDto(Guid Id,Guid ClubeId,Guid EventoId,Guid SolicitanteId,PessoaDto Pessoa,string Status,string Observacao);
public sealed record UsuarioDto(string Id,string Perfil,string Nome,string Email,Guid? AssociadoId);
public sealed record CobrancaDto(Guid Id,Guid TitularId,string Titular,string Consumidor,string Origem,string Descricao,decimal Valor,
    DateOnly Vencimento,string Situacao,bool Vencida,string? Competencia);
public sealed record FinanceiroDto(decimal Aberto,decimal Vencido,decimal Recebido,IReadOnlyList<CobrancaDto> Cobrancas);
