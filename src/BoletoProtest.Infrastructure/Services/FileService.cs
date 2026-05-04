using BoletoProtest.Core.Models;

namespace BoletoProtest.Infrastructure.Services;

public class FileService
{
    private string CaminhoCompletoDoArquivo(Boleto boleto, AppConfig appConf)
    {
        string nomeArquivo = $"Vencimento_{boleto.Vencimento:dd-MM-yyyy}.pdf";

        return Path.Combine(appConf.PastaDestino, nomeArquivo);
    }

    public async Task SalvaArquivoPDF(byte[] conteudoPDF, Boleto boleto, AppConfig appConf)
    {
        string caminhoCompleto = CaminhoCompletoDoArquivo(boleto, appConf);
        string? diretorio = Path.GetDirectoryName(caminhoCompleto);

        if (diretorio is null)
        {
            Console.WriteLine($"Caminho de destino inválido nas configurações!");
            return;
        }

        if (!Directory.Exists(diretorio))
        {
            Console.WriteLine($"O caminho '{diretorio}' não existe!");
            return;
        }

        try
        {
            await File.WriteAllBytesAsync(caminhoCompleto, conteudoPDF);
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Erro de autorização para salvar o arquivo! => {ex.Message}");
        }
    }
}
