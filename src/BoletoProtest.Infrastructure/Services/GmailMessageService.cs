using System.Text;
using System.Text.RegularExpressions;
using BoletoProtest.Core.Models;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace BoletoProtest.Infrastructure.Services;

public class GmailMessageService(GmailService gmailService)
{
    private readonly GmailService _gmailService = gmailService;

    private async Task<List<Message>> BuscaMensagensGmail(AppConfig appConf)
    {
        string busca =
            $"subject:\"{appConf.AssuntoEmailBusca}\" from:{appConf.EmailRemetenteBusca}";

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

    private async Task<List<string>> FiltraEmailsPorData(AppConfig appConf)
    {
        DateTime mesQueVem = DateTime.Now.AddMonths(1);
        string vencimento = mesQueVem.ToString("MM/yyyy");

        List<Message> mensagensGmail = await BuscaMensagensGmail(appConf);

        List<string> urlsBoletos = [];

        foreach (Message mensagem in mensagensGmail)
        {
            string corpo = AuxMultipart(mensagem.Payload);

            if (string.IsNullOrEmpty(corpo))
            {
                continue;
            }

            // A lib do Google já trabalha nativamente com base64url, então o campo
            // Data que ela devolve já vem no formato `"-"/"_"`, dispensando o
            // `Replace('-', '+').Replace('_', '/')` manual.
            byte[] bytes = FromBase64UrlString(corpo);
            string conteudo = Encoding.UTF8.GetString(bytes);

            if (conteudo.Contains(vencimento))
            {
                Match match = Regex.Match(
                    conteudo,
                    Regex.Escape(appConf.UrlBoleto) + @"[^\s""<>]+"
                );
                if (match.Success)
                {
                    urlsBoletos.Add(match.Value);
                }
            }
        }

        return urlsBoletos;
    }

    public async Task<List<Boleto>> BaixaPdf(AppConfig appConf)
    {
        List<string> listaUrls = await FiltraEmailsPorData(appConf);

        using HttpClient clienteGet = new();
        FileService fileService = new(appConf.PastaDestino);

        List<Boleto> boletos = [];

        foreach (string url in listaUrls)
        {
            HttpResponseMessage resposta = await clienteGet.GetAsync(url);
            resposta.EnsureSuccessStatusCode();

            byte[] bytes = await resposta.Content.ReadAsByteArrayAsync();

            string numeroApartamento = PdfService.BuscaNumeroApartamentoFormatado(
                bytes,
                appConf.CpfPrefixo
            );

            Boleto boleto = new(url, numeroApartamento, DateTime.Now.AddMonths(1));

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
