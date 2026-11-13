using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClubeDaOnca.Api.Infrastructure.Comunicacao;
using ClubeDaOnca.Api.Infrastructure.Email;
using ClubeDaOnca.Contracts.Associacoes;
using ClubeDaOnca.Contracts.Auth;
using ClubeDaOnca.Contracts.Comunicacao;
using ClubeDaOnca.Contracts.Eventos;
using ClubeDaOnca.Contracts.Reservas;
using ClubeDaOnca.Contracts.Socios;
using Microsoft.Extensions.DependencyInjection;

namespace ClubeDaOnca.Api.Tests;

public class ComunicacaoTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> ClienteAsync(string login, string senha)
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/auth/login", new LoginRequest(login, senha));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> AdminNovaUnidadeAsync()
    {
        var super = await ClienteAsync(ApiFactory.AdminEmail, ApiFactory.AdminSenha);
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var resp = await super.PostAsJsonAsync("/associacoes", new NovaAssociacaoRequest(
            $"Clube Com {sufixo}", null, null, null, null, "Admin", $"com{sufixo}@teste.local"));
        var u = (await resp.Content.ReadFromJsonAsync<AssociacaoCriadaResponse>())!;
        return await ClienteAsync(u.AdminLogin, u.SenhaProvisoria);
    }

    private async Task<(HttpClient Cliente, string Email, string Telefone)> SocioAsync(HttpClient admin, string telefone = "(45) 99876-5432")
    {
        var cpf = CpfTeste.Proximo();
        var email = $"com{cpf}@teste.local";
        var resp = await admin.PostAsJsonAsync("/socios", new SalvarSocioRequest(
            $"Sócio {cpf[..4]}", cpf, email, telefone, new DateOnly(1985, 1, 1), "Individual"));
        resp.EnsureSuccessStatusCode();
        var criado = (await resp.Content.ReadFromJsonAsync<SocioCriadoResponse>())!;
        await SocioTeste.AtivarAsync(admin, criado.Socio.Id);
        return (await ClienteAsync(criado.Login, criado.SenhaProvisoria!), email, telefone);
    }

    [Fact]
    public async Task Comunicado_chega_pelos_canais_respeitando_preferencias()
    {
        var admin = await AdminNovaUnidadeAsync();
        var (socioA, emailA, _) = await SocioAsync(admin);
        var (socioB, emailB, _) = await SocioAsync(admin, telefone: "(45) 3030-1234"); // fixo: sem WhatsApp

        // B desliga o e-mail.
        (await socioB.PutAsJsonAsync("/avisos/preferencias", new PreferenciasComunicacaoDto(true, false, true))).EnsureSuccessStatusCode();

        var resp = await admin.PostAsJsonAsync("/divulgacoes", new NovaDivulgacaoRequest(
            "Assembleia geral", "Dia 20, às 19h, no salão.", ["App", "Email", "WhatsApp"]));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        // Central de avisos (sininho).
        Assert.Equal(1, (await socioA.GetFromJsonAsync<ResumoAvisosDto>("/avisos/resumo"))!.NaoLidos);
        var avisos = (await socioA.GetFromJsonAsync<List<AvisoDto>>("/avisos"))!;
        Assert.Equal("Assembleia geral", Assert.Single(avisos).Titulo);
        (await socioA.PostAsync("/avisos/lidos", null)).EnsureSuccessStatusCode();
        Assert.Equal(0, (await socioA.GetFromJsonAsync<ResumoAvisosDto>("/avisos/resumo"))!.NaoLidos);

        // E-mail: A recebe, B não (desligou).
        var emails = factory.Services.GetRequiredService<EmailMemoria>().Enviados;
        Assert.Contains(emails, m => m.Para == emailA && m.Assunto == "Assembleia geral");
        Assert.DoesNotContain(emails, m => m.Para == emailB && m.Assunto == "Assembleia geral");

        // WhatsApp: só celular válido (55 + DDD + 9 dígitos).
        var zap = factory.Services.GetRequiredService<WhatsAppMemoria>().Enviados;
        Assert.Contains(zap, z => z.Telefone == "5545998765432" && z.Titulo == "Assembleia geral");

        // Relatório: quem não recebeu e por quê.
        var divulgacao = (await admin.GetFromJsonAsync<List<DivulgacaoDto>>("/divulgacoes"))!.Single(d => d.Titulo == "Assembleia geral");
        Assert.Equal("Concluida", divulgacao.Status);
        Assert.Equal(2, divulgacao.Totais.Single(t => t.Canal == "App").Enviados);
        var naoRecebidos = (await admin.GetFromJsonAsync<List<FalhaEnvioDto>>($"/divulgacoes/{divulgacao.Id}/envios"))!;
        Assert.Contains(naoRecebidos, f => f.Canal == "Email" && f.Detalhe!.Contains("desligou"));
        Assert.Contains(naoRecebidos, f => f.Canal == "WhatsApp" && f.Detalhe!.Contains("celular"));
    }

    [Fact]
    public async Task Publicar_evento_divulga_e_cancelar_avisa_quem_reservou()
    {
        var admin = await AdminNovaUnidadeAsync();
        var (socio, _, _) = await SocioAsync(admin);
        var local = (await (await admin.PostAsJsonAsync("/espacos", new SalvarEspacoRequest(
            "Salão", null, true, new TimeOnly(8, 0), new TimeOnly(0, 0), 60, [0, 1, 2, 3, 4, 5, 6], 0, 60, 5, 0m, false, 0, "Salao", 100)))
            .Content.ReadFromJsonAsync<EspacoDto>())!;
        var inicio = DateTime.Today.AddDays(9).AddHours(20);
        var evento = (await (await admin.PostAsJsonAsync("/eventos", new SalvarEventoRequest(
            "Noite do Samba", "Roda de samba", inicio, inicio.AddHours(4), 80, true, true, true, 24, [local.Id],
            [new SalvarAmbienteRequest(null, "Mesa", 4, 10, 1, 0m)]))).Content.ReadFromJsonAsync<EventoDto>())!;

        (await admin.PostAsync($"/eventos/{evento.Id}/publicar", null)).EnsureSuccessStatusCode();
        var avisos = (await socio.GetFromJsonAsync<List<AvisoDto>>("/avisos"))!;
        var aviso = Assert.Single(avisos, a => a.Tipo == "Evento");
        Assert.Equal(evento.Id, aviso.ReferenciaId);
        Assert.Contains("Reserve pelo app", aviso.Mensagem);

        // Post para redes sociais (evento aberto ao público).
        var post = (await admin.GetFromJsonAsync<PostRedesSociaisDto>($"/eventos/{evento.Id}/post"))!;
        Assert.Contains("Noite do Samba", post.Texto);
        var imagem = await admin.GetAsync(post.ImagemUrl);
        Assert.Equal("image/png", imagem.Content.Headers.ContentType?.MediaType);
        Assert.True((await imagem.Content.ReadAsByteArrayAsync()).Length > 10_000);

        // Cancelar: quem reservou recebe aviso.
        (await socio.PostAsJsonAsync($"/eventos/{evento.Id}/reservas", new ReservarEventoRequest(evento.Ambientes[0].Id, 2, null)))
            .EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/eventos/{evento.Id}/cancelar", new CancelarEventoRequest("Chuva"))).EnsureSuccessStatusCode();
        Assert.Contains((await socio.GetFromJsonAsync<List<AvisoDto>>("/avisos"))!, a => a.Titulo.StartsWith("Evento cancelado"));
    }
}
