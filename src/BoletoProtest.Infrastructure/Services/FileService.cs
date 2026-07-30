using System.Text;
using BoletoProtest.Core.Models;
using BoletoProtest.Infrastructure.Helpers;

namespace BoletoProtest.Infrastructure.Services;

public class FileService
{
    private static string CaminhoCompletoDoArquivo(Boleto boleto, AppConfig appConf)
    {
        string nomeArquivo = $"Vencimento_{boleto.Vencimento:dd-MM-yyyy}.pdf";

        return Path.Combine(appConf.PastaDestino, nomeArquivo);
    }

    public static async Task SalvaArquivoPdf(byte[] conteudoPdf, Boleto boleto, AppConfig appConf)
    {
        string caminhoCompleto = CaminhoCompletoDoArquivo(boleto, appConf);

        try
        {
            await File.WriteAllBytesAsync(caminhoCompleto, conteudoPdf);
        }
        catch (DirectoryNotFoundException ex)
        {
            Helper.MensagemEx(ex, "Caminho de destino inválido nas configurações!");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            Helper.MensagemEx(ex, "Erro de autorização para salvar o arquivo!");
            throw;
        }
    }

    public static async Task<string> ConverteBytesPdfParaString(byte[] bytesPdf)
    {
        return Encoding.UTF8.GetString(bytesPdf);
    }

    public static async Task<byte[]> LerBytesArquivoPdf(Boleto boleto, AppConfig appConf)
    {
        string caminhoCompleto = CaminhoCompletoDoArquivo(boleto, appConf);

        try
        {
            return await File.ReadAllBytesAsync(caminhoCompleto);
        }
        catch (FileNotFoundException ex)
        {
            Helper.MensagemEx(ex, "Caminho do arquivo inválido nas configurações!");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            Helper.MensagemEx(ex, "Erro de autorização para salvar o arquivo!");
            throw;
        }
    }
}
