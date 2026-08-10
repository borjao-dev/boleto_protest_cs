using BoletoProtest.Core.Models;
using BoletoProtest.Infrastructure.Services;
using Xunit;

namespace BoletoProtest.Infrastructure.Tests;

public class BoletoParserServiceTest
{
    private const string UrlBoleto = "https://prosiga.protest.com.br/Operacional";

    private static readonly string ConteudoEmailReal = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "TestData", "ConteudoEmailReal.txt")
    );

    [Fact]
    public void ExtraiBoletosDoConteudo_ComEmailReal_DeveEncontrarQuatroBoletos()
    {
        List<BoletoEncontrado> resultado = BoletoParserService.ExtraiBoletosDoConteudo(
            ConteudoEmailReal,
            UrlBoleto
        );

        // O email real tem 4 blocos de boleto empilhados, cada URL repetida 2x
        // (href + texto visível) — o resultado já deve vir deduplicado.
        Assert.Equal(4, resultado.Count);
    }

    [Fact]
    public void ExtraiBoletosDoConteudo_ComEmailReal_NaoDeveIncluirLinkDeDescadastro()
    {
        List<BoletoEncontrado> resultado = BoletoParserService.ExtraiBoletosDoConteudo(
            ConteudoEmailReal,
            UrlBoleto
        );

        Assert.DoesNotContain(resultado, boleto => boleto.Link.Contains("RemoverEmail"));
    }

    [Fact]
    public void ExtraiBoletosDoConteudo_ComEmailReal_TodasAsUrlsDevemSerUnicas()
    {
        List<BoletoEncontrado> resultado = BoletoParserService.ExtraiBoletosDoConteudo(
            ConteudoEmailReal,
            UrlBoleto
        );

        List<string> links = resultado.Select(boleto => boleto.Link).ToList();

        Assert.Equal(links.Count, links.Distinct().Count());
    }

    [Fact]
    public void ExtraiBoletosDoConteudo_ComEmailReal_TodasAsUrlsDevemConterAmpersandDecodificado()
    {
        List<BoletoEncontrado> resultado = BoletoParserService.ExtraiBoletosDoConteudo(
            ConteudoEmailReal,
            UrlBoleto
        );

        Assert.All(resultado, boleto => Assert.DoesNotContain("&amp;", boleto.Link));
        Assert.All(resultado, boleto => Assert.Contains("&cont=", boleto.Link));
    }

    [Fact]
    public void ExtraiBoletosDoConteudo_ComEmailReal_TodosOsBoletosDevemTerMesmoVencimento()
    {
        List<BoletoEncontrado> resultado = BoletoParserService.ExtraiBoletosDoConteudo(
            ConteudoEmailReal,
            UrlBoleto
        );

        DateTime esperado = new(2026, 8, 5);

        Assert.All(resultado, boleto => Assert.Equal(esperado, boleto.Vencimento));
    }

    [Fact]
    public void ExtraiBoletosDoConteudo_SemTextoDeVencimento_DeveRetornarListaVazia()
    {
        string conteudoSemVencimento = "Este texto não menciona nenhuma data de vencimento.";

        List<BoletoEncontrado> resultado = BoletoParserService.ExtraiBoletosDoConteudo(
            conteudoSemVencimento,
            UrlBoleto
        );

        Assert.Empty(resultado);
    }

    [Fact]
    public void ExtraiVencimento_ComTextoValido_DeveRetornarDataCorreta()
    {
        string conteudo = "Segue o boleto, vencimento: 05/08/2026, favor pagar.";

        DateTime? resultado = BoletoParserService.ExtraiVencimento(conteudo);

        Assert.Equal(new DateTime(2026, 8, 5), resultado);
    }

    [Fact]
    public void ExtraiVencimento_SemDataNoTexto_DeveRetornarNull()
    {
        string conteudo = "Texto qualquer, sem nenhuma data.";

        DateTime? resultado = BoletoParserService.ExtraiVencimento(conteudo);

        Assert.Null(resultado);
    }

    [Fact]
    public void ExtraiUrlsDeBoletos_ComLinkDeBoletoEDescadastroMisturados_DeveRetornarSoOBoleto()
    {
        string conteudo =
            "Boleto: "
            + UrlBoleto
            + "/PopUp/pCli_BoletoNovo.aspx?i=ABC==&amp;cont=DEF==&amp;log=GHI=&amp;baixa=1 "
            + "Descadastro: "
            + UrlBoleto
            + "/RemoverEmail.aspx?r=XYZ==";

        List<string> resultado = BoletoParserService.ExtraiUrlsDeBoletos(conteudo, UrlBoleto);

        Assert.Single(resultado);
        Assert.Contains("pCli_BoletoNovo", resultado[0]);
    }

    [Fact]
    public void ExtraiUrlsDeBoletos_ComUrlDuplicadaHrefETexto_DeveDeduplicar()
    {
        string url =
            UrlBoleto + "/PopUp/pCli_BoletoNovo.aspx?i=ABC==&amp;cont=DEF==&amp;log=GHI=&amp;baixa=1";
        string conteudo = $"<a href=\"{url}\">{url}</a>";

        List<string> resultado = BoletoParserService.ExtraiUrlsDeBoletos(conteudo, UrlBoleto);

        Assert.Single(resultado);
    }
}
