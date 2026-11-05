using Clube360.Application;
using Clube360.Domain;

namespace Clube360.Infrastructure;

public sealed class RepositorioClubesPostgres(ArmazenamentoPostgres banco) : IRepositorioClubes, IDadosDemonstracao
{
    public T Ler<T>(Func<EstadoClubes, T> consulta) =>
        consulta(banco.Ler<EstadoClubes>(ArmazenamentoPostgres.Clubes));

    public T Alterar<T>(Func<EstadoClubes, T> operacao) =>
        banco.Alterar(ArmazenamentoPostgres.Clubes, operacao);

    public IReadOnlyList<Clube> Clubes => Ler(s => s.Clubes.ToArray());
    public IReadOnlyList<Associado> Associados => Ler(s => s.Associados.ToArray());
    public IReadOnlyList<Despesa> Despesas => Ler(s => s.Despesas.ToArray());
    public TabelaMensalidade Tabela(Guid clubeId) =>
        Ler(s => new TabelaMensalidade(GestaoClubes.ExigirClube(s, clubeId).ValorMensalPadrao));
}
