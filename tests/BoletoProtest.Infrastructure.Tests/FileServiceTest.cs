using BoletoProtest.Core.Models;
using BoletoProtest.Infrastructure.Services;
using Xunit;

namespace BoletoProtest.Infrastructure.Tests;

public class FileServiceTest : IDisposable
{
    private readonly string _pastaTemporaria;

    public FileServiceTest()
    {
        // Pasta temporária única por execução de teste, evitando que testes
        // interfiram uns nos outros ou deixem lixo no disco real do dev.
        _pastaTemporaria = Path.Combine(Path.GetTempPath(), "BoletoProtestTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_pastaTemporaria);
    }

    public void Dispose()
    {
        if (Directory.Exists(_pastaTemporaria))
        {
            Directory.Delete(_pastaTemporaria, recursive: true);
        }
    }

    [Fact]
    public void CaminhoCompletoDoArquivo_DeveMontarNomeComVencimentoFormatado()
    {
        FileService fileService = new(_pastaTemporaria);
        Boleto boleto = new("https://link.qualquer", "1509-A", new DateTime(2026, 8, 5));

        string caminho = fileService.CaminhoCompletoDoArquivo(boleto);

        Assert.Equal(
            Path.Combine(_pastaTemporaria, "Vencimento_05-08-2026.pdf"),
            caminho
        );
    }

    [Fact]
    public async Task SalvaArquivoPdf_DeveGravarBytesExatosNoArquivo()
    {
        FileService fileService = new(_pastaTemporaria);
        Boleto boleto = new("https://link.qualquer", "1509-A", new DateTime(2026, 8, 5));
        byte[] bytesOriginais = [1, 2, 3, 4, 5];

        await fileService.SalvaArquivoPdf(bytesOriginais, boleto);

        string caminho = fileService.CaminhoCompletoDoArquivo(boleto);
        byte[] bytesLidos = await File.ReadAllBytesAsync(caminho);

        Assert.Equal(bytesOriginais, bytesLidos);
    }

    [Fact]
    public async Task SalvaArquivoPdf_ChamadoDuasVezesComMesmoBoleto_DeveSobrescrever()
    {
        FileService fileService = new(_pastaTemporaria);
        Boleto boleto = new("https://link.qualquer", "1509-A", new DateTime(2026, 8, 5));

        await fileService.SalvaArquivoPdf([1, 2, 3], boleto);
        await fileService.SalvaArquivoPdf([9, 9, 9, 9], boleto);

        string caminho = fileService.CaminhoCompletoDoArquivo(boleto);
        byte[] bytesLidos = await File.ReadAllBytesAsync(caminho);

        Assert.Equal(new byte[] { 9, 9, 9, 9 }, bytesLidos);
    }

    [Fact]
    public async Task SalvaArquivoPdf_ComPastaDestinoInexistente_DeveLancarDirectoryNotFoundException()
    {
        string pastaInexistente = Path.Combine(_pastaTemporaria, "pasta-que-nao-existe");
        FileService fileService = new(pastaInexistente);
        Boleto boleto = new("https://link.qualquer", "1509-A", new DateTime(2026, 8, 5));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => fileService.SalvaArquivoPdf([1, 2, 3], boleto)
        );
    }
}
