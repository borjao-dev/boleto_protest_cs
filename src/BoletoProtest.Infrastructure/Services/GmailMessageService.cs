using System.Text;
using BoletoProtest.Core.Models;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace BoletoProtest.Infrastructure.Services;

public class GmailMessageService(GmailService gmailService)
{
    private readonly GmailService _gmailService = gmailService;

    private async Task<List<Message>> BuscaMensagensGmail(AppConfig appConf)
    {
        string busca = $"subject:\"{appConf.AssuntoBusca}\" from:{appConf.Remetente}";

        UsersResource.MessagesResource.ListRequest listagem = _gmailService.Users.Messages.List(
            "me"
        );
        listagem.Q = busca;

        ListMessagesResponse idsEncontrados = await listagem.ExecuteAsync();

        List<Message> mensagens = [];

        if (idsEncontrados.Messages is null)
        {
            return mensagens;
        }

        foreach (Message referencia in idsEncontrados.Messages)
        {
            Message mensagemCompleta = await _gmailService
                .Users.Messages.Get("me", referencia.Id)
                .ExecuteAsync();

            mensagens.Add(mensagemCompleta);
        }

        return mensagens;
    }

    private async Task<List<BoletoEncontrado>> FiltraEmailsPorData(AppConfig appConf)
    {
        List<Message> mensagensGmail = await BuscaMensagensGmail(appConf);

        List<BoletoEncontrado> boletosEncontrados = [];

        foreach (Message mensagem in mensagensGmail)
        {
            string corpo = AuxMultipart(mensagem.Payload);

            if (string.IsNullOrEmpty(corpo))
                continue;

            byte[] bytes = FromBase64UrlString(corpo);
            string conteudo = Encoding.UTF8.GetString(bytes);

            boletosEncontrados.AddRange(
                BoletoParserService.ExtraiBoletosDoConteudo(conteudo, appConf.UrlBoleto)
            );
        }

        return boletosEncontrados;
    }

    public async Task<List<Boleto>> BaixaPdf(AppConfig appConf)
    {
        List<BoletoEncontrado> boletosEncontrados = await FiltraEmailsPorData(appConf);

        using HttpClient clienteGet = new();

        List<Boleto> boletos = [];

        foreach (BoletoEncontrado encontrado in boletosEncontrados)
        {
            HttpResponseMessage resposta = await clienteGet.GetAsync(encontrado.Link);
            resposta.EnsureSuccessStatusCode();

            byte[] bytes = await resposta.Content.ReadAsByteArrayAsync();

            string numeroApartamento = PdfService.BuscaNumeroApartamentoFormatado(
                bytes,
                appConf.CpfPrefixo
            );

            // Só agora, com o apartamento já extraído do PDF, dá pra saber a pasta
            // final certa (cada apartamento tem sua própria pasta no PC da Dalgiza).
            string pastaDoApartamento = appConf.PastaDestino.Replace("{{apto}}", numeroApartamento);
            FileService fileService = new(pastaDoApartamento);

            Boleto boleto = new(encontrado.Link, numeroApartamento, encontrado.Vencimento);

            await fileService.SalvaArquivoPdf(bytes, boleto);

            boletos.Add(boleto);
        }

        return boletos;
    }

    // MessagePart, na lib, ja e a estrutura tipada equivalente ao antigo GmailMessagesParts.
    private static string AuxMultipart(MessagePart parte)
    {
        if (!string.IsNullOrEmpty(parte.Body?.Data))
        {
            if (parte.MimeType == "text/html")
            {
                return parte.Body.Data;
            }
        }

        if (parte.Parts is not null)
        {
            foreach (MessagePart subParte in parte.Parts)
            {
                string resultado = AuxMultipart(subParte);
                if (resultado != "")
                {
                    return resultado;
                }
            }
        }

        return "";
    }

    private static byte[] FromBase64UrlString(string base64Url)
    {
        string base64Padrao = base64Url.Replace('-', '+').Replace('_', '/');

        switch (base64Padrao.Length % 4)
        {
            case 2:
                base64Padrao += "==";
                break;
            case 3:
                base64Padrao += "=";
                break;
        }

        return Convert.FromBase64String(base64Padrao);
    }
}
