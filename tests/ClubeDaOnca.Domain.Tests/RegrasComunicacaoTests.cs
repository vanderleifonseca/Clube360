using ClubeDaOnca.Domain.Comunicacao;
using ClubeDaOnca.Domain.Socios;

namespace ClubeDaOnca.Domain.Tests;

public class RegrasComunicacaoTests
{
    [Theory]
    [InlineData("(45) 99999-1234", "5545999991234")]
    [InlineData("+55 45 99999-1234", "5545999991234")]
    [InlineData("045999991234", "5545999991234")]
    [InlineData("(45) 3030-1234", null)] // fixo: sem WhatsApp
    [InlineData("1234", null)]
    [InlineData(null, null)]
    public void Celular_no_formato_do_whatsapp(string? telefone, string? esperado) =>
        Assert.Equal(esperado, RegrasComunicacao.TelefoneWhatsApp(telefone));

    [Fact]
    public void Respeita_preferencias_e_dados_do_socio()
    {
        var s = new Socio { Situacao = SituacaoTitulo.Ativo, Email = "a@b.com", Telefone = "(45) 99999-1234" };
        Assert.Null(RegrasComunicacao.MotivoParaNaoEnviar(s, CanalComunicacao.Email, true));
        Assert.Null(RegrasComunicacao.MotivoParaNaoEnviar(s, CanalComunicacao.WhatsApp, true));
        Assert.NotNull(RegrasComunicacao.MotivoParaNaoEnviar(s, CanalComunicacao.WhatsApp, false)); // canal não configurado

        s.ReceberEmail = false;
        Assert.Contains("desligou", RegrasComunicacao.MotivoParaNaoEnviar(s, CanalComunicacao.Email, true));
        s.Situacao = SituacaoTitulo.EmAdesao;
        Assert.Contains("não está ativo", RegrasComunicacao.MotivoParaNaoEnviar(s, CanalComunicacao.App, true));
    }

    [Fact]
    public void Resumir_texto() =>
        Assert.Equal("Festa da…", RegrasComunicacao.Resumir("Festa  da   Onça", 9));
}
