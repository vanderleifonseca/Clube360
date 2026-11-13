using ClubeDaOnca.Contracts.Associacoes;
using ClubeDaOnca.Contracts.Auth;
using ClubeDaOnca.Contracts.Carteirinha;
using ClubeDaOnca.Contracts.Comunicacao;
using ClubeDaOnca.Contracts.Contratos;
using ClubeDaOnca.Contracts.Eventos;
using ClubeDaOnca.Contracts.Financeiro;
using ClubeDaOnca.Contracts.Reservas;
using ClubeDaOnca.Contracts.Socios;
using Refit;

namespace ClubeDaOnca.App.Services;

/// <summary>Endpoints públicos de autenticação (sem token).</summary>
public interface IAuthApi
{
    [Post("/auth/login")]
    Task<AuthResponse> LoginAsync([Body] LoginRequest request, CancellationToken ct = default);

    [Post("/auth/refresh")]
    Task<AuthResponse> RefreshAsync([Body] RefreshRequest request, CancellationToken ct = default);

    [Post("/auth/logout")]
    Task LogoutAsync([Body] RefreshRequest request, CancellationToken ct = default);
}

/// <summary>Endpoints que exigem usuário autenticado (o token é injetado pelo AuthHeaderHandler).</summary>
public interface IClubeApi
{
    [Get("/auth/me")]
    Task<UsuarioDto> MeAsync(CancellationToken ct = default);

    [Post("/auth/alterar-senha")]
    Task AlterarSenhaAsync([Body] AlterarSenhaRequest request, CancellationToken ct = default);

    // Associações (unidades)
    [Get("/associacoes")]
    Task<List<AssociacaoResumoDto>> AssociacoesAsync(CancellationToken ct = default);

    [Get("/associacoes/atual")]
    Task<AssociacaoDto> AssociacaoAtualAsync(CancellationToken ct = default);

    [Get("/associacoes/{id}")]
    Task<AssociacaoDto> AssociacaoAsync(Guid id, CancellationToken ct = default);

    [Post("/associacoes")]
    Task<AssociacaoCriadaResponse> CriarAssociacaoAsync([Body] NovaAssociacaoRequest request, CancellationToken ct = default);

    [Put("/associacoes/{id}")]
    Task<AssociacaoDto> AtualizarAssociacaoAsync(Guid id, [Body] SalvarAssociacaoRequest request, CancellationToken ct = default);

    [Patch("/associacoes/{id}/ativa")]
    Task<AssociacaoDto> AlterarAssociacaoAtivaAsync(Guid id, [Body] AlterarAtivaRequest request, CancellationToken ct = default);

    [Post("/associacoes/{id}/mandatos")]
    Task<MandatoDto> CriarMandatoAsync(Guid id, [Body] SalvarMandatoRequest request, CancellationToken ct = default);

    [Delete("/associacoes/{id}/mandatos/{mandatoId}")]
    Task RemoverMandatoAsync(Guid id, Guid mandatoId, CancellationToken ct = default);

    [Post("/associacoes/{id}/diretores")]
    Task<DiretorDto> CriarDiretorAsync(Guid id, [Body] SalvarDiretorRequest request, CancellationToken ct = default);

    [Delete("/associacoes/{id}/diretores/{diretorId}")]
    Task RemoverDiretorAsync(Guid id, Guid diretorId, CancellationToken ct = default);

    // Contratos de adesão (secretaria)
    [Get("/socios/{socioId}/contratos")]
    Task<List<ContratoDto>> ContratosDoSocioAsync(Guid socioId, CancellationToken ct = default);

    [Post("/socios/{socioId}/contratos")]
    Task<ContratoDto> CriarContratoAsync(Guid socioId, [Body] SalvarContratoRequest request, CancellationToken ct = default);

    [Put("/contratos/{id}")]
    Task<ContratoDto> AtualizarContratoAsync(Guid id, [Body] SalvarContratoRequest request, CancellationToken ct = default);

    [Post("/contratos/{id}/enviar")]
    Task<ContratoDto> EnviarContratoAsync(Guid id, CancellationToken ct = default);

