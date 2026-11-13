namespace ClubeDaOnca.Domain.Socios;

public sealed record DecisaoAcesso(bool Liberado, string Motivo);

/// <summary>Decide se a portaria libera a entrada.</summary>
public static class RegrasAcesso
{
    /// <param name="inadimplente">Título com mensalidade em atraso além da tolerância.</param>
    /// <param name="bloquearInadimplente">Configuração do clube: bloquear ou só avisar o porteiro.</param>
    public static DecisaoAcesso Avaliar(SituacaoTitulo situacao, bool dependenteAtivo = true,
        bool inadimplente = false, bool bloquearInadimplente = false) =>
        situacao switch
        {
            SituacaoTitulo.Cancelado => new(false, "Título cancelado"),
            SituacaoTitulo.Suspenso => new(false, "Título suspenso"),
            SituacaoTitulo.EmAdesao => new(false, "Adesão em análise: aguardando aprovação do contrato"),
            _ when !dependenteAtivo => new(false, "Dependente inativo"),
            _ when inadimplente && bloquearInadimplente => new(false, "Mensalidade em atraso. Encaminhe à secretaria."),
            _ when inadimplente => new(true, "Entrada liberada. Atenção: mensalidade em atraso."),
            _ => new(true, "Entrada liberada"),
        };
}
