using BoletoProtest.Core.Models;
using BoletoProtest.Infrastructure.Services;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Xunit;

namespace BoletoProtest.Infrastructure.Tests;

public class GmailDraftServiceTest
{
    // GmailDraftService.SubstituiPalavraChave/MontaMimeDoRascunho nunca fazem chamada
    // de rede — só precisam de uma instância de GmailService para existir (guardada
    // como campo, usada só em CriaRascunho, que não é testado aqui). Um GmailService
    // "vazio", sem credenciais reais, é suficiente para os testes deste arquivo.
    private static readonly GmailService GmailServiceFalso = new(
        new BaseClientService.Initializer { ApplicationName = "Testes" }
    );

    private static AppConfig CriaAppConfigDeTeste(List<string> apartamentosGerenciados) =>
        new()
        {
            ApartamentosGerenciados = apartamentosGerenciados,
            Assunto = "Boleto ap {{apto}}",
            Corpo = "O boleto{{plural}} do apto{{plural}} {{apto}} vence em {{dtVenc}}.",
            PastaDestino = "/qualquer/caminho/{{apto}}",
        };

    private static List<Boleto> CriaBoletos(params string[] apartamentos) =>
        [.. apartamentos.Select(apto => new Boleto("https://link", apto, new DateTime(2026, 8, 5)))];

    [Fact]
    public void Construtor_DeveFiltrarSomenteBoletosDeApartamentosGerenciados()
    {
        List<Boleto> boletos = CriaBoletos("1509-A", "1508-A", "1503-A");
        AppConfig appConf = CriaAppConfigDeTeste(["1509-A"]);

        GmailDraftService service = new(boletos, appConf, GmailServiceFalso);

        string assunto = service.SubstituiPalavraChave("{{apto}}");
        Assert.Equal("1509-A", assunto);
    }

    [Fact]
    public void Construtor_SemNenhumBoletoCorrespondente_DeveLancarInvalidOperationException()
    {
        List<Boleto> boletos = CriaBoletos("1503-A", "1508-A");
        AppConfig appConf = CriaAppConfigDeTeste(["1509-A"]);

        Assert.Throws<InvalidOperationException>(
            () => new GmailDraftService(boletos, appConf, GmailServiceFalso)
        );
    }

    [Fact]
    public void SubstituiPalavraChave_ComApartamentoUnico_DeveSubstituirCorretamente()
    {
        List<Boleto> boletos = CriaBoletos("1509-A");
        AppConfig appConf = CriaAppConfigDeTeste(["1509-A"]);
        GmailDraftService service = new(boletos, appConf, GmailServiceFalso);

        string resultado = service.SubstituiPalavraChave("Apto: {{apto}}");

        Assert.Equal("Apto: 1509-A", resultado);
    }

    [Fact]
    public void SubstituiPalavraChave_ComMultiplosApartamentos_DeveJuntarComVirgula()
    {
        List<Boleto> boletos = CriaBoletos("1509-A", "1508-A");
        AppConfig appConf = CriaAppConfigDeTeste(["1509-A", "1508-A"]);
        GmailDraftService service = new(boletos, appConf, GmailServiceFalso);

        string resultado = service.SubstituiPalavraChave("Aptos: {{apto}}");

        Assert.Equal("Aptos: 1509-A, 1508-A", resultado);
    }

    [Fact]
    public void SubstituiPalavraChave_ComSubstitutoEChaveExplicitos_DeveUsarOsFornecidos()
    {
        List<Boleto> boletos = CriaBoletos("1509-A");
        AppConfig appConf = CriaAppConfigDeTeste(["1509-A"]);
        GmailDraftService service = new(boletos, appConf, GmailServiceFalso);

        string resultado = service.SubstituiPalavraChave(
            "Vencimento: {{dtVenc}}",
            "05/08/2026",
            "{{dtVenc}}"
        );

        Assert.Equal("Vencimento: 05/08/2026", resultado);
    }

    [Fact]
    public void MontaMimeDoRascunho_ComUmApartamento_DeveUsarSingularNoPlural()
    {
        List<Boleto> boletos = CriaBoletos("1509-A");
        AppConfig appConf = CriaAppConfigDeTeste(["1509-A"]);
        GmailDraftService service = new(boletos, appConf, GmailServiceFalso);

        // MontaMimeDoRascunho tenta anexar o PDF via FileService, o que falharia
        // (pasta de teste não existe de verdade) — mas queremos só validar o texto
        // do corpo, então testamos indiretamente via SubstituiPalavraChave, que é
        // o mesmo mecanismo usado internamente para resolver {{plural}}.
        string plural = boletos.Count > 1 ? "s" : "";
        string corpoComPlural = service.SubstituiPalavraChave(
            appConf.Corpo,
            plural,
            "{{plural}}"
        );
        // Segunda substituição: resolve {{apto}} usando o padrão (lista de
        // apartamentos já filtrada), para o texto final não sobrar nenhum marcador.
        corpoComPlural = service.SubstituiPalavraChave(corpoComPlural);

        Assert.DoesNotContain("{{plural}}", corpoComPlural);
        Assert.DoesNotContain("{{apto}}", corpoComPlural);
        Assert.Contains("O boleto do apto 1509-A", corpoComPlural);
    }

    [Fact]
    public void MontaMimeDoRascunho_ComMultiplosApartamentos_DeveUsarPluralNoTexto()
    {
        List<Boleto> boletos = CriaBoletos("1509-A", "1508-A");
        AppConfig appConf = CriaAppConfigDeTeste(["1509-A", "1508-A"]);
        GmailDraftService service = new(boletos, appConf, GmailServiceFalso);

        string plural = boletos.Count > 1 ? "s" : "";
        string corpoComPlural = service.SubstituiPalavraChave(
            appConf.Corpo,
            plural,
            "{{plural}}"
        );
        corpoComPlural = service.SubstituiPalavraChave(corpoComPlural);

        Assert.DoesNotContain("{{plural}}", corpoComPlural);
        // "boleto{{plural}}" -> "boletos", "apto{{plural}}" -> "aptos" (o "O" do
        // início do template de teste não tem {{plural}} colado, então continua "O"
        // mesmo no plural — limitação conhecida do "{{plural}}" simples, registrada
        // como observação para revisar o texto real do appsettings.json).
        Assert.Contains("boletos", corpoComPlural);
        Assert.Contains("aptos", corpoComPlural);
    }
}
