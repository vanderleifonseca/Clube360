using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using ClubeDaOnca.Api.Infrastructure.Email;
using ClubeDaOnca.Contracts.Contratos;
using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Contratos;
using ClubeDaOnca.Domain.Financeiro;
using ClubeDaOnca.Domain.Socios;
using Microsoft.EntityFrameworkCore;

namespace ClubeDaOnca.Api.Infrastructure.Contratos;

/// <summary>Erro de regra do contrato: vira 409 (ou 400 com a lista de pendências do cadastro).</summary>
public sealed class ContratoException(string mensagem, IReadOnlyList<string>? pendencias = null) : Exception(mensagem)
{
    public IReadOnlyList<string>? Pendencias { get; } = pendencias;
}

/// <summary>
/// Fluxo do contrato de adesão: rascunho → envio por e-mail → aceite do candidato pelo link →
/// aval do presidente → (desligamento | renovação).
/// </summary>
public sealed class ContratoService(
    AppDbContext db,
    FinanceiroService financeiro,
    EmissaoBoletos emissao,
    Comunicacao.DivulgacaoService comunicacao,
    GeradorContrato gerador,
    IEnvioEmail email,
    RelogioClube relogio,
    UsuarioAtual usuario,
    IConfiguration config,
    ILogger<ContratoService> log)
{
    public async Task<Contrato> CriarAsync(Guid socioId, SalvarContratoRequest req, Guid? contratoAnteriorId = null, CancellationToken ct = default)
    {
        var socio = await db.Socios.FirstOrDefaultAsync(s => s.Id == socioId, ct)
            ?? throw new KeyNotFoundException("Sócio não encontrado.");

        var pendencias = RegrasContrato.PendenciasCadastro(socio);
        if (pendencias.Count > 0)
            throw new ContratoException("Complete o cadastro do sócio antes de gerar o contrato.", pendencias);

        var abertos = await db.Contratos.Where(c => c.SocioId == socioId && c.Status != StatusContrato.Reprovado
                && c.Status != StatusContrato.Desligado && c.Status != StatusContrato.Encerrado)
            .Select(c => new { c.Id, c.Numero, c.Status }).ToListAsync(ct);
        // Só a renovação pode existir ao lado do contrato aprovado que ela vai substituir.
        if (abertos.Any(c => c.Id != contratoAnteriorId))
            throw new ContratoException($"Este sócio já tem o contrato {abertos.First(c => c.Id != contratoAnteriorId).Numero} em andamento.");

        var cfg = await financeiro.ConfiguracaoAsync(ct);
        var contrato = new Contrato { SocioId = socioId, ContratoAnteriorId = contratoAnteriorId, Categoria = socio.Categoria };
        Aplicar(contrato, req, cfg);
        await NumerarAsync(contrato, ct);
        db.Contratos.Add(contrato);
        await db.SaveChangesAsync(ct);
        return contrato;
    }

    public async Task<Contrato> AtualizarAsync(Guid id, SalvarContratoRequest req, CancellationToken ct = default)
    {
        var contrato = await BuscarAsync(id, ct);
        if (!RegrasContrato.Editavel(contrato.Status))
            throw new ContratoException("Só é possível alterar o contrato antes do aceite do candidato.");

        Aplicar(contrato, req, await financeiro.ConfiguracaoAsync(ct));
        if (contrato.Status == StatusContrato.AguardandoAceite)
        {
            // Proposta mudou: o link enviado deixa de valer e é preciso reenviar.
            contrato.Status = StatusContrato.Rascunho;
            contrato.TokenAceiteHash = null;
            contrato.TokenExpiraEm = null;
        }
        await db.SaveChangesAsync(ct);
        return contrato;
    }

    /// <summary>Gera o documento e envia a proposta ao e-mail do candidato com o link de aceite (também reenvia).</summary>
    public async Task<Contrato> EnviarAsync(Guid id, string urlPublica, CancellationToken ct = default)
    {
        var contrato = await BuscarAsync(id, ct, comSocio: true);
        if (!RegrasContrato.PodeMudar(contrato.Status, StatusContrato.AguardandoAceite))
            throw new ContratoException("Este contrato não pode mais ser enviado ao candidato.");

        var socio = contrato.Socio!;
        var pendencias = RegrasContrato.PendenciasCadastro(socio);
        if (pendencias.Count > 0)
            throw new ContratoException("Complete o cadastro do sócio antes de enviar o contrato.", pendencias);

        contrato.Dependentes = await db.Dependentes.Where(d => d.SocioId == socio.Id && d.Ativo).OrderBy(d => d.Nome)
            .Select(d => new DependenteContrato(d.Nome, d.Parentesco, d.DataNascimento, d.Cpf)).ToListAsync(ct);

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        contrato.TokenAceiteHash = TokenService.Hash(token);
        contrato.TokenExpiraEm = relogio.UtcAgora.AddDays(RegrasContrato.DiasValidadeLink);
        contrato.Status = StatusContrato.AguardandoAceite;
        contrato.EnviadoEm = relogio.UtcAgora;
        contrato.EnviadoPara = socio.Email;

        var associacao = await AssociacaoAsync(ct);
        await GerarDocumentoAsync(contrato, socio, associacao, ct);

        var link = $"{urlPublica.TrimEnd('/')}/aceite/{token}";
        await email.EnviarAsync(new MensagemEmail(
            socio.Email!, socio.Nome, $"Proposta de adesão nº {contrato.Numero} — {associacao.Nome}",
            HtmlProposta(contrato, socio, associacao, link),
            [new AnexoEmail($"Contrato-{contrato.Numero.Replace('/', '-')}.{contrato.DocumentoTipo}",
                GeradorContrato.MimeType(contrato.DocumentoTipo), contrato.Documento!)],
            associacao.Nome), ct);

        await db.SaveChangesAsync(ct);
        log.LogInformation("Contrato {Numero} enviado para {Email}.", contrato.Numero, socio.Email);
        return contrato;
    }

    /// <summary>Localiza a proposta pelo token do link (sem login; ignora o filtro de associação e passa a operar nela).</summary>
    public async Task<Contrato?> PorTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return null;
        var hash = TokenService.Hash(token);
        var contrato = await db.Contratos.IgnoreQueryFilters().Include(c => c.Socio)
            .FirstOrDefaultAsync(c => c.TokenAceiteHash == hash, ct);
        if (contrato is not null) db.Tenant.Definir(contrato.AssociacaoId);
        return contrato;
    }

    public bool LinkValido(Contrato c) => c.Status == StatusContrato.AguardandoAceite && c.TokenExpiraEm > relogio.UtcAgora;

    public async Task RegistrarAceiteAsync(Contrato contrato, IPAddress? ip, CancellationToken ct = default)
    {
        if (!LinkValido(contrato)) throw new ContratoException("Esta proposta não está mais disponível para aceite.");
        contrato.Status = StatusContrato.EmAnalise;
        contrato.AceiteEm = relogio.UtcAgora;
        contrato.AceiteIp = ip?.ToString();
        await GerarDocumentoAsync(contrato, contrato.Socio!, await AssociacaoAsync(ct), ct);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Contrato {Numero}: aceite registrado (IP {Ip}).", contrato.Numero, contrato.AceiteIp);
    }

    // ---------- Presidente ----------

    public async Task<MandatoPresidente?> MandatoVigenteAsync(CancellationToken ct = default)
    {
        if (db.Tenant.AssociacaoId is not { } associacaoId) return null;
        var hoje = relogio.Hoje;
        return await db.Mandatos.AsNoTracking()
            .Where(m => m.AssociacaoId == associacaoId && m.Inicio <= hoje && m.Fim >= hoje)
            .OrderByDescending(m => m.Inicio).FirstOrDefaultAsync(ct);
    }

    public static bool EhPresidente(ClaimsPrincipal user, MandatoPresidente? mandato) =>
        mandato?.Email is { Length: > 0 } emailMandato
        && string.Equals(user.FindFirst("email")?.Value, emailMandato, StringComparison.OrdinalIgnoreCase);

    public async Task<Contrato> AprovarAsync(Guid id, CancellationToken ct = default)
    {
        var contrato = await BuscarAsync(id, ct, comSocio: true);
        if (!RegrasContrato.PodeMudar(contrato.Status, StatusContrato.Aprovado))
            throw new ContratoException("Só é possível aprovar fichas em análise (após o aceite do candidato).");

        var inicio = relogio.Hoje;
        if (contrato.ContratoAnteriorId is { } anteriorId)
        {
            var anterior = await db.Contratos.FirstOrDefaultAsync(c => c.Id == anteriorId, ct);
            if (anterior is { Status: StatusContrato.Aprovado })
            {
                anterior.Status = StatusContrato.Encerrado;
                if (anterior.VigenciaFim is { } fimAnterior && fimAnterior >= inicio) inicio = fimAnterior.AddDays(1);
            }
        }

        (contrato.VigenciaInicio, contrato.VigenciaFim) = RegrasContrato.Vigencia(inicio);
        contrato.Status = StatusContrato.Aprovado;
        contrato.AprovadoEm = relogio.UtcAgora;
        contrato.AprovadoPor = usuario.Nome;
        contrato.AprovadoPorUsuarioId = usuario.Id;
        contrato.TokenAceiteHash = null;
        contrato.Socio!.Situacao = SituacaoTitulo.Ativo;

        await GerarDocumentoAsync(contrato, contrato.Socio, await AssociacaoAsync(ct), ct);
        var parcelas = await financeiro.GerarParcelasContratoAsync(contrato, ct);
        await comunicacao.AvisarAsync(contrato.Socio, Domain.Comunicacao.TipoAviso.Contrato, "Associação aprovada! 🎉",
            $"Seu contrato {contrato.Numero} foi aprovado. Bem-vindo(a)! Sua carteirinha já vale na portaria" +
            (parcelas > 0 ? $" e as {parcelas} parcelas estão na aba Financeiro." : "."), contrato.Id, porEmail: true, ct);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Contrato {Numero} aprovado por {Presidente}; {Parcelas} parcela(s) geradas.", contrato.Numero, usuario.Nome, parcelas);
        if (parcelas > 0) await emissao.SolicitarAsync(contrato.Id, ct);
        return contrato;
    }

    public async Task<Contrato> ReprovarAsync(Guid id, string motivo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ContratoException("Informe o motivo da reprovação.");
        var contrato = await BuscarAsync(id, ct, comSocio: true);
        if (!RegrasContrato.PodeMudar(contrato.Status, StatusContrato.Reprovado))
            throw new ContratoException("Só é possível reprovar fichas em análise.");

        contrato.Status = StatusContrato.Reprovado;
        contrato.MotivoReprovacao = motivo.Trim();
        contrato.AprovadoEm = relogio.UtcAgora;
        contrato.AprovadoPor = usuario.Nome;
        contrato.AprovadoPorUsuarioId = usuario.Id;
        contrato.TokenAceiteHash = null;
        // Candidato reprovado não entra; sócio antigo em renovação continua como está.
        if (contrato.Socio!.Situacao == SituacaoTitulo.EmAdesao) contrato.Socio.Situacao = SituacaoTitulo.Cancelado;
        await db.SaveChangesAsync(ct);
        return contrato;
    }

    // ---------- Desligamento e renovação ----------

    public async Task<Contrato> DesligarAsync(Guid id, string motivo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ContratoException("Informe o motivo do desligamento.");
        var contrato = await BuscarAsync(id, ct, comSocio: true);
        if (!RegrasContrato.PodeMudar(contrato.Status, StatusContrato.Desligado))
            throw new ContratoException("Só é possível desligar a partir de um contrato aprovado.");

        contrato.Status = StatusContrato.Desligado;
        contrato.DesligadoEm = relogio.UtcAgora;
        contrato.MotivoDesligamento = motivo.Trim();
        contrato.Socio!.Situacao = SituacaoTitulo.Cancelado;

        // Parcelas que ainda vão vencer deixam de ser devidas (cláusula 8.2); as vencidas continuam.
        var hoje = relogio.Hoje;
        var futuras = await db.Cobrancas.Where(c => c.SocioId == contrato.SocioId && c.Status == StatusCobranca.Aberta
            && c.Tipo == TipoCobranca.Mensalidade && c.Vencimento > hoje).ToListAsync(ct);
        foreach (var c in futuras)
        {
            c.Status = StatusCobranca.Cancelada;
            c.Observacao = $"Desligamento (contrato {contrato.Numero})";
        }
        await financeiro.CancelarNoGatewayAsync(futuras.Select(c => c.Id).ToList(), ct);

        await db.SaveChangesAsync(ct);
        return contrato;
    }

    /// <summary>Renovação manual: novo rascunho com os dados atuais do cadastro, que passa de novo por aceite e aval.</summary>
    public async Task<Contrato> RenovarAsync(Guid id, CancellationToken ct = default)
    {
        var anterior = await BuscarAsync(id, ct);
        if (anterior.Status != StatusContrato.Aprovado)
            throw new ContratoException("Só é possível renovar um contrato aprovado.");
        if (await db.Contratos.AnyAsync(c => c.ContratoAnteriorId == id && c.Status != StatusContrato.Reprovado, ct))
            throw new ContratoException("Já existe uma renovação para este contrato.");

        var cfg = await financeiro.ConfiguracaoAsync(ct);
        var primeiro = anterior.VigenciaFim is { } fim
            ? RegrasContrato.PrimeiroVencimento(fim, cfg.DiaVencimento)
            : RegrasContrato.PrimeiroVencimento(relogio.Hoje, cfg.DiaVencimento);
        return await CriarAsync(anterior.SocioId, new SalvarContratoRequest(
            anterior.Categoria.ToString(), anterior.ValorMensalidade, anterior.DiaVencimento, primeiro, anterior.RenovacaoAutomatica), id, ct);
    }

    /// <summary>
    /// Job diário: contratos aprovados com renovação automática e vigência vencida ganham um novo contrato
    /// (já aprovado, com os dados atuais do cadastro). O anterior fica Encerrado.
    /// </summary>
    public async Task<int> RenovarVencidosAsync(CancellationToken ct = default)
    {
        var hoje = relogio.Hoje;
        var vencidos = await db.Contratos.Include(c => c.Socio)
            .Where(c => c.Status == StatusContrato.Aprovado && c.RenovacaoAutomatica && c.VigenciaFim < hoje)
            .ToListAsync(ct);
        if (vencidos.Count == 0) return 0;

        var associacao = await AssociacaoAsync(ct);
        var cfg = await financeiro.ConfiguracaoAsync(ct);
        var renovados = 0;
        foreach (var anterior in vencidos)
        {
            if (await db.Contratos.AnyAsync(c => c.ContratoAnteriorId == anterior.Id && c.Status != StatusContrato.Reprovado, ct))
                continue; // renovação manual em andamento

            var socio = anterior.Socio!;
            var inicio = anterior.VigenciaFim!.Value.AddDays(1);
            var novo = new Contrato
            {
                SocioId = socio.Id,
                ContratoAnteriorId = anterior.Id,
                Categoria = socio.Categoria,
                ValorMensalidade = cfg.ValorPara(socio.Categoria),
                DiaVencimento = cfg.DiaVencimento,
                PrimeiroVencimento = RegrasContrato.PrimeiroVencimento(anterior.VigenciaFim.Value, cfg.DiaVencimento),
                RenovacaoAutomatica = true,
                Status = StatusContrato.Aprovado,
                AprovadoEm = relogio.UtcAgora,
                AprovadoPor = "Renovação automática",
                Dependentes = await db.Dependentes.Where(d => d.SocioId == socio.Id && d.Ativo).OrderBy(d => d.Nome)
                    .Select(d => new DependenteContrato(d.Nome, d.Parentesco, d.DataNascimento, d.Cpf)).ToListAsync(ct),
            };
            (novo.VigenciaInicio, novo.VigenciaFim) = RegrasContrato.Vigencia(inicio);
            await NumerarAsync(novo, ct);
            novo.Socio = socio;
            await GerarDocumentoAsync(novo, socio, associacao, ct);
            anterior.Status = StatusContrato.Encerrado;
            db.Contratos.Add(novo);
            var parcelas = await financeiro.GerarParcelasContratoAsync(novo, ct);
            await db.SaveChangesAsync(ct);
            if (parcelas > 0) await emissao.SolicitarAsync(novo.Id, ct);
            renovados++;
        }
        return renovados;
    }

    // ---------- Apoio ----------

    public async Task<Contrato> BuscarAsync(Guid id, CancellationToken ct, bool comSocio = false)
    {
        var query = db.Contratos.AsQueryable();
        if (comSocio) query = query.Include(c => c.Socio);
        return await query.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new KeyNotFoundException("Contrato não encontrado.");
    }

    public async Task<Associacao> AssociacaoAsync(CancellationToken ct)
    {
        var id = db.Tenant.Exigir();
        return await db.Associacoes.FirstAsync(a => a.Id == id, ct);
    }

    private async Task GerarDocumentoAsync(Contrato contrato, Socio socio, Associacao associacao, CancellationToken ct)
    {
        var presidente = await MandatoVigenteAsync(ct);
        var cfg = await financeiro.ConfiguracaoAsync(ct);
        var (conteudo, tipo) = await gerador.GerarAsync(new DadosContrato(
            contrato, socio, associacao, presidente?.Nome, cfg.DiasTolerancia, relogio.Hoje, relogio.ParaLocal), ct);
        contrato.Documento = conteudo;
        contrato.DocumentoTipo = tipo;
        contrato.DocumentoGeradoEm = relogio.UtcAgora;
    }

    private void Aplicar(Contrato c, SalvarContratoRequest req, ConfiguracaoFinanceira cfg)
    {
        if (req.Categoria is { Length: > 0 } cat)
        {
            if (!Enum.TryParse<CategoriaTitulo>(cat, true, out var categoria) || !Enum.IsDefined(categoria) || int.TryParse(cat, out _))
                throw new ContratoException("Categoria inválida. Use Individual, Familiar ou Remido.");
            c.Categoria = categoria;
        }

        var valor = req.ValorMensalidade ?? cfg.ValorPara(c.Categoria);
        if (valor < 0 || valor > 100_000) throw new ContratoException("Valor da mensalidade inválido.");
        c.ValorMensalidade = Math.Round(valor, 2);

        var dia = req.DiaVencimento ?? cfg.DiaVencimento;
        if (dia is < 1 or > 28) throw new ContratoException("O dia de vencimento deve ficar entre 1 e 28.");
        c.DiaVencimento = dia;

        var primeiro = req.PrimeiroVencimento ?? RegrasContrato.PrimeiroVencimento(relogio.Hoje, dia);
        if (primeiro < relogio.Hoje) throw new ContratoException("O primeiro vencimento não pode estar no passado.");
        c.PrimeiroVencimento = primeiro;

        if (req.RenovacaoAutomatica is { } renova) c.RenovacaoAutomatica = renova;
    }

    private async Task NumerarAsync(Contrato c, CancellationToken ct)
    {
        c.Ano = relogio.Hoje.Year;
        var ultimo = await db.Contratos.Where(x => x.Ano == c.Ano).MaxAsync(x => (int?)x.Sequencial, ct) ?? 0;
        c.Sequencial = ultimo + 1;
        c.Numero = RegrasContrato.Numero(c.Ano, c.Sequencial);
    }

    public string UrlPublica(HttpRequest request) =>
        config["Contratos:UrlPublica"] is { Length: > 0 } url ? url : $"{request.Scheme}://{request.Host}";

    private string HtmlProposta(Contrato c, Socio s, Associacao a, string link)
    {
        static string H(string? v) => WebUtility.HtmlEncode(v ?? string.Empty);
        var primeiroNome = s.Nome.Split(' ')[0];
        var expira = relogio.ParaLocal(c.TokenExpiraEm!.Value);
        return $"""
            <div style="font-family:Arial,sans-serif;max-width:560px;margin:auto;color:#222">
              {Marca.ImgEmail}
              <h2 style="color:#B45309;text-align:center">{H(a.Nome)}</h2>
              <p>Olá, {H(primeiroNome)}!</p>
              <p>Segue a sua proposta de adesão ao quadro associativo (contrato nº <b>{H(c.Numero)}</b>),
                 com mensalidade de <b>R$ {c.ValorMensalidade:N2}</b>. O contrato completo está em anexo.</p>
              <p>Para concordar com a proposta, clique no botão abaixo:</p>
              <p style="text-align:center;margin:28px 0">
                <a href="{H(link)}" style="background:#B45309;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">
                  Ver proposta e concordar</a></p>
              <p style="font-size:13px;color:#666">O link vale até {expira:dd/MM/yyyy HH:mm}. Depois do seu aceite, a ficha
                 segue para a aprovação do presidente da associação.</p>
              <p style="font-size:13px;color:#666">Se você não solicitou a adesão, ignore este e-mail.</p>
            </div>
            """;
    }
}
