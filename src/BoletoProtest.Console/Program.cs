using BoletoProtest.Console.Services;
using BoletoProtest.Core.Models;
using BoletoProtest.Infrastructure.Services;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

public class Program
{
    public static async Task Main(string[] args)
    {
        AppConfig appConf = ConfigurationService.AppConfiguration();

        GmailService gmailService = await GmailAuthService.BuscaGmailService();

        GmailMessageService gmailMessageService = new(gmailService);
        List<Boleto> boletos = await gmailMessageService.BaixaPdf(appConf);

        Console.WriteLine($"Qtd _boletos: {boletos.Count}");
        if (boletos.Count == 0)
        {
            // Não é um erro: significa apenas que não há boleto novo neste ciclo
            // (situação legítima do domínio, não uma falha do sistema). Por isso
            // termina normalmente, sem lançar exceção.
            Console.WriteLine("Nenhum boleto encontrado para o período atual.");
            return;
        }

        GmailDraftService gmailDraftService = new(boletos, appConf, gmailService);
        Draft rascunho = await gmailDraftService.CriaRascunho(appConf);

        Console.WriteLine($"Rascunho criado com sucesso! Id: {rascunho.Id}");

        Console.WriteLine("Abrindo rascunho no GoogleChrome...");
        gmailDraftService.AbreRascunhoDireto(rascunho);

        Console.WriteLine(
            "\nFINALIZADO! Pressione qualquer tecla para sair (fecha sozinho em 5 min)..."
        );

        Task tarefaTecla = Task.Run(() => Console.ReadKey(intercept: true));
        Task tarefaTimeout = Task.Delay(TimeSpan.FromMinutes(5));

        await Task.WhenAny(tarefaTecla, tarefaTimeout);
    }
}
