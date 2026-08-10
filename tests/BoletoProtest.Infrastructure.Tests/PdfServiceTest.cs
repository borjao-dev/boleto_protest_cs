using BoletoProtest.Infrastructure.Services;
using Xunit;

namespace BoletoProtest.Infrastructure.Tests;

public class PdfServiceTest
{
    private static readonly string PastaTestData = Path.Combine(
        AppContext.BaseDirectory,
        "TestData"
    );

    [Theory]
    [InlineData("Boleto_1503-A.pdf", "1503-A")]
    [InlineData("Boleto_1508-A.pdf", "1508-A")]
    [InlineData("Boleto_1509-A.pdf", "1509-A")]
    [InlineData("Boleto_1515-B.pdf", "1515-B")]
    public void BuscaNumeroApartamentoFormatado_ComOsQuatroPdfsReais_DeveExtrairApartamentoCorreto(
        string nomeArquivo,
        string apartamentoEsperado
    )
    {
        string caminhoPdf = Path.Combine(PastaTestData, nomeArquivo);
        byte[] pdfBytes = File.ReadAllBytes(caminhoPdf);

        string aptoFormatado = PdfService.BuscaNumeroApartamentoFormatado(pdfBytes, "0573");

        Assert.Equal(apartamentoEsperado, aptoFormatado);
    }

    [Fact]
    public void BuscaNumeroApartamentoFormatado_ComPdfReal_DeveRetornarApartamentoNoFormatoCorreto()
    {
        string caminhoPdf = Path.Combine(PastaTestData, "Boleto_1509-A.pdf");
        byte[] pdfBytes = File.ReadAllBytes(caminhoPdf);

        // Senha real usada nos PDFs de teste (4 primeiros dígitos do CPF de dev).
        string aptoFormatado = PdfService.BuscaNumeroApartamentoFormatado(pdfBytes, "0573");

        Assert.Equal("1509-A", aptoFormatado);
    }

    [Fact]
    public void BuscaNumeroApartamentoFormatado_ComSenhaErrada_DeveLancarExcecao()
    {
        string caminhoPdf = Path.Combine(PastaTestData, "Boleto_1509-A.pdf");
        byte[] pdfBytes = File.ReadAllBytes(caminhoPdf);

        Assert.ThrowsAny<Exception>(
            () => PdfService.BuscaNumeroApartamentoFormatado(pdfBytes, "0000")
        );
    }

    [Fact]
    public void BuscaNumeroApartamentoFormatado_ComBytesVazios_DeveLancarArgumentException()
    {
        byte[] bytesVazios = [];

        Assert.Throws<ArgumentException>(
            () => PdfService.BuscaNumeroApartamentoFormatado(bytesVazios, "0573")
        );
    }

    [Fact]
    public void BuscaNumeroApartamentoFormatado_ComBytesNulos_DeveLancarArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => PdfService.BuscaNumeroApartamentoFormatado(null!, "0573")
        );
    }

    [Fact]
    public void BuscaNumeroApartamentoFormatado_ComBytesQueNaoSaoPdf_DeveLancarPdfDocumentFormatException()
    {
        byte[] bytesLixo = "isto não é um PDF"u8.ToArray();

        Assert.ThrowsAny<Exception>(
            () => PdfService.BuscaNumeroApartamentoFormatado(bytesLixo, "0573")
        );
    }
}
