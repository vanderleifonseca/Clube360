namespace Clube360.Domain;
public enum PublicoEvento { SomenteSocios, Publico }
public enum SituacaoReserva { Confirmada, Cancelada }
public enum SituacaoCobranca { Aberta, Paga, Cancelada, ReembolsoPendente }
public enum TipoSolicitacao { Solicitacao, Reclamacao }
public enum StatusSolicitacao { Aberta, EmAnalise, Concluida, Cancelada }
public enum StatusConvite { Pendente, Aprovado, Rejeitado }
public sealed record Espaco(Guid Id, Guid ClubeId, string Nome, decimal TaxaAluguel, bool Ativo = true, string? Tipo = null);
public sealed record Evento(Guid Id, Guid ClubeId, string Nome, string Descricao,
    DateTimeOffset Inicio, DateTimeOffset Fim, PublicoEvento Publico,
    decimal ValorInscricao, Guid? EspacoId = null, bool Ativo = true);
public sealed record Reserva(Guid Id, Guid ClubeId, Guid EspacoId, Guid SolicitanteId, Guid TitularId,
    DateTimeOffset Inicio, DateTimeOffset Fim, string Finalidade, decimal Valor,
    SituacaoReserva Situacao = SituacaoReserva.Confirmada)
{
    public bool PodeCancelar(DateTimeOffset agora) => Situacao == SituacaoReserva.Confirmada && agora <= Inicio.AddDays(-5);
}
public sealed record Cobranca(Guid Id, Guid ClubeId, Guid TitularId, Guid ConsumidorId,
    string Origem, Guid OrigemId, string Descricao, decimal Valor, DateOnly Vencimento,
    SituacaoCobranca Situacao = SituacaoCobranca.Aberta, string? Competencia = null, DateTimeOffset? PagoEm = null, string? BaixadoPor = null)
{
    public bool Vencida(DateOnly hoje) => Situacao == SituacaoCobranca.Aberta && Vencimento < hoje;
}
public sealed record Andamento(DateTimeOffset Data, StatusSolicitacao Status, string Mensagem);
public sealed record Solicitacao(Guid Id, Guid ClubeId, Guid AssociadoId, TipoSolicitacao Tipo,
    string Titulo, string Descricao, StatusSolicitacao Status, List<Andamento> Historico);
public sealed record Convite(Guid Id, Guid ClubeId, Guid EventoId, Guid SolicitanteId,
    Associado Pessoa, StatusConvite Status = StatusConvite.Pendente, string Observacao = "");
public sealed record Visitante(Guid Id, Guid ClubeId, Guid EventoId, string Nome, string Cpf, string Email);
public sealed record Inscricao(Guid Id, Guid ClubeId, Guid EventoId, Guid AssociadoId);
public sealed record AgendaItem(Guid Id, string Tipo, string Nome, Guid? EspacoId,
    DateTimeOffset Inicio, DateTimeOffset Fim);
public static class Periodos
{
    public static void Validar(DateTimeOffset inicio, DateTimeOffset fim, DateTimeOffset agora) {
        if (inicio <= agora || fim <= inicio) throw new ArgumentException("O início deve ser futuro e o término posterior ao início.");
    }
    public static bool Sobrepoe(DateTimeOffset a, DateTimeOffset b, DateTimeOffset c, DateTimeOffset d) => a < d && c < b;
}
public sealed class EstadoClubes
{
    public int Versao { get; set; } = 1;
    public List<Clube> Clubes { get; set; } = [];
    public List<Associado> Associados { get; set; } = [];
    public List<Despesa> Despesas { get; set; } = [];
    public List<Espaco> Espacos { get; set; } = [];
    public List<Evento> Eventos { get; set; } = [];
    public List<Reserva> Reservas { get; set; } = [];
    public List<Cobranca> Cobrancas { get; set; } = [];
    public List<Solicitacao> Solicitacoes { get; set; } = [];
    public List<Convite> Convites { get; set; } = [];
    public List<Inscricao> Inscricoes { get; set; } = [];
    public List<Visitante> Visitantes { get; set; } = [];
    public List<VisitaClube> Visitas { get; set; } = [];
}
