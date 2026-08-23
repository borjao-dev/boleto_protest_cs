using System.Text;
using BoletoProtest.Core.Models;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace BoletoProtest.Infrastructure.Services;

public class GmailMessageService(GmailService gmailService)
{
    private readonly GmailService _gmailService = gmailService;

    // A PROTEST manda UM email por apartamento (não um único email com vários
    // boletos empilhados, como um teste anterior com email encaminhado sugeria).
    // Os 4 emails chegam "aglomerados" (mesmo dia/horário aproximado). Por isso,
    // buscamos vários emails recentes, não só o último, e depois agrupamos pelos
    // que têm o MESMO vencimento do mais recente — critério robusto que não
    // depende de "quantos emails" chegam, só da data real que os relaciona.
    private async Task<List<Message>> BuscaMensagensRecentes(AppConfig appConf)
    {
        Console.WriteLine("Buscando mensagens no Gmail...");

        // newer_than: corte de segurança, evita processar anos de histórico
        // acumulado — não é o critério de escolha em si (isso é o vencimento).
        string busca =
            $"subject:\"{appConf.AssuntoBusca}\" from:\"{appConf.Remetente}\" newer_than:2m";

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

        Console.WriteLine($"{mensagens.Count} mensagens encontradas nos últimos 2 meses.");

        return mensagens;
    }

    private static string ExtraiConteudo(Message mensagem)
    {
        string corpo = AuxMultipart(mensagem.Payload);

        if (string.IsNullOrEmpty(corpo))
        {
            return "";
        }

        byte[] bytes = FromBase64UrlString(corpo);
        return Encoding.UTF8.GetString(bytes);
    }

    private async Task<List<BoletoEncontrado>> FiltraBoletosEncontrados(AppConfig appConf)
    {
        Console.WriteLine("Filtrando boletos encontrados...");

        List<Message> mensagens = await BuscaMensagensRecentes(appConf);

        if (mensagens.Count == 0)
        {
            return [];
        }

        // Descobre o vencimento do email mais recente (por InternalDate real,
        // não pela ordem que a API devolve).
        Message maisRecente = mensagens.MaxBy(mensagem => mensagem.InternalDate ?? 0)!;
        string conteudoMaisRecente = ExtraiConteudo(maisRecente);
        DateTime? vencimentoAlvo = BoletoParserService.ExtraiVencimento(conteudoMaisRecente);

        if (vencimentoAlvo is null)
        {
            return [];
        }

        Console.WriteLine($"Vencimento alvo (lote atual): {vencimentoAlvo:dd/MM/yyyy}");

        // Agrupa todos os emails com o MESMO vencimento do mais recente — é o
        // "lote" de boletos daquele mês, independente de quantos emails chegaram.
        List<BoletoEncontrado> boletosEncontrados = [];

        foreach (Message mensagem in mensagens)
        {
            string conteudo = ExtraiConteudo(mensagem);

            if (string.IsNullOrEmpty(conteudo))
            {
                continue;
            }

            DateTime? vencimentoDesteEmail = BoletoParserService.ExtraiVencimento(conteudo);

            if (vencimentoDesteEmail != vencimentoAlvo)
            {
                continue;
            }

            boletosEncontrados.AddRange(
                BoletoParserService.ExtraiBoletosDoConteudo(conteudo, appConf.UrlBoleto)
            );
        }

        return boletosEncontrados;
    }

    public async Task<List<Boleto>> BaixaPdf(AppConfig appConf)
    {
        Console.WriteLine("Baixando arquivos PDF...");

        List<BoletoEncontrado> boletosEncontrados = await FiltraBoletosEncontrados(appConf);

        using HttpClient clienteGet = new();

        List<Boleto> boletos = [];

        foreach (BoletoEncontrado encontrado in boletosEncontrados)
        {
            HttpResponseMessage resposta = await clienteGet.GetAsync(encontrado.Link);
            resposta.EnsureSuccessStatusCode();

            byte[] bytes = await resposta.Content.ReadAsByteArrayAsync();

            string numeroApartamento = PdfService.FormataNumeroApartamento(
                bytes,
                appConf.CpfPrefixo
            );

            // Só agora, com o apartamento já extraído do PDF, dá pra saber a pasta
            // final certa (cada apartamento tem sua própria pasta no PC da Dalgiza).
            string pastaDoApartamento = appConf.PastaDestino.Replace(
                "{{apto}}",
                numeroApartamento
            );
            FileService fileService = new(pastaDoApartamento);

            Boleto boleto = new(encontrado.Link, numeroApartamento, encontrado.Vencimento);

            if (fileService.JaExiste(boleto))
            {
                Console.WriteLine(
                    $"Boleto do apto {numeroApartamento}, vencimento {encontrado.Vencimento:dd/MM/yyyy}, já existe — ignorando."
                );
                continue;
            }

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
