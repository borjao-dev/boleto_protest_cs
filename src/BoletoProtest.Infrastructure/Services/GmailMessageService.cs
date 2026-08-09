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

            Match matchData = Regex.Match(conteudo, @"vencimento:\s*(\d{2}/\d{2}/\d{4})");

            if (!matchData.Success)
                continue;

            DateTime vencimento = DateTime.ParseExact(
                matchData.Groups[1].Value,
                "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture
            );

            // Um mesmo email pode conter vários boletos empilhados (a PROTEST manda
            // um aviso por apartamento, mas a Dalgiza às vezes encaminha vários juntos
            // numa "Forwarded Conversation"). Regex.Matches (plural) captura TODAS as
            // URLs do corpo, não só a primeira — cada uma vira um BoletoEncontrado,
            // todos compartilhando a mesma data de vencimento (idêntica no mesmo email).
            //
            // Descoberta via inspeção do conteúdo real decodificado: o HTML usa "&amp;"
            // como separador de parâmetros (entidade HTML de "&"), não "&" puro — usar
            // "&" no Regex cortava a URL no meio, gerando um link truncado/inválido que
            // o servidor da PROTEST devolvia como página de erro, não PDF. Além disso,
            // cada URL aparece 2x na mesma linha (dentro do href="" e como texto visível
            // do link) — Distinct() remove as duplicatas antes de baixar.
            //
            // Segunda descoberta: appConf.UrlBoleto sozinho ("/Operacional") também bate
            // com o link de descadastro do rodapé de cada bloco ("/Operacional/
            // RemoverEmail.aspx"), que devolve uma página HTML, não um PDF — daí o erro
            // "Could not find the version header comment". Exigir "/PopUp/pCli_BoletoNovo"
            // como parte obrigatória do padrão resolve, pois só a URL de boleto de
            // verdade tem esse caminho específico.
            MatchCollection matchesUrl = Regex.Matches(
                conteudo,
                Regex.Escape(appConf.UrlBoleto) + @"/PopUp/pCli_BoletoNovo\.aspx[^\s""<>]*"
            );

            List<string> urlsUnicas = matchesUrl
                .Select(match => match.Value.Replace("&amp;", "&"))
                .Distinct()
                .ToList();

            foreach (string url in urlsUnicas)
            {
                boletosEncontrados.Add(new BoletoEncontrado(url, vencimento));
            }
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
            string pastaDoApartamento = appConf.PastaDestino.Replace("[[apto]]", numeroApartamento);
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
