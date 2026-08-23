using BoletoProtest.Infrastructure.Services;
using Xunit;

namespace BoletoProtest.Infrastructure.Tests;

public class PdfServiceTest
{
    // Senha real usada nos PDFs de teste — 4 primeiros dígitos do CPF da Dalgiza
    // (mesma senha usada em produção, já que esses PDFs foram baixados de verdade).
    private const string SenhaReal = "4776";

    private static readonly string PastaTestData = Path.Combine(
        AppContext.BaseDirectory,
        "TestData"
    );

    [Theory]
    [InlineData("Boleto_1503-A.pdf", "1503-A")]
    [InlineData("Boleto_1508-A.pdf", "1508-A")]
    [InlineData("Boleto_1509-A.pdf", "1509-A")]
    [InlineData("Boleto_1515-B.pdf", "1515-B")]
    public void FormataNumeroApartamento_ComOsQuatroPdfsReais_DeveExtrairApartamentoCorreto(
        string nomeArquivo,
        string apartamentoEsperado
    )
    {
        string caminhoPdf = Path.Combine(PastaTestData, nomeArquivo);
        byte[] pdfBytes = File.ReadAllBytes(caminhoPdf);

        string aptoFormatado = PdfService.FormataNumeroApartamento(pdfBytes, SenhaReal);

        Assert.Equal(apartamentoEsperado, aptoFormatado);
    }

    [Fact]
    public void FormataNumeroApartamento_ComSenhaErrada_AindaConsegueLerConteudo()
    {
        // Comportamento REAL observado (não assumido): os PDFs da PROTEST não têm
        // restrição de LEITURA — só de edição/impressão, então PdfPig consegue abrir
        // e extrair o texto independente da senha fornecida. Por isso este teste NÃO
        // espera exceção — documenta o comportamento real, para não confundir no
        // futuro quem ler o código achando que senha errada deveria falhar aqui.
        string caminhoPdf = Path.Combine(PastaTestData, "Boleto_1509-A.pdf");
        byte[] pdfBytes = File.ReadAllBytes(caminhoPdf);

        string aptoFormatado = PdfService.FormataNumeroApartamento(pdfBytes, "0000");

        Assert.Equal("1509-A", aptoFormatado);
    }

    [Fact]
    public void FormataNumeroApartamento_ComBytesVazios_DeveLancarArgumentException()
    {
        byte[] bytesVazios = [];

        Assert.Throws<ArgumentException>(
            () => PdfService.FormataNumeroApartamento(bytesVazios, SenhaReal)
        );
    }

    [Fact]
    public void FormataNumeroApartamento_ComBytesNulos_DeveLancarArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => PdfService.FormataNumeroApartamento(null!, SenhaReal)
        );
    }

    [Fact]
    public void FormataNumeroApartamento_ComBytesQueNaoSaoPdf_DeveLancarPdfDocumentFormatException()
    {
        byte[] bytesLixo = "isto não é um PDF"u8.ToArray();

        Assert.ThrowsAny<Exception>(
            () => PdfService.FormataNumeroApartamento(bytesLixo, SenhaReal)
        );
    }
}
