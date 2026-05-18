using System.Buffers.Text;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using BoletoProtest.Infrastructure.Models;

namespace BoletoProtest.Infrastructure.Services;

public class GmailAuthService
{
    private static readonly HttpClient _clientePost = new();

    public async Task<string> GetAccessTokenAsync()
    {
        GmailOAuthToken? token = LoadTokenFromDisk();

        string caminhoToken = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "boletoprotest",
            "token.json"
        );

        if (token is not null)
        {
            if (token.IsExpired)
            {
                token = await RefreshAccessTokenAsync(caminhoToken, token);
            }
        }
        else
        {
            token = await AuthorizeAsync();

            string serializiedToken = JsonSerializer.Serialize(token);

            File.WriteAllText(caminhoToken, serializiedToken);
        }

        return token.AccessToken;
    }

    private GmailOAuthToken? LoadTokenFromDisk()
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
                Console.Write($"Erro: Arquivo JSON corrompido! => {jex.Message}");
                return null;
            }
        }

        return null;
    }

    private async Task<GmailOAuthToken> RefreshAccessTokenAsync(
        string caminhoToken,
        GmailOAuthToken antigoToken
    )
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
                Console.Write($"Erro: Arquivo JSON corrompido! => {jex.Message}");
            }
        }

        if (credenciais is null)
        {
            throw new ArgumentNullException("Erro: Credenciais NULL!");
        }

        var dadosForm = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", antigoToken?.RefreshToken ?? "" },
            { "client_id", credenciais.ClientId },
            { "client_secret", credenciais.ClientSecret },
        };

        using var conteudoPost = new FormUrlEncodedContent(dadosForm);
        var resposta = await _clientePost.PostAsync(credenciais.TokenUri, conteudoPost);
        resposta.EnsureSuccessStatusCode();

        string resultado = await resposta.Content.ReadAsStringAsync();

        GmailOAuthToken tokenRetornado =
            JsonSerializer.Deserialize<GmailOAuthToken>(resultado)
            ?? throw new JsonException("Resposta inválida do servidor de tokens.");

        tokenRetornado.RefreshToken = antigoToken?.RefreshToken ?? "";

        string novoToken = JsonSerializer.Serialize(tokenRetornado);

        File.WriteAllText(caminhoToken, novoToken);

        return tokenRetornado;
    }

    private async Task<GmailOAuthToken> AuthorizeAsync()
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
                Console.Write($"Erro: Arquivo JSON corrompido! => {jex.Message}");
            }
        }

        if (credenciais is null)
        {
            throw new ArgumentNullException("Erro: Credenciais NULL!");
        }

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

        var builder = new UriBuilder(credenciais.AuthUri);
        string porta = "5000";
        string redirectUri =
            $"{credenciais.RedirectUris[0] ?? "http://localhost"}:{porta}/".Replace("/:", ":");

        var query = HttpUtility.ParseQueryString(builder.Query);
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

            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            var contextTask = listener.GetContextAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(limiteTempo));

            if (await Task.WhenAny(contextTask, timeoutTask) == timeoutTask)
            {
                listener.Stop();
                throw new OperationCanceledException("Tempo limite de autorização excedido.");
            }

            var context = await contextTask;
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
            Console.Write($"Erro: Porta já em uso ou sem permissão! => {hlex.Message}");
        }
        catch (OperationCanceledException ocex)
        {
            Console.Write($"Erro: Operação cancelada! => {ocex.Message}");
        }

        var dadosForm = new Dictionary<string, string>
        {
            { "code", code ?? "" },
            { "client_id", credenciais.ClientId },
            { "client_secret", credenciais.ClientSecret },
            { "redirect_uri", redirectUri },
            { "grant_type", "authorization_code" },
            { "code_verifier", codeVerifierAleatorio },
        };

        using var conteudoPost = new FormUrlEncodedContent(dadosForm);
        var resposta = await _clientePost.PostAsync(credenciais.TokenUri, conteudoPost);
        resposta.EnsureSuccessStatusCode();

        string resultado = await resposta.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<GmailOAuthToken>(resultado)
            ?? throw new JsonException("Resposta inválida do servidor de tokens.");
    }
}
