using BoletoProtest.Core.Models;

namespace BoletoProtest.Infrastructure.Services;

public class GmailDraftService
{
    public static async Task<string> MontaMensagem(AppConfig appConf)
    {
        List<Boleto> boletos = await GmailMessageService.BaixaPDF(appConf);

        string mensagem = $"";

        return mensagem;
    }
}
