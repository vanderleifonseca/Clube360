using System.Globalization;
using ClubeDaOnca.Domain.Contratos;
using ClubeDaOnca.Domain.Socios;

namespace ClubeDaOnca.Domain.Financeiro;

public static class RegrasFinanceiras
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static DateOnly Competencia(DateOnly data) => new(data.Year, data.Month, 1);

    /// <summary>Vencimento no dia configurado; em meses curtos, cai no último dia do mês.</summary>
    public static DateOnly Vencimento(DateOnly competencia, int diaVencimento) =>
        new(competencia.Year, competencia.Month, Math.Clamp(diaVencimento, 1, DateTime.DaysInMonth(competencia.Year, competencia.Month)));

    public static string DescricaoMensalidade(DateOnly competencia)
    {
        var mes = competencia.ToString("MMMM/yyyy", PtBr);
        return $"Mensalidade {char.ToUpper(mes[0])}{mes[1..]}";
    }

    /// <summary>
    /// Cria a mensalidade da competência para o sócio, ou nulo quando não cabe cobrança
    /// (título não ativo, valor zero ou mensalidade já existente).
    /// </summary>
    public static Cobranca? GerarMensalidade(Socio socio, DateOnly competencia, ConfiguracaoFinanceira cfg, bool jaExiste)
    {
        competencia = Competencia(competencia);
        var valor = cfg.ValorPara(socio.Categoria);
        if (jaExiste || socio.Situacao != SituacaoTitulo.Ativo || valor <= 0)
            return null;

        return new Cobranca
        {
            SocioId = socio.Id,
            Tipo = TipoCobranca.Mensalidade,
            Competencia = competencia,
            Descricao = DescricaoMensalidade(competencia),
            Valor = valor,
            Vencimento = Vencimento(competencia, cfg.DiaVencimento),
        };
    }

    public const int ParcelasPorContrato = 12;

    public const decimal ValorMaximoAvulsa = 100_000m;

    /// <summary>
    /// Cobrança avulsa lançada pelo financeiro (multa, consumo, conserto etc.). Devolve o erro em texto quando
    /// os dados não servem: título cancelado ou em adesão, descrição curta, valor fora da faixa ou vencimento no passado.
    /// </summary>
    public static (Cobranca? Cobranca, string? Erro) CobrancaAvulsa(Socio socio, string? descricao, decimal valor, DateOnly vencimento, DateOnly hoje)
    {
        var texto = descricao?.Trim() ?? string.Empty;
        if (socio.Situacao == SituacaoTitulo.Cancelado)
            return (null, "Este título está cancelado: não recebe novas cobranças.");
        if (socio.Situacao == SituacaoTitulo.EmAdesao)
            return (null, "Este candidato ainda está em adesão: a cobrança só pode ser lançada depois da aprovação.");
        if (texto.Length < 3)
            return (null, "Informe a descrição da cobrança (ex.: Multa por dano ao patrimônio).");
        if (texto.Length > 200)
            return (null, "A descrição pode ter no máximo 200 caracteres.");
        if (valor <= 0 || valor > ValorMaximoAvulsa)
            return (null, $"Informe um valor entre R$ 0,01 e {ValorMaximoAvulsa.ToString("C", PtBr)}.");
        if (decimal.Round(valor, 2) != valor)
            return (null, "O valor deve ter no máximo 2 casas decimais.");
        if (vencimento < hoje)
            return (null, "O vencimento não pode estar no passado.");

        return (new Cobranca
        {
            SocioId = socio.Id,
            Tipo = TipoCobranca.Avulsa,
            Descricao = texto,
            Valor = valor,
            Vencimento = vencimento,
        }, null);
    }

    /// <summary>
    /// As 12 mensalidades do contrato, a partir do primeiro vencimento, uma por mês (competência = mês do vencimento).
    /// Meses que já têm mensalidade do sócio são pulados; contrato de valor zero (ex.: remido) não gera parcelas.
    /// </summary>
    public static List<Cobranca> ParcelasContrato(Contrato contrato, ISet<DateOnly> competenciasExistentes)
    {
        var parcelas = new List<Cobranca>();
        if (contrato.ValorMensalidade <= 0) return parcelas;

        var primeiraCompetencia = Competencia(contrato.PrimeiroVencimento);
        for (var i = 0; i < ParcelasPorContrato; i++)
        {
            var competencia = primeiraCompetencia.AddMonths(i);
            if (competenciasExistentes.Contains(competencia)) continue;
            var vencimento = i == 0 ? contrato.PrimeiroVencimento : Vencimento(competencia, contrato.DiaVencimento);
            parcelas.Add(new Cobranca
            {
                AssociacaoId = contrato.AssociacaoId,
                SocioId = contrato.SocioId,
                Tipo = TipoCobranca.Mensalidade,
                Competencia = competencia,
                ContratoId = contrato.Id,
                Parcela = i + 1,
                Descricao = $"{DescricaoMensalidade(competencia)} · parcela {i + 1}/{ParcelasPorContrato} (contrato {contrato.Numero})",
                Valor = contrato.ValorMensalidade,
                Vencimento = vencimento,
            });
        }
        return parcelas;
    }

    /// <summary>Contrato aprovado cobre a competência: as mensalidades vêm das parcelas dele, não do job mensal.</summary>
    public static bool ContratoCobre(Contrato contrato, DateOnly competencia)
    {
        competencia = Competencia(competencia);
        return contrato.Status == StatusContrato.Aprovado && contrato.VigenciaInicio is { } inicio && contrato.VigenciaFim is { } fim
            && Competencia(inicio) <= competencia && competencia <= Competencia(fim).AddMonths(1);
    }

    /// <summary>Taxa vence em alguns dias, mas nunca depois da data da reserva.</summary>
    public static DateOnly VencimentoTaxaReserva(DateOnly hoje, DateOnly dataReserva, int prazoDias)
    {
        var limite = hoje.AddDays(Math.Max(prazoDias, 0));
        return limite < dataReserva ? limite : (dataReserva < hoje ? hoje : dataReserva);
    }

    /// <summary>Inadimplente: alguma cobrança aberta vencida há mais dias que a tolerância.</summary>
    public static bool Inadimplente(IEnumerable<Cobranca> cobrancas, DateOnly hoje, int diasTolerancia) =>
        cobrancas.Any(c => c.Status == StatusCobranca.Aberta && c.Vencimento.AddDays(diasTolerancia) < hoje);

    public static DateOnly LimiteInadimplencia(DateOnly hoje, int diasTolerancia) => hoje.AddDays(-diasTolerancia);
}
