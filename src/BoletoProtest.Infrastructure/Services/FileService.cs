using BoletoProtest.Core.Models;

namespace BoletoProtest.Infrastructure.Services;

public class FileService
{
    private static string CaminhoCompletoDoArquivo(Boleto boleto, AppConfig appConf)
    {
        string nomeArquivo = $"Vencimento_{boleto.Vencimento:dd-MM-yyyy}.pdf";

        return Path.Combine(appConf.PastaDestino, nomeArquivo);
    }

    public static async Task SalvaArquivoPDF(byte[] conteudoPDF, Boleto boleto, AppConfig appConf)
    {
        string caminhoCompleto = CaminhoCompletoDoArquivo(boleto, appConf);

        try
        {
            await File.WriteAllBytesAsync(caminhoCompleto, conteudoPDF);
        }
        catch (DirectoryNotFoundException ex)
        {
            Helpers.MensagemEx(ex, "Caminho de destino inválido nas configurações!");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            Helpers.MensagemEx(ex, "Erro de autorização para salvar o arquivo!");
            throw;
        }
    }

    public static async Task<byte[]> LerArquivoPDF(Boleto boleto, AppConfig appConf)
    {
        string caminhoCompleto = CaminhoCompletoDoArquivo(boleto, appConf);

        try
        {
            return await File.ReadAllBytesAsync(caminhoCompleto);
        }
        catch (FileNotFoundException ex)
        {
            Helpers.MensagemEx(ex, "Caminho do arquivo inválido nas configurações!");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            Helpers.MensagemEx(ex, "Erro de autorização para salvar o arquivo!");
            throw;
        }
    }
}
