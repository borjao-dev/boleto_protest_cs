using System.Buffers.Text;
using BoletoProtest.Core.Models;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using MimeKit;

namespace BoletoProtest.Infrastructure.Services;

public class GmailDraftService
{
    private readonly List<Boleto> _boletos;
    private readonly GmailService _gmailService;
    private readonly List<string> _apartamentos;

    public GmailDraftService(
        List<Boleto> boletosBaixados,
        AppConfig appConf,
        GmailService gmailService
    )
    {
        // Todos os boletos são baixados e salvos em disco (Dalgiza quer isso, mesmo
        // dos apartamentos que ela não repassa pra imobiliária). Mas só os apartamentos
        // listados em ApartamentosGerenciados devem ir anexados no rascunho — nunca
        // "chumbado" um apartamento fixo, sempre dirigido pela config.
        _boletos = boletosBaixados
            .Where(boleto => appConf.ApartamentosGerenciados.Contains(boleto.Apartamento))
            .ToList();

        if (_boletos.Count == 0)
        {
            throw new InvalidOperationException(
                "Nenhum dos boletos baixados corresponde a um apartamento em "
                    + "ApartamentosGerenciados. Rascunho não pode ser montado sem "
                    + "ao menos um boleto válido para anexar."
            );
        }

        _gmailService = gmailService;
        _apartamentos = [.. _boletos.Select(boleto => boleto.Apartamento)];
    }

    internal string SubstituiPalavraChave(
        string template,
        string? substituto = null,
        string chave = "{{apto}}"
    )
    {
        // is null (não IsNullOrEmpty!) — string vazia ("") é um valor de substituição
        // VÁLIDO e intencional (ex: {{plural}} no singular vira ""), diferente de
        // "nenhum valor foi passado" (null, aí sim usa o padrão: lista de apartamentos).
        if (substituto is null)
        {
            substituto = string.Join(", ", _apartamentos);
        }

        return template.Replace(chave, substituto);
    }

    public MimeMessage MontaMimeDoRascunho(AppConfig appConf)
    {
        var mensagem = new MimeMessage();

        mensagem.From.Add(new MailboxAddress(appConf.Nome, appConf.Remetente));
        mensagem.To.Add(new MailboxAddress(appConf.NomeDestinatario, appConf.Destinatario));
        mensagem.Subject = SubstituiPalavraChave(appConf.Assunto);

        // Todos os boletos da mesma leva compartilham o mesmo vencimento (decisão
        // tomada no início do projeto), então basta ler do primeiro — já é a data
        // real, extraída do corpo do email pela GmailMessageService, sem aproximação.
        string vencimentoFormatado = _boletos[0].Vencimento.ToString("dd/MM/yyyy");

        string corpoRascunho = SubstituiPalavraChave(
            appConf.Corpo,
            vencimentoFormatado,
            "{{dtVenc}}"
        );

        string plural = _boletos.Count > 1 ? "s" : "";
        corpoRascunho = SubstituiPalavraChave(corpoRascunho, plural, "{{plural}}");

        corpoRascunho = SubstituiPalavraChave(corpoRascunho);

        var builder = new BodyBuilder { TextBody = corpoRascunho };

        foreach (Boleto boleto in _boletos)
        {
            // Cada apartamento tem sua própria pasta no PC da Dalgiza — {{apto}} só
            // pode ser substituído aqui dentro do loop, já com o Boleto completo
            // (mesmo raciocínio já aplicado em GmailMessageService.BaixaPdf).
            string pastaDoApartamento = SubstituiPalavraChave(
                appConf.PastaDestino,
                boleto.Apartamento
            );
            FileService fs = new(pastaDoApartamento);

            builder.Attachments.Add(fs.CaminhoCompletoDoArquivo(boleto));
        }

        mensagem.Body = builder.ToMessageBody();

        return mensagem;
    }

    // Serializa o MimeMessage inteiro (headers + corpo + anexos, tudo junto) para os
    // bytes crus do formato MIME/RFC 2822, depois codifica em base64url — exatamente o
    // que a API do Gmail exige no campo Message.Raw.
    private static string SerializaParaBase64Url(MimeMessage mensagem)
    {
        using MemoryStream stream = new();
        mensagem.WriteTo(stream);

        byte[] bytesMime = stream.ToArray();

        return Base64Url.EncodeToString(bytesMime);
    }

    public async Task<Draft> CriaRascunho(AppConfig appConf)
    {
        Console.WriteLine("Montando rascunho do Gmail...");

        MimeMessage mensagem = MontaMimeDoRascunho(appConf);
        string rawBase64Url = SerializaParaBase64Url(mensagem);

        Draft rascunho = new() { Message = new Message { Raw = rawBase64Url } };

        return await _gmailService.Users.Drafts.Create(rascunho, "me").ExecuteAsync();
    }
}
