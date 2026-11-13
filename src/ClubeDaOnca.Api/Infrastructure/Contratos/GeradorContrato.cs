using System.Globalization;
using System.Text.RegularExpressions;
using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Comum;
using ClubeDaOnca.Domain.Contratos;
using ClubeDaOnca.Domain.Socios;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace ClubeDaOnca.Api.Infrastructure.Contratos;

/// <summary>Dados usados para preencher o modelo.</summary>
public sealed record DadosContrato(
    Contrato Contrato,
    Socio Socio,
    Associacao Associacao,
    string? Presidente,
    int DiasTolerancia,
    DateOnly Hoje,
    Func<DateTimeOffset, DateTime> ParaLocal);

/// <summary>
/// Preenche o modelo .docx (Templates/ContratoAdesao.docx) trocando os campos {{...}}
/// e gera o PDF pelo LibreOffice (ou mantém o .docx).
/// </summary>
public sealed partial class GeradorContrato(IWebHostEnvironment env, ConversorPdf conversor)
{
    public const string ArquivoModelo = "ContratoAdesao.docx";
    private static readonly CultureInfo PtBr = new("pt-BR");
    private const string SemValor = "—";

    public byte[] Modelo()
    {
        var caminhos = new[]
        {
            Path.Combine(env.ContentRootPath, "Templates", ArquivoModelo),
            Path.Combine(AppContext.BaseDirectory, "Templates", ArquivoModelo),
        };
        var caminho = caminhos.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Modelo de contrato não encontrado.", caminhos[0]);
        return File.ReadAllBytes(caminho);
    }

    /// <summary>Gera o documento; Tipo = "pdf" ou "docx".</summary>
    public async Task<(byte[] Conteudo, string Tipo)> GerarAsync(DadosContrato dados, CancellationToken ct = default)
    {
        var docx = Preencher(Modelo(), Campos(dados), dados.Contrato.Dependentes.Select(CamposDependente).ToList(), Marca.Logo);
        var pdf = await conversor.ConverterAsync(docx, ct);
        return pdf is null ? (docx, "docx") : (pdf, "pdf");
    }

    public static string MimeType(string? tipo) => tipo == "pdf"
        ? "application/pdf"
        : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public static Dictionary<string, string> Campos(DadosContrato d)
    {
        var c = d.Contrato;
        var s = d.Socio;
        var a = d.Associacao;
        // Antes da aprovação a vigência é uma previsão a partir de hoje; o documento final traz as datas reais.
        var (inicio, fim) = c.VigenciaInicio is { } vi && c.VigenciaFim is { } vf ? (vi, vf) : RegrasContrato.Vigencia(d.Hoje);

        return new Dictionary<string, string>
        {
            ["Associacao.Nome"] = a.Nome,
            ["Associacao.Cnpj"] = string.IsNullOrWhiteSpace(a.Cnpj) ? SemValor : Cnpj.Formatar(a.Cnpj),
            ["Associacao.Endereco"] = Valor(a.Endereco.ToString()),
            ["Associacao.Cidade"] = Valor(a.Endereco.Cidade),
            ["Associacao.Uf"] = Valor(a.Endereco.Uf),
            ["Presidente.Nome"] = Valor(d.Presidente),

            ["Socio.Nome"] = s.Nome,
            ["Socio.Nacionalidade"] = Minuscula(s.Nacionalidade),
            ["Socio.EstadoCivil"] = EstadoCivilTexto(s.EstadoCivil),
            ["Socio.Profissao"] = Minuscula(s.Profissao),
            ["Socio.DataNascimento"] = s.DataNascimento.ToString("dd/MM/yyyy", PtBr),
            ["Socio.Rg"] = Valor(s.Rg),
            ["Socio.Cpf"] = FormatarCpf(s.Cpf),
            ["Socio.Endereco"] = Valor(s.Endereco.ToString()),
            ["Socio.Email"] = Valor(s.Email),
            ["Socio.Telefone"] = Valor(s.Telefone),
            ["Socio.NumeroTitulo"] = s.NumeroTitulo.ToString(PtBr),

            ["Contrato.Numero"] = c.Numero,
            ["Contrato.Categoria"] = c.Categoria.ToString(),
            ["Contrato.VigenciaInicio"] = inicio.ToString("dd/MM/yyyy", PtBr),
            ["Contrato.VigenciaFim"] = fim.ToString("dd/MM/yyyy", PtBr),
            ["Contrato.Valor"] = c.ValorMensalidade.ToString("N2", PtBr),
            ["Contrato.ValorExtenso"] = ValorPorExtenso.Reais(c.ValorMensalidade),
            ["Contrato.DiaVencimento"] = c.DiaVencimento.ToString(PtBr),
            ["Contrato.PrimeiroVencimento"] = c.PrimeiroVencimento.ToString("dd/MM/yyyy", PtBr),
            ["Contrato.DiasTolerancia"] = d.DiasTolerancia.ToString(PtBr),
            ["Contrato.Data"] = (c.AprovadoEm is { } ap ? DateOnly.FromDateTime(d.ParaLocal(ap)) : d.Hoje).ToString("d 'de' MMMM 'de' yyyy", PtBr),
            ["Contrato.AceiteEm"] = c.AceiteEm is { } ac ? d.ParaLocal(ac).ToString("dd/MM/yyyy 'às' HH:mm", PtBr) : "(pendente)",
            ["Contrato.AceiteIp"] = Valor(c.AceiteIp),
            ["Contrato.AprovadoEm"] = c.AprovadoEm is { } apr && c.Status is StatusContrato.Aprovado or StatusContrato.Desligado or StatusContrato.Encerrado
                ? d.ParaLocal(apr).ToString("dd/MM/yyyy 'às' HH:mm", PtBr) : "(pendente)",
        };
    }

