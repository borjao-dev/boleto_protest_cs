using BoletoProtest.Infrastructure.Utils;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace BoletoProtest.Infrastructure.Services;

public static class PdfService
{
    public static string FormataNumeroApartamento(byte[] bytesPdf, string senha)
    {
        Console.WriteLine("Formatando número do apartamento...");

        string apto = ExtraiNumeroApartamento(bytesPdf, senha); // A - AP 1509

        if (!apto.Contains(" - "))
            throw new FormatException("A string `apto` não contém ` - `!");

        if (!apto[^4..].All(char.IsDigit))
            throw new FormatException("A string `apto` não termina com 4 dígitos!");

        string aptoFormatado = apto.Replace(" ", "").Replace("AP", "").Replace("-", ""); // A1509
        aptoFormatado += "-"; // A1509-

        return aptoFormatado[1..] + aptoFormatado[0]; // 1509-A;
    }

    private static string ExtraiNumeroApartamento(byte[] bytesPdf, string senha)
    {
        Console.WriteLine("Extraindo número do apartamento dentro do PDF...");

        ArgumentNullException.ThrowIfNull(bytesPdf);
        if (bytesPdf.Length == 0)
            throw new ArgumentException("PDF byte vazio.", nameof(bytesPdf));

        try
        {
            using PdfDocument document = PdfDocument.Open(
                bytesPdf,
                new ParsingOptions { Password = senha }
            );
            Page pagina = document.GetPage(1);
            string conteudo = ContentOrderTextExtractor.GetText(pagina);

            const string inicio = "UNIDADE: BL";
            const string fim = $"\n";

            int indiceInicio = conteudo.IndexOf(inicio);
            if (indiceInicio == -1)
                throw new FormatException($"Marcador `inicio` \"{inicio}\" não encontrado no PDF.");

            indiceInicio += inicio.Length;

            int indiceFim = conteudo.IndexOf(fim, indiceInicio);
            if (indiceFim == -1)
                throw new FormatException($"Marcador `fim` \"{fim}\" não encontrado no PDF.");

            return conteudo[indiceInicio..indiceFim].Trim();
        }
        catch (PdfDocumentFormatException ex)
        {
            Util.MensagemEx(ex, "Bytes do PDF corrompidos ou formato inválido.");
            throw;
        }
        catch (PdfDocumentEncryptedException ex)
        {
            Util.MensagemEx(ex, "Senha inválida para abrir o PDF.");
            throw;
        }
        catch (FormatException ex)
        {
            Util.MensagemEx(ex, "Número do apartamento não encontrado no texto do PDF.");
            throw;
        }
    }
}