    [Post("/contratos/{id}/desligar")]
    Task<ContratoDto> DesligarContratoAsync(Guid id, [Body] MotivoRequest request, CancellationToken ct = default);

    [Post("/contratos/{id}/renovar")]
    Task<ContratoDto> RenovarContratoAsync(Guid id, CancellationToken ct = default);

    [Get("/contratos/{id}/parcelas")]
    Task<List<CobrancaDto>> ParcelasContratoAsync(Guid id, CancellationToken ct = default);

    [Post("/contratos/{id}/boletos")]
    Task<EmissaoBoletosDto> EmitirBoletosAsync(Guid id, CancellationToken ct = default);

    [Get("/associacoes/{id}/pagamentos")]
    Task<PagamentosAssociacaoDto> PagamentosAssociacaoAsync(Guid id, CancellationToken ct = default);

    [Put("/associacoes/{id}/pagamentos")]
    Task<PagamentosAssociacaoDto> SalvarPagamentosAssociacaoAsync(Guid id, [Body] SalvarPagamentosAssociacaoRequest request, CancellationToken ct = default);

    [Get("/contratos/{id}/documento")]
    Task<HttpResponseMessage> DocumentoContratoAsync(Guid id, CancellationToken ct = default);

    // Presidência (aval das fichas de novos sócios)
    [Get("/presidencia/resumo")]
    Task<ResumoPresidenciaDto> PresidenciaResumoAsync(CancellationToken ct = default);

    [Get("/presidencia/fichas")]
    Task<List<FichaAprovacaoDto>> FichasAsync(CancellationToken ct = default);

    [Get("/presidencia/fichas/{id}/documento")]
    Task<HttpResponseMessage> DocumentoFichaAsync(Guid id, CancellationToken ct = default);

    [Post("/presidencia/fichas/{id}/aprovar")]
    Task<ContratoDto> AprovarFichaAsync(Guid id, CancellationToken ct = default);

    [Post("/presidencia/fichas/{id}/reprovar")]
    Task<ContratoDto> ReprovarFichaAsync(Guid id, [Body] MotivoRequest request, CancellationToken ct = default);

    // Eventos
    [Get("/eventos")]
    Task<List<EventoDto>> EventosAsync(bool? todos = null, CancellationToken ct = default);

    [Get("/eventos/{id}")]
    Task<EventoDto> EventoAsync(Guid id, CancellationToken ct = default);

    [Post("/eventos")]
    Task<EventoDto> CriarEventoAsync([Body] SalvarEventoRequest request, CancellationToken ct = default);

    [Put("/eventos/{id}")]
    Task<EventoDto> AtualizarEventoAsync(Guid id, [Body] SalvarEventoRequest request, CancellationToken ct = default);

    [Post("/eventos/disponibilidade")]
    Task<DisponibilidadeDto> VerificarAgendaAsync([Body] VerificarAgendaRequest request, CancellationToken ct = default);

    [Post("/eventos/{id}/publicar")]
    Task<EventoDto> PublicarEventoAsync(Guid id, CancellationToken ct = default);

    [Post("/eventos/{id}/cancelar")]
    Task<EventoDto> CancelarEventoAsync(Guid id, [Body] CancelarEventoRequest request, CancellationToken ct = default);

    [Get("/eventos/{id}/reservas")]
    Task<List<ReservaEventoDto>> ReservasDoEventoAsync(Guid id, CancellationToken ct = default);

    [Post("/eventos/reservas/{id}/cancelar")]
    Task<ReservaEventoDto> CancelarReservaEventoAsync(Guid id, CancellationToken ct = default);

    [Post("/eventos/{id}/reservas")]
    Task<ReservaEventoDto> ReservarEventoAsync(Guid id, [Body] ReservarEventoRequest request, CancellationToken ct = default);

    [Get("/eventos/minhas-reservas")]
    Task<List<ReservaEventoDto>> MinhasReservasEventoAsync(CancellationToken ct = default);

    [Post("/eventos/minhas-reservas/{id}/cancelar")]
    Task<ReservaEventoDto> CancelarMinhaReservaEventoAsync(Guid id, CancellationToken ct = default);

