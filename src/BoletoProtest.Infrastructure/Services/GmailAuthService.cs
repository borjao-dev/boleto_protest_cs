using System.Buffers.Text;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using BoletoProtest.Infrastructure.Helpers;
using BoletoProtest.Infrastructure.Models;

namespace BoletoProtest.Infrastructure.Services;

public class GmailAuthService
{
    private static readonly HttpClient _clientePost = new();
    private const string Porta = "5000";

    private static readonly string CaminhoToken = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "boletoprotest",
        "token.json"
    );

    public static async Task<string> BuscaTokenDeAcessoAsync()
    {
        GmailOAuthToken? token = CarregarTokenDoDisco();

        if (token is not null)
        {
            if (token.IsExpired)
            {
                token = await RenovaTokenDeAcessoAsync(CaminhoToken, token);
            }
        }
        else
        {
            token = await AutorizaAsync();

            SalvarTokenEmDisco(CaminhoToken, token);
        }

        return token.AccessToken;
    }

    private static GmailOAuthToken? CarregarTokenDoDisco()
    {
        string caminho = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "boletoprotest",
            "token.json"
        );

        if (File.Exists(caminho))
        {
            string conteudo = File.ReadAllText(caminho);
            try
            {
                return JsonSerializer.Deserialize<GmailOAuthToken>(conteudo);
            }
            catch (JsonException jex)
            {
                Helper.MensagemEx(jex, "Erro: Arquivo JSON corrompido!");
                throw;
                return null;
            }
        }

        return null;
    }

    private static void SalvarTokenEmDisco(string caminhoToken, GmailOAuthToken token)
    {
        string serializiedToken = JsonSerializer.Serialize(token);

        File.WriteAllText(caminhoToken, serializiedToken);
    }

    private static GmailCredentialsInstalled CarregarCredenciais()
    {
        string caminhoCredenciais = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "boletoprotest",
            "credentials.json"
        );

        GmailCredentialsInstalled? credenciais = null;

        if (File.Exists(caminhoCredenciais))
        {
            string conteudo = File.ReadAllText(caminhoCredenciais);

            try
            {
                credenciais = JsonSerializer.Deserialize<GmailCredentials>(conteudo)?.Installed;
            }
            catch (JsonException jex)
            {
                Helper.MensagemEx(jex, "Erro: Arquivo JSON corrompido!");
                throw;
            }
        }

        if (credenciais is null)
        {
            throw new ArgumentNullException("Erro: Credenciais NULL!");
        }

        return credenciais;
    }

    private static async Task<GmailOAuthToken> RenovaTokenDeAcessoAsync(
        string caminhoToken,
        GmailOAuthToken antigoToken
    )
    {
        GmailCredentialsInstalled credenciais = CarregarCredenciais();

        Dictionary<string, string> dadosForm = new()
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", antigoToken.RefreshToken },
            { "client_id", credenciais.ClientId },
            { "client_secret", credenciais.ClientSecret },
        };

        using FormUrlEncodedContent conteudoPost = new(dadosForm);

        HttpResponseMessage resposta = await _clientePost.PostAsync(
            credenciais.TokenUri,
            conteudoPost
        );

        resposta.EnsureSuccessStatusCode();

        string resultado = await resposta.Content.ReadAsStringAsync();

        GmailOAuthToken tokenRetornado =
            JsonSerializer.Deserialize<GmailOAuthToken>(resultado)
            ?? throw new JsonException("Resposta inválida do servidor de tokens.");

        tokenRetornado.RefreshToken = antigoToken.RefreshToken;

        SalvarTokenEmDisco(caminhoToken, tokenRetornado);

        return tokenRetornado;
    }

    private static async Task<GmailOAuthToken> AutorizaAsync()
    {
        GmailCredentialsInstalled credenciais = CarregarCredenciais();

        /*
            O PKCE (code_verifier + code_challenge) é uma proteção extra:
            prova que quem troca o code pelo token é o mesmo que iniciou o fluxo.
            Sem ele, alguém que interceptasse o code poderia trocar por um token.
        */
        byte[] randBytes = RandomNumberGenerator.GetBytes(32);
        string codeVerifierAleatorio = Base64Url.EncodeToString(randBytes);

        byte[] verifierBytes = Encoding.ASCII.GetBytes(codeVerifierAleatorio);
        byte[] challengeBytes = SHA256.HashData(verifierBytes);
        string codeChallengeAleatorio = Base64Url.EncodeToString(challengeBytes);

        UriBuilder builder = new(credenciais.AuthUri);

        string redirectUri =
            $"{credenciais.RedirectUris[0] ?? "http://localhost"}:{Porta}/".Replace("/:", ":");

        NameValueCollection query = HttpUtility.ParseQueryString(builder.Query);
        query["client_id"] = credenciais.ClientId;
        query["redirect_uri"] = redirectUri;
        query["response_type"] = "code";
        query["scope"] = "https://www.googleapis.com/auth/gmail.modify";
        query["code_challenge"] = codeChallengeAleatorio;
        query["code_challenge_method"] = "S256";
        query["access_type"] = "offline";

        builder.Query = query.ToString();

        Uri url = builder.Uri;

        Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });

        string? code = null;

        try
        {
            int limiteTempo = 120;

            using HttpListener listener = new();
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            Task<HttpListenerContext> contextTask = listener.GetContextAsync();
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(limiteTempo));

            if (await Task.WhenAny(contextTask, timeoutTask) == timeoutTask)
            {
                listener.Stop();
                throw new OperationCanceledException("Tempo limite de autorização excedido.");
            }

            HttpListenerContext context = await contextTask;
            code = context.Request.QueryString["code"];

            context.Response.StatusCode = 200;
            await context.Response.OutputStream.WriteAsync(
                Encoding.UTF8.GetBytes("<h1>Autorizado! Pode fechar esta aba.</h1>")
            );

            context.Response.Close();
            listener.Stop();
        }
        catch (HttpListenerException hlex)
        {
            Helper.MensagemEx(hlex, "Erro: Porta já em uso ou sem permissão!");
        }
        catch (OperationCanceledException ocex)
        {
            Helper.MensagemEx(ocex, "Erro: Operação cancelada!");
        }

        if (code is null)
        {
            throw new InvalidOperationException("Falha na autorização: código não recebido.");
        }

        Dictionary<string, string> dadosForm = new()
        {
            { "code", code },
            { "client_id", credenciais.ClientId },
            { "client_secret", credenciais.ClientSecret },
            { "redirect_uri", redirectUri },
            { "grant_type", "authorization_code" },
            { "code_verifier", codeVerifierAleatorio },
        };

        using FormUrlEncodedContent conteudoPost = new(dadosForm);
        HttpResponseMessage resposta = await _clientePost.PostAsync(
            credenciais.TokenUri,
            conteudoPost
        );
        resposta.EnsureSuccessStatusCode();

        string resultado = await resposta.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<GmailOAuthToken>(resultado)
            ?? throw new JsonException("Resposta inválida do servidor de tokens.");
    }
}
