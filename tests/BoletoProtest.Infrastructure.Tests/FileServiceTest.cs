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
    public void CaminhoCompletoDoArquivo_DeveMontarNomeComApartamentoEVencimentoFormatado()
    {
        FileService fileService = new(_pastaTemporaria);
        Boleto boleto = new("https://link.qualquer", "1509-A", new DateTime(2026, 8, 5));

        string caminho = fileService.CaminhoCompletoDoArquivo(boleto);

        Assert.Equal(
            Path.Combine(_pastaTemporaria, "1509-A_vencimento_05-08-2026.pdf"),
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
        byte[] bytesLidos = await File.ReadAllBytesAsync(
            caminho,
            TestContext.Current.CancellationToken
        );

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
        byte[] bytesLidos = await File.ReadAllBytesAsync(
            caminho,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(new byte[] { 9, 9, 9, 9 }, bytesLidos);
    }

    [Fact]
    public async Task SalvaArquivoPdf_ComPastaDestinoInexistente_DeveLancarDirectoryNotFoundException()
    {
        string pastaInexistente = Path.Combine(_pastaTemporaria, "pasta-que-nao-existe");
        FileService fileService = new(pastaInexistente);
        Boleto boleto = new("https://link.qualquer", "1509-A", new DateTime(2026, 8, 5));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            fileService.SalvaArquivoPdf([1, 2, 3], boleto)
        );
    }

    [Fact]
    public void JaExiste_ComArquivoNaoSalvo_DeveRetornarFalse()
    {
        FileService fileService = new(_pastaTemporaria);
        Boleto boleto = new("https://link.qualquer", "1509-A", new DateTime(2026, 8, 5));

        Assert.False(fileService.JaExiste(boleto));
    }

    [Fact]
    public async Task JaExiste_ComArquivoJaSalvo_DeveRetornarTrue()
    {
        FileService fileService = new(_pastaTemporaria);
        Boleto boleto = new("https://link.qualquer", "1509-A", new DateTime(2026, 8, 5));

        await fileService.SalvaArquivoPdf([1, 2, 3], boleto);

        Assert.True(fileService.JaExiste(boleto));
    }

    [Fact]
    public async Task JaExiste_ComVencimentoDiferente_DeveRetornarFalse()
    {
        FileService fileService = new(_pastaTemporaria);
        Boleto boletoSalvo = new("https://link.qualquer", "1509-A", new DateTime(2026, 8, 5));
        Boleto boletoNovo = new("https://link.qualquer", "1509-A", new DateTime(2026, 9, 5));

        await fileService.SalvaArquivoPdf([1, 2, 3], boletoSalvo);

        Assert.False(fileService.JaExiste(boletoNovo));
    }

    [Fact]
    public async Task JaExiste_ComApartamentoDiferente_DeveRetornarFalse()
    {
        FileService fileService = new(_pastaTemporaria);
        Boleto boletoSalvo = new("https://link.qualquer", "1509-A", new DateTime(2026, 8, 5));
        Boleto boletoOutroApto = new("https://link.qualquer", "1508-A", new DateTime(2026, 8, 5));

        await fileService.SalvaArquivoPdf([1, 2, 3], boletoSalvo);

        Assert.False(fileService.JaExiste(boletoOutroApto));
    }
}
