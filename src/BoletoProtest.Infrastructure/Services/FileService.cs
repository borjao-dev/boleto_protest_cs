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

    // Checa se já existe um PDF salvo para este boleto específico (mesmo apartamento
    // + mesmo vencimento), antes de baixar/sobrescrever. Usado para evitar reprocessar
    // um boleto que já foi baixado numa execução anterior do programa.
    public bool JaExiste(Boleto boleto)
    {
        string caminhoCompleto = CaminhoCompletoDoArquivo(boleto);

        return File.Exists(caminhoCompleto);
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