    // Avisos e comunicação
    [Get("/avisos")]
    Task<List<AvisoDto>> AvisosAsync(CancellationToken ct = default);

    [Get("/avisos/resumo")]
    Task<ResumoAvisosDto> ResumoAvisosAsync(CancellationToken ct = default);

    [Post("/avisos/lidos")]
    Task MarcarAvisosLidosAsync(CancellationToken ct = default);

    [Get("/avisos/preferencias")]
    Task<PreferenciasComunicacaoDto> PreferenciasComunicacaoAsync(CancellationToken ct = default);

    [Put("/avisos/preferencias")]
    Task<PreferenciasComunicacaoDto> SalvarPreferenciasComunicacaoAsync([Body] PreferenciasComunicacaoDto request, CancellationToken ct = default);

    [Get("/divulgacoes/canais")]
    Task<CanaisDisponiveisDto> CanaisComunicacaoAsync(CancellationToken ct = default);

    [Get("/divulgacoes")]
    Task<List<DivulgacaoDto>> DivulgacoesAsync(CancellationToken ct = default);

    [Post("/divulgacoes")]
    Task CriarDivulgacaoAsync([Body] NovaDivulgacaoRequest request, CancellationToken ct = default);

    [Get("/divulgacoes/{id}/envios")]
    Task<List<FalhaEnvioDto>> EnviosNaoRecebidosAsync(Guid id, CancellationToken ct = default);

    [Get("/eventos/{id}/post")]
    Task<PostRedesSociaisDto> PostEventoAsync(Guid id, CancellationToken ct = default);

    [Get("/eventos/{id}/post/imagem")]
    Task<HttpResponseMessage> ImagemPostEventoAsync(Guid id, CancellationToken ct = default);

    // Carteirinha
    [Get("/carteirinha")]
    Task<List<CarteirinhaDto>> CarteirinhasAsync(CancellationToken ct = default);

    // Sócios (secretaria)
    [Get("/socios")]
    Task<PaginaDto<SocioResumoDto>> SociosAsync(string? busca, string? genero = null, string? situacao = null,
        int pagina = 1, int tamanhoPagina = 30, CancellationToken ct = default);

    [Get("/socios/{id}/historico")]
    Task<List<HistoricoDto>> HistoricoSocioAsync(Guid id, int limite = 100, CancellationToken ct = default);

    [Get("/socios/{id}")]
    Task<SocioDto> SocioAsync(Guid id, CancellationToken ct = default);

    [Post("/socios")]
    Task<SocioCriadoResponse> CriarSocioAsync([Body] SalvarSocioRequest request, CancellationToken ct = default);

    [Put("/socios/{id}")]
    Task<SocioDto> AtualizarSocioAsync(Guid id, [Body] SalvarSocioRequest request, CancellationToken ct = default);

    [Patch("/socios/{id}/situacao")]
    Task<SocioDto> AlterarSituacaoAsync(Guid id, [Body] AlterarSituacaoRequest request, CancellationToken ct = default);

    [Post("/socios/{id}/renovar-qr")]
    Task RenovarQrAsync(Guid id, CancellationToken ct = default);

    [Post("/socios/{id}/dependentes")]
    Task<DependenteDto> CriarDependenteAsync(Guid id, [Body] SalvarDependenteRequest request, CancellationToken ct = default);

    [Put("/socios/{id}/dependentes/{dependenteId}")]
    Task<DependenteDto> AtualizarDependenteAsync(Guid id, Guid dependenteId, [Body] SalvarDependenteRequest request, CancellationToken ct = default);

    [Delete("/socios/{id}/dependentes/{dependenteId}")]
    Task<DependenteDto> InativarDependenteAsync(Guid id, Guid dependenteId, CancellationToken ct = default);

    [Post("/socios/{id}/dependentes/{dependenteId}/reativar")]
    Task<DependenteDto> ReativarDependenteAsync(Guid id, Guid dependenteId, CancellationToken ct = default);

