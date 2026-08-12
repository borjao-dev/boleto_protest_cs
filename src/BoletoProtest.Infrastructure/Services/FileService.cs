using BoletoProtest.Core.Models;
using BoletoProtest.Infrastructure.Utils;

namespace BoletoProtest.Infrastructure.Services;

public class FileService(string pastaDestino)
{
    private readonly string _pastaDestino = pastaDestino;

    public string CaminhoCompletoDoArquivo(Boleto boleto)
    {
        Console.WriteLine("Buscando caminho completo do arquivo...");

        string nomeArquivo = $"{boleto.Apartamento}_vencimento_{boleto.Vencimento:dd-MM-yyyy}.pdf";

        return Path.Combine(_pastaDestino, nomeArquivo);
    }

    public async Task SalvaArquivoPdf(byte[] conteudoPdf, Boleto boleto)
    {
        string caminhoCompleto = CaminhoCompletoDoArquivo(boleto);

        try
        {
            Console.WriteLine("Salvando arquivo PDF...");

            await File.WriteAllBytesAsync(caminhoCompleto, conteudoPdf);
        }
        catch (DirectoryNotFoundException ex)
        {
            Util.MensagemEx(ex, "Caminho de destino inválido nas configurações!");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            Util.MensagemEx(ex, "Erro de autorização para salvar o arquivo!");
            throw;
        }
    }
}
