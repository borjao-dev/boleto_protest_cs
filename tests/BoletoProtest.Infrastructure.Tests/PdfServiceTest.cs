using BoletoProtest.Infrastructure.Services;
using Xunit;

namespace BoletoProtest.Infrastructure.Tests;

public static class PdfServiceTest
{
    [Fact]
    public static void FormataNumeroApartamento_DeveRetornarNumeroDoApartamentoComFormatacaoCorreta()
    {
        string caminhoPdf = Path.Combine(
            Environment.GetFolderPath("docs"),
            "Recibo_0000886797_20260729000000_62e567ca-ab5a-4ba9-8143-8e84c38e0450.pdf"
        );
        byte[] pdfBytes = File.ReadAllBytes(caminhoPdf);

        string aptoFormatado = FormataNumeroApartamento(pdfBytes);
        Assert.Equal("1509-A", aptoFormatado);
    }
}
