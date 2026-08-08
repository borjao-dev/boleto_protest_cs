using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace BoletoProtest.Infrastructure.Services;

public static class GmailAuthService
{
    private const string NomeAplicacao = "BoletoProtestCs";

    private static readonly string[] Escopos = [GmailService.Scope.GmailModify];

    private static readonly string PastaConfiguracao = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "boletoprotest"
    );

    private static readonly string CaminhoCredenciais = Path.Combine(
        PastaConfiguracao,
        "credentials.json"
    );

    // Pasta onde a lib guarda o token já autorizado (equivalente ao seu antigo token.json,
    // mas gerenciado automaticamente pela lib: cria, lê, atualiza sozinha).
    private static readonly string PastaTokens = Path.Combine(PastaConfiguracao, "tokens");

    public static async Task<GmailService> BuscaGmailService()
    {
        using FileStream stream = new(CaminhoCredenciais, FileMode.Open, FileAccess.Read);

        // GoogleClientSecrets.FromStreamAsync lê o mesmo formato de credentials.json
        // que você já usava (baixado do Google Cloud Console), só que sem precisar de
        // uma classe manual (tipo o antigo GmailCredentials.cs).
        GoogleClientSecrets segredos = await GoogleClientSecrets.FromStreamAsync(stream);

        // Isto substitui inteiramente o antigo AutorizaAsync/RenovaTokenDeAcessoAsync:
        // - Se não existir token salvo em PastaTokens, abre o navegador e faz o fluxo
        //   OAuth2 completo (com PKCE incluso, gerenciado internamente pela lib).
        // - Se já existir um token salvo, ele é reaproveitado (e renovado automaticamente
        //   se estiver expirado, usando o refresh_token), sem você escrever nada disso.
        UserCredential credencial = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            segredos.Secrets,
            Escopos,
            "usuario", // identificador local do "perfil" salvo em disco; pode ser fixo
            CancellationToken.None,
            new FileDataStore(PastaTokens, true)
        );

        // GmailService é a classe "canhão" da lib: a partir dela, todo o resto
        // (GmailMessageService, GmailDraftService) vai chamar metodos tipados, tipo
        // gmailService.Users.Messages.List(...) ou gmailService.Users.Drafts.Create(...).
        return new GmailService(
            new BaseClientService.Initializer
            {
                HttpClientInitializer = credencial,
                ApplicationName = NomeAplicacao,
            }
        );
    }
}
