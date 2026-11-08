using Clube360.Contracts;
using Clube360.Domain;

namespace Clube360.Application;

public sealed class PortariaClube(IRepositorioClubes repositorio, TimeProvider relogio)
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public PortariaDto Consultar(Guid clube, DateOnly? dia) => repositorio.Ler(estado =>
    {
        GestaoClubes.ExigirClube(estado, clube);
        var data = dia ?? DiaLocal(relogio.GetUtcNow());
        var pessoas = PessoasDoClube(estado, clube);
        var visitas = estado.Visitas.Where(v => v.ClubeId == clube).ToArray();
        var presentes = visitas.Where(v => v.Saida is null).OrderBy(v => v.Entrada.Horario).ToArray();
        var idsPresentes = presentes.Select(v => v.AssociadoId).ToHashSet();
        var elegiveis = pessoas.Values.Where(p => PodeEntrar(p, pessoas) && !idsPresentes.Contains(p.Id))
            .OrderBy(p => p.Nome)
            .Select(p => new PessoaPortariaDto(p.Id, p.Nome, p.Categoria.ToString(), FinalCpf(p)))
            .ToArray();
        var historico = visitas
            .Select(v => (Visita: v, SaidaNoDia: v.Saida is { } saida && DiaLocal(saida.Horario) == data))
            .Where(item => item.SaidaNoDia || DiaLocal(item.Visita.Entrada.Horario) == data)
            .OrderByDescending(item => item.SaidaNoDia ? item.Visita.Saida!.Horario : item.Visita.Entrada.Horario)
            .Select(item => Resumo(item.Visita))
            .ToArray();

        return new PortariaDto(data, elegiveis, presentes.Select(Resumo).ToArray(), historico);
    });

    public VisitaPortariaDto RegistrarEntrada(Guid clube, Guid visitaId, Guid associadoId, string usuarioId, string usuarioNome) =>
        repositorio.Alterar(estado =>
        {
            GestaoClubes.ExigirClube(estado, clube);
            if (visitaId == Guid.Empty)
                throw new ArgumentException("Identificação da visita inválida.");

            var anterior = estado.Visitas.Find(v => v.ClubeId == clube && v.Id == visitaId);
            if (anterior is not null)
            {
                if (anterior.AssociadoId != associadoId || anterior.Entrada.UsuarioId != usuarioId)
                    throw new ArgumentException("Identificação da visita já utilizada.");
                return Resumo(anterior);
            }

            var pessoas = PessoasDoClube(estado, clube);
            if (!pessoas.TryGetValue(associadoId, out var pessoa) || !PodeEntrar(pessoa, pessoas))
                throw new ArgumentException("Selecione um associado, fundador ou dependente ativo e habilitado neste clube.");

            if (estado.Visitas.Any(v => v.ClubeId == clube && v.AssociadoId == associadoId && v.Saida is null))
                throw new ArgumentException("Esta pessoa já tem uma entrada em aberto.");

            var visita = new VisitaClube(visitaId, clube, pessoa.Id, pessoa.Nome, pessoa.Categoria, FinalCpf(pessoa),
                new RegistroPortaria(relogio.GetUtcNow(), usuarioId, usuarioNome));
            estado.Visitas.Add(visita);
            return Resumo(visita);
        });

    public VisitaPortariaDto RegistrarSaida(Guid clube, Guid visitaId, string usuarioId, string usuarioNome) =>
        repositorio.Alterar(estado =>
        {
            var indice = estado.Visitas.FindIndex(v => v.ClubeId == clube && v.Id == visitaId);
            if (indice < 0)
                throw new ArgumentException("Visita não encontrada neste clube.");

            var visita = estado.Visitas[indice];
            if (visita.Saida is null)
            {
                visita = visita with { Saida = new RegistroPortaria(relogio.GetUtcNow(), usuarioId, usuarioNome) };
                estado.Visitas[indice] = visita;
            }

            return Resumo(visita);
        });

    private static Dictionary<Guid, Associado> PessoasDoClube(EstadoClubes estado, Guid clube) =>
        estado.Associados.Where(p => p.ClubeId == clube).ToDictionary(p => p.Id);

    private static bool PodeEntrar(Associado pessoa, Dictionary<Guid, Associado> pessoas) =>
        pessoa.Ativo && (pessoa.Categoria is Categoria.Associado or Categoria.Fundador ||
            (pessoa.Categoria == Categoria.Dependente && pessoa.TitularId is { } id &&
                pessoas.TryGetValue(id, out var titular) && titular.Ativo &&
                titular.Categoria is Categoria.Associado or Categoria.Fundador));

    private static DateOnly DiaLocal(DateTimeOffset instante) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, Zona).DateTime);

    private static string FinalCpf(Associado pessoa) =>
        pessoa.Cpf is { Length: 11 } cpf ? cpf[^4..] : "";

    private static VisitaPortariaDto Resumo(VisitaClube visita) =>
        new(visita.Id, visita.AssociadoId, visita.Nome, visita.Categoria.ToString(), visita.FinalCpf,
            visita.Entrada.Horario, visita.Entrada.UsuarioNome, visita.Saida?.Horario, visita.Saida?.UsuarioNome);
}
