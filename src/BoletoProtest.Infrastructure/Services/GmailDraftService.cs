using BoletoProtest.Core.Models;
using MimeKit;

namespace BoletoProtest.Infrastructure.Services;

public class GmailDraftService(List<Boleto> boletosBaixados)
{
    private readonly List<Boleto> _boletos = boletosBaixados;
    private readonly List<string> _apartamentos = boletosBaixados
        .Select(boleto => boleto.Apartamento)
        .ToList();

    private string SubstituiPalavraChave(string template)
    {
        string aptos = string.Join(", ", _apartamentos);

        return template.Replace("[[apto]]", aptos);
    }

    public MimeMessage MontaMimeDoRascunho(AppConfig appConf)
    {
        FileService fs = new(appConf.PastaDestino);
        var mensagem = new MimeMessage();

        mensagem.From.Add(new MailboxAddress(appConf.Nome, appConf.Remetente));
        mensagem.To.Add(new MailboxAddress(appConf.NomeDestinatario, appConf.Destinatario));
        mensagem.Subject = appConf.Assunto;

        var builder = new BodyBuilder { TextBody = SubstituiPalavraChave(appConf.Corpo) };

        List<string> pdfFiles = [];

        foreach (Boleto boleto in _boletos)
        {
            pdfFiles.Add(fs.CaminhoCompletoDoArquivo(boleto));
        }

        foreach (var file in pdfFiles)
        {
            builder.Attachments.Add(file);
        }

        mensagem.Body = builder.ToMessageBody();

        return mensagem;
    }
}
