using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Contratos;
using ClubeDaOnca.Domain.Reservas;
using ClubeDaOnca.Domain.Socios;

namespace ClubeDaOnca.Domain.Financeiro;

public enum TipoCobranca
{
    Mensalidade = 1,
    TaxaReserva = 2,
    TaxaEvento = 3,

    /// <summary>Lançada pelo financeiro para um sócio (multa, consumo, conserto etc.).</summary>
    Avulsa = 4,
}

public enum StatusCobranca
{
    Aberta = 1,
    Paga = 2,
    Cancelada = 3,
}

public enum FormaPagamento
{
    Pix = 1,
    Boleto = 2,

    /// <summary>Baixa feita pela secretaria (dinheiro, cartão no balcão, transferência).</summary>
    Manual = 3,
}

public enum StatusPagamento
{
    Pendente = 1,
    Confirmado = 2,
    Cancelado = 3,
}

/// <summary>Valor devido pelo título: mensalidade, taxa de reserva ou de evento, ou cobrança avulsa.</summary>
public sealed class Cobranca : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }
    public Guid SocioId { get; set; }
    public Socio? Socio { get; set; }

    public TipoCobranca Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Mês de referência (sempre dia 1) das mensalidades.</summary>
    public DateOnly? Competencia { get; set; }

    public Guid? ReservaId { get; set; }
    public Reserva? Reserva { get; set; }

    /// <summary>Reserva de ambiente em evento que originou a taxa.</summary>
    public Guid? ReservaEventoId { get; set; }

    /// <summary>Contrato de adesão que originou a mensalidade (parcela 1 a 12).</summary>
    public Guid? ContratoId { get; set; }
    public Contrato? Contrato { get; set; }
    public int? Parcela { get; set; }

    /// <summary>Quando o boleto foi registrado no gateway (data de emissão).</summary>
    public DateTimeOffset? EmitidaEm { get; set; }

    public decimal Valor { get; set; }
    public DateOnly Vencimento { get; set; }
    public StatusCobranca Status { get; set; } = StatusCobranca.Aberta;

    public DateTimeOffset? PagoEm { get; set; }
    public FormaPagamento? FormaPagamento { get; set; }
    public string? Observacao { get; set; }

    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    public List<Pagamento> Pagamentos { get; set; } = [];

    public bool Vencida(DateOnly hoje) => Status == StatusCobranca.Aberta && Vencimento < hoje;
}

/// <summary>Tentativa de pagamento no gateway (um Pix ou um boleto gerado para a cobrança).</summary>
public sealed class Pagamento : IPertenceAssociacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociacaoId { get; set; }
    public Guid CobrancaId { get; set; }
    public Cobranca? Cobranca { get; set; }

    public string Gateway { get; set; } = string.Empty;
    public string IdExterno { get; set; } = string.Empty;
    public FormaPagamento Forma { get; set; }
    public StatusPagamento Status { get; set; } = StatusPagamento.Pendente;
    public decimal Valor { get; set; }

    public string? PixCopiaECola { get; set; }

    /// <summary>Imagem PNG do QR Pix em Base64, quando o gateway fornece.</summary>
    public string? PixImagemBase64 { get; set; }
    public DateTimeOffset? PixExpiraEm { get; set; }

    public string? BoletoUrl { get; set; }
    public string? LinhaDigitavel { get; set; }

    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConfirmadoEm { get; set; }
}

/// <summary>Parâmetros financeiros da associação (um registro por associação).</summary>
public sealed class ConfiguracaoFinanceira : IPertenceAssociacao
{
    public Guid AssociacaoId { get; set; }
    public decimal ValorIndividual { get; set; } = 150m;
    public decimal ValorFamiliar { get; set; } = 250m;

    /// <summary>Sócio remido normalmente não paga mensalidade (valor 0 = não gera cobrança).</summary>
    public decimal ValorRemido { get; set; }

    public int DiaVencimento { get; set; } = 10;

    /// <summary>Dias após o vencimento até o título ser considerado inadimplente.</summary>
    public int DiasTolerancia { get; set; } = 5;

    public bool BloquearInadimplentesNaPortaria { get; set; }
    public bool BloquearInadimplentesNasReservas { get; set; } = true;

    /// <summary>Dias para pagar a taxa de uma reserva confirmada (limitado à data da reserva).</summary>
    public int PrazoTaxaReservaDias { get; set; } = 3;

    public decimal ValorPara(CategoriaTitulo categoria) => categoria switch
    {
        CategoriaTitulo.Familiar => ValorFamiliar,
        CategoriaTitulo.Remido => ValorRemido,
        _ => ValorIndividual,
    };
}
