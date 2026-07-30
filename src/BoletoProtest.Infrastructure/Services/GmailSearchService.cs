using System.Net.Http.Headers;
using System.Text.Json;
using BoletoProtest.Core.Models;
using BoletoProtest.Infrastructure.Models;

namespace BoletoProtest.Infrastructure.Services;

public class GmailSearchService
{
    private const string Protocolo = "https://";

    private const string Dominio = "gmail.googleapis.com";

    private const string Caminho = "/gmail/v1/users/me/messages";

    private const string Consulta = "?q=";

    private static readonly HttpClient _clienteGet = new();

    public static async Task<GmailFilteredEmails> BuscaEmailsContendoTermo(AppConfig appConf)
    {
        string busca = $"subject:\"{appConf.AssuntoEmailBusca}\" from:{appConf.EmailRemetente}";

        string uri = $"{Protocolo}{Dominio}{Caminho}{Consulta}{Uri.EscapeDataString(busca)}";

        string tokenDeAcesso = await GmailAuthService.BuscaTokenDeAcessoAsync();

        _clienteGet.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokenDeAcesso
        );

        HttpResponseMessage resposta = await _clienteGet.GetAsync(uri);
        resposta.EnsureSuccessStatusCode();

        string resultado = await resposta.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<GmailFilteredEmails>(resultado)
            ?? throw new JsonException("Resposta inválida do servidor de Emails.");
    }
}