    public static Dictionary<string, string> CamposDependente(DependenteContrato d) => new()
    {
        ["Dependente.Nome"] = d.Nome,
        ["Dependente.Parentesco"] = d.Parentesco switch
        {
            Parentesco.Conjuge => "Cônjuge",
            Parentesco.Filho => "Filho(a)",
            Parentesco.Enteado => "Enteado(a)",
            Parentesco.Mae => "Mãe",
            _ => d.Parentesco.ToString(),
        },
        ["Dependente.DataNascimento"] = d.DataNascimento.ToString("dd/MM/yyyy", PtBr),
        ["Dependente.Cpf"] = d.Cpf is null ? SemValor : FormatarCpf(d.Cpf),
    };

    /// <summary>Troca os campos no corpo, cabeçalhos e rodapés; repete a linha de dependentes da tabela.</summary>
    public static byte[] Preencher(byte[] modelo, IReadOnlyDictionary<string, string> campos, IReadOnlyList<Dictionary<string, string>> dependentes, byte[]? logo = null)
    {
        using var ms = new MemoryStream();
        ms.Write(modelo);
        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var main = doc.MainDocumentPart ?? throw new InvalidOperationException("Modelo de contrato inválido.");
            var corpo = main.Document!.Body!;

            var linhaModelo = corpo.Descendants<TableRow>().FirstOrDefault(r => r.InnerText.Contains("{{Dependente."));
            if (linhaModelo is not null)
            {
                if (dependentes.Count == 0)
                {
                    var tabela = linhaModelo.Ancestors<Table>().First();
                    tabela.InsertBeforeSelf(new Paragraph(new Run(new Text("Não há dependentes vinculados a este contrato."))));
                    tabela.Remove();
                }
                else
                {
                    foreach (var dep in dependentes)
                    {
                        var linha = (TableRow)linhaModelo.CloneNode(true);
                        Substituir(linha, dep);
                        linhaModelo.InsertBeforeSelf(linha);
                    }
                    linhaModelo.Remove();
                }
            }

            Substituir(corpo, campos);
            foreach (var h in main.HeaderParts) Substituir(h.Header!, campos);
            foreach (var f in main.FooterParts) Substituir(f.Footer!, campos);
            if (logo is not null) AdicionarLogo(main, logo);
            main.Document.Save();
        }
        return ms.ToArray();
    }

    // Logo no cabeçalho: 2,2 cm de altura, proporção do PNG da marca (427 × 512).
    private const long AlturaLogoEmu = 792_000;
    private const long LarguraLogoEmu = AlturaLogoEmu * 427 / 512;

    /// <summary>
    /// Põe a logo, centralizada, no cabeçalho padrão de cada seção (aparece em todas as páginas).
    /// Se o modelo já tiver cabeçalho, a logo entra antes do conteúdo dele; se já tiver imagem, não mexe.
    /// </summary>
    public static void AdicionarLogo(MainDocumentPart main, byte[] png)
    {
        var corpo = main.Document!.Body!;
        var secoes = corpo.Descendants<SectionProperties>().ToList();
        if (secoes.Count == 0)
        {
            var nova = new SectionProperties();
            corpo.Append(nova);
            secoes.Add(nova);
        }

        foreach (var secao in secoes)
        {
            var referencia = secao.Elements<HeaderReference>()
                .FirstOrDefault(h => h.Type is null || h.Type.Value == HeaderFooterValues.Default);
            HeaderPart parte;
            if (referencia?.Id?.Value is { } id && main.GetPartById(id) is HeaderPart existente)
            {
                parte = existente;
            }
            else
            {
                parte = main.AddNewPart<HeaderPart>();
                parte.Header = new Header();
                secao.PrependChild(new HeaderReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(parte) });
            }

            if (parte.Header!.Descendants<A.Blip>().Any()) continue;
            var imagem = parte.AddImagePart(ImagePartType.Png);
            using (var dados = new MemoryStream(png)) imagem.FeedData(dados);
            parte.Header.PrependChild(ParagrafoLogo(parte.GetIdOfPart(imagem)));
            parte.Header.Save();
        }
    }

    private static Paragraph ParagrafoLogo(string relacao)
    {
        var inline = new DW.Inline(
            new DW.Extent { Cx = LarguraLogoEmu, Cy = AlturaLogoEmu },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = 1U, Name = "Logo" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 0U, Name = "logo.png" },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(new A.Blip { Embed = relacao }, new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = LarguraLogoEmu, Cy = AlturaLogoEmu }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U };

        return new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }, new SpacingBetweenLines { After = "120" }),
            new Run(new Drawing(inline)));
    }

    [GeneratedRegex(@"\{\{\s*([\w.]+)\s*\}\}")]
    private static partial Regex Campo();

    private static void Substituir(OpenXmlElement raiz, IReadOnlyDictionary<string, string> campos)
    {
        foreach (var paragrafo in raiz.Descendants<Paragraph>().ToList())
        {
            if (!paragrafo.InnerText.Contains("{{")) continue;
            var textos = paragrafo.Descendants<Text>().ToList();

            // Caso normal: cada campo inteiro dentro de um mesmo trecho (mantém a formatação).
            foreach (var t in textos.Where(t => t.Text.Contains("{{")))
                Trocar(t, campos);

            // Campo quebrado em vários trechos (o Word às vezes divide ao editar): junta o parágrafo num trecho só.
            if (paragrafo.InnerText.Contains("{{") && Campo().IsMatch(paragrafo.InnerText) && textos.Count > 0)
            {
                textos[0].Text = paragrafo.InnerText;
                foreach (var t in textos.Skip(1)) t.Text = string.Empty;
                Trocar(textos[0], campos);
            }
        }
    }

    private static void Trocar(Text t, IReadOnlyDictionary<string, string> campos)
    {
        t.Text = Campo().Replace(t.Text, m => campos.TryGetValue(m.Groups[1].Value, out var v) ? v : SemValor);
        t.Space = SpaceProcessingModeValues.Preserve;
    }

    private static string Valor(string? v) => string.IsNullOrWhiteSpace(v) ? SemValor : v.Trim();

    private static string Minuscula(string? v) =>
        string.IsNullOrWhiteSpace(v) ? SemValor : char.ToLower(v.Trim()[0], PtBr) + v.Trim()[1..];

    private static string FormatarCpf(string cpf) =>
        cpf.Length == 11 ? $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}" : cpf;

    private static string EstadoCivilTexto(EstadoCivil e) => e switch
    {
        EstadoCivil.Solteiro => "solteiro(a)",
        EstadoCivil.Casado => "casado(a)",
        EstadoCivil.UniaoEstavel => "em união estável",
        EstadoCivil.Separado => "separado(a)",
        EstadoCivil.Divorciado => "divorciado(a)",
        EstadoCivil.Viuvo => "viúvo(a)",
        _ => SemValor,
    };
}
