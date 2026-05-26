using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BoletoProtest.Core.Models;
using BoletoProtest.Infrastructure.Models;

namespace BoletoProtest.Infrastructure.Services;

public class GmailMessageService
{
    private const string Protocolo = "https://";

    private const string Dominio = "gmail.googleapis.com";

    private const string Caminho = "/gmail/v1/users/me/messages";

    private static readonly HttpClient _clienteGet = new();

    public static async Task<List<GmailMessages>> BuscaMensagensGmail(AppConfig appConf)
    {
        GmailFilteredEmails ids = await GmailSearchService.BuscaEmailsContendoTermo(appConf);

        string tokenDeAcesso = await GmailAuthService.BuscaTokenDeAcessoAsync();

        _clienteGet.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokenDeAcesso
        );

        List<GmailMessages> lista = [];

        foreach (GmailFilteredEmailsMessages emailRef in ids.Messages)
        {
            string uri = $"{Protocolo}{Dominio}{Caminho}/{emailRef.Id}";

            HttpResponseMessage resposta = await _clienteGet.GetAsync(uri);
            resposta.EnsureSuccessStatusCode();

            string resultado = await resposta.Content.ReadAsStringAsync();

            lista.Add(
                JsonSerializer.Deserialize<GmailMessages>(resultado)
                    ?? throw new JsonException("Resposta inválida do servidor de mensagens.")
            );
        }

        return lista;
    }

    public static async Task<List<string>> FiltraEmailsPorData(AppConfig appConf)
    {
        DateTime mesQueVem = DateTime.Now.AddMonths(1);
        string vencimento = mesQueVem.ToString("MM/yyyy");

        List<GmailMessages> mensagensGmail = await BuscaMensagensGmail(appConf);

        List<string> urlsBoletos = [];

        foreach (GmailMessages mensagem in mensagensGmail)
        {
            string corpo = mensagem.Payload.Body.Data;
            string base64Padrao = corpo.Replace('-', '+').Replace('_', '/');
            byte[] bytes = Convert.FromBase64String(base64Padrao);
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
}