    // Reservas
    [Get("/espacos")]
    Task<List<EspacoDto>> EspacosAsync(bool? todos = null, CancellationToken ct = default);

    [Post("/espacos")]
    Task<EspacoDto> CriarEspacoAsync([Body] SalvarEspacoRequest request, CancellationToken ct = default);

    [Put("/espacos/{id}")]
    Task<EspacoDto> AtualizarEspacoAsync(Guid id, [Body] SalvarEspacoRequest request, CancellationToken ct = default);

    [Get("/espacos/{id}/agenda")]
    Task<AgendaDto> AgendaAsync(Guid id, [Query(Format = "yyyy-MM-dd")] DateOnly data, CancellationToken ct = default);

    [Post("/reservas")]
    Task<ReservaDto> ReservarAsync([Body] CriarReservaRequest request, CancellationToken ct = default);

    [Get("/reservas/minhas")]
    Task<List<ReservaDto>> MinhasReservasAsync(CancellationToken ct = default);

    [Get("/reservas")]
    Task<List<ReservaDto>> ReservasAsync(string? status = null, [Query(Format = "yyyy-MM-dd")] DateOnly? data = null, CancellationToken ct = default);

    [Post("/reservas/{id}/cancelar")]
    Task<ReservaDto> CancelarReservaAsync(Guid id, CancellationToken ct = default);

    [Post("/reservas/{id}/aprovar")]
    Task<ReservaDto> AprovarReservaAsync(Guid id, CancellationToken ct = default);

    [Post("/reservas/{id}/recusar")]
    Task<ReservaDto> RecusarReservaAsync(Guid id, [Body] RecusarReservaRequest request, CancellationToken ct = default);

    // Financeiro
    [Get("/financeiro/minhas")]
    Task<ResumoFinanceiroDto> MeuFinanceiroAsync(CancellationToken ct = default);

    [Post("/financeiro/cobrancas/{id}/pagar")]
    Task<PagamentoDto> PagarAsync(Guid id, [Body] PagarRequest request, CancellationToken ct = default);

    [Post("/financeiro/pagamentos/{id}/simular-confirmacao")]
    Task SimularConfirmacaoAsync(Guid id, CancellationToken ct = default);

    [Get("/financeiro/cobrancas")]
    Task<List<CobrancaDto>> CobrancasAsync(string? status = null, bool? vencidas = null,
        [Query(Format = "yyyy-MM-dd")] DateOnly? competencia = null, Guid? socioId = null, CancellationToken ct = default);

    [Post("/financeiro/cobrancas")]
    Task<CobrancaDto> LancarCobrancaAsync([Body] LancarCobrancaRequest request, CancellationToken ct = default);

    [Post("/financeiro/mensalidades/gerar")]
    Task<GerarMensalidadesResponse> GerarMensalidadesAsync([Body] GerarMensalidadesRequest request, CancellationToken ct = default);

    [Post("/financeiro/cobrancas/{id}/baixa")]
    Task<CobrancaDto> BaixaManualAsync(Guid id, [Body] BaixaManualRequest request, CancellationToken ct = default);

    [Post("/financeiro/cobrancas/{id}/cancelar")]
    Task<CobrancaDto> CancelarCobrancaAsync(Guid id, CancellationToken ct = default);

    [Get("/financeiro/inadimplentes")]
    Task<List<InadimplenteDto>> InadimplentesAsync(CancellationToken ct = default);

    [Get("/financeiro/configuracao")]
    Task<ConfiguracaoFinanceiraDto> ConfiguracaoFinanceiraAsync(CancellationToken ct = default);

    [Put("/financeiro/configuracao")]
    Task<ConfiguracaoFinanceiraDto> SalvarConfiguracaoFinanceiraAsync([Body] ConfiguracaoFinanceiraDto request, CancellationToken ct = default);

    // Portaria
    [Post("/portaria/validar")]
    Task<ResultadoAcessoDto> ValidarQrAsync([Body] ValidarQrRequest request, CancellationToken ct = default);

    [Get("/portaria/acessos")]
    Task<List<RegistroAcessoDto>> AcessosAsync(int limite = 30, CancellationToken ct = default);
}
