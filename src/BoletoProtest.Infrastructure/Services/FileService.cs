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
        string? diretorio = Path.GetDirectoryName(caminhoCompleto);

        if (diretorio is null)
        {
            Console.Write("Caminho de destino inválido nas configurações!");
            return;
        }

        if (!Directory.Exists(diretorio))
        {
            Console.Write($"O caminho '{diretorio}' não existe!");
            return;
        }

        try
        {
            await File.WriteAllBytesAsync(caminhoCompleto, conteudoPDF);
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Write($"Erro de autorização para salvar o arquivo! => {ex.Message}");
        }
    }

    public static async Task<byte[]> LerArquivoPDF(Boleto boleto, AppConfig appConf)
    {
        string? caminhoCompleto = CaminhoCompletoDoArquivo(boleto, appConf);

        try
        {
            return await File.ReadAllBytesAsync(caminhoCompleto);
        }
        catch (FileNotFoundException ex)
        {
            Console.Write($"Caminho do arquivo inválido nas configurações! => {ex.Message}");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Write($"Erro de autorização para salvar o arquivo! => {ex.Message}");
            throw;
        }
    }
}
