using BoletoProtest.Core.Models;

namespace BoletoProtest.Infrastructure.Services;

public class GmailDraftService
{
    /*
    ===== PLANO — GmailDraftService (atualizado após decisão de migrar p/ Google.Apis.Gmail.v1) =====

    MUDANÇA DE ESTRATÉGIA (registrada aqui pra não esquecer o porquê):
    - Projeto passou a usar os pacotes NuGet oficiais Google.Apis.Gmail.v1 (chamadas à API,
      autenticação OAuth2) e PdfPig (extração de texto de PDF), em vez de HttpClient +
      models manuais + regex de PDF cru. Motivo: reduzir boilerplate de infraestrutura
      (parsing de JSON, montagem de URL, gestão de token) e focar o tempo/aprendizado na
      lógica de negócio específica do projeto (MIME, apartamentos, templates).
    - Código antigo 100% funcional preservado na branch de backup do Git, caso seja
      necessário consultar/reverter.
    - GmailCredentials.cs, GmailFilteredEmails.cs, GmailMessages.cs, GmailOAuthToken.cs
      (models manuais) e GmailAuthService.cs / GmailSearchService.cs (versões antigas)
      foram apagados: a lib já traz os equivalentes prontos.

    BLOQUEIO ANTERIOR A TUDO ISSO (resolver primeiro):
    - GmailMessageService.BaixaPdf() cria cada Boleto com Apartamento = "apartamento_aqui"
      (hardcoded). Sem o apartamento real por boleto, não há como saber quais aps foram
      efetivamente baixados nesta execução. Já catalogado em docs/revisao-tecnica.md (#3).
      Decisão tomada: o nº do apartamento NÃO vem no e-mail da PROTEST (confirmado por
      inspeção manual de um e-mail real) — só existe dentro do texto do próprio PDF
      (ex: "UNIDADE: BL A - AP 1503"). Precisa: extrair texto do PDF via PdfPig, depois
      Regex/parsing de string pra reformatar de "BL A - AP 1503" para o padrão já usado
      no resto do sistema, "1503-A".

    PRINCÍPIO GERAL:
    - A lista de apartamentos usada no Assunto/Corpo do rascunho SEMPRE deve vir dos
      `Boleto`s efetivamente baixados nesta execução (List<Boleto> boletos), NUNCA de
      appConf.ApartamentosGerenciados. Isso evita o rascunho "mentir" (ex: dizer que
      há 3 boletos anexados quando só 2 foram baixados com sucesso).

    MÉTODOS PLANEJADOS (ainda válidos com a lib nova — MIME continua sendo escrito à mão,
    a lib não monta isso por você, só facilita transporte/autenticação):
    1. SubstituiPalavraChave(string template, List<Boleto> boletosBaixados) : string
       - Extrai os apartamentos dos boletos baixados (não do appConf!), junta com
         string.Join(", ", ...) internamente, e substitui "[[apto]]" no template.
       - Usado 2x: uma para AssuntoRascunho, uma para CorpoRascunho.
       - Não recebe array pronto: monta a string de apartamentos internamente a partir
         da List<Boleto> recebida (tarefa pequena o suficiente para não merecer
         método próprio separado).

    2. MontaMimeDoRascunho(AppConfig appConf) : Task<string>  [substitui a antiga MontaRascunho]
       - Chama SubstituiPalavraChave() para Assunto e para Corpo.
       - Monta o texto MIME/RFC 2822 completo: headers (From/To/Subject) + boundary +
         parte de texto + parte(s) de anexo Pdf em base64 (um anexo por boleto baixado).
       - Codifica o texto MIME inteiro em base64url (Base64Url.EncodeToString), como
         última linha do próprio método (não merece método isolado: é 1 linha usando
         API pronta do C#, sem lógica própria de negócio).
       - Retorna a string em base64url pronta para o campo `raw` de Message.Raw (classe
         da própria lib, Google.Apis.Gmail.v1.Data.Message — não precisamos mais de
         GmailDraftMessage/GmailDraftRequest manuais).

    3. MontaMensagemRascunho(...) : ainda a definir assinatura exata
       - Recebe o base64url de MontaMimeDoRascunho.
       - Monta um Google.Apis.Gmail.v1.Data.Draft { Message = new Message { Raw = ... } }
         (classes já prontas da lib, sem [JsonPropertyName] manual).

    4. Método de envio (nome a definir) : Task<...>
       - Usa gmailService.Users.Drafts.Create(draft, "me").ExecuteAsync() — SEM montar URL,
         SEM JsonSerializer manual, SEM HttpClient/Bearer token manual.
       - Precisa de um GmailService autenticado — ver GmailAuthService.BuscaGmailService()
         abaixo, chamado uma vez e reaproveitado.
       - Fica no mesmo GmailDraftService (mesmo raciocínio de antes: mesmo endpoint/assunto
         "drafts", outros services do projeto já misturam "montar dado" + "chamar API").

    NÃO CRIAR:
    - Método isolado só para o Join de apartamentos (operação de 1 linha, sem motivo
      pra crescer em complexidade própria).
    - Método isolado só para a codificação base64url (idem: 1 linha, API pronta do C#).
    - Service novo separado para o envio do rascunho.

    ===== PLANO — GmailAuthService (novo, usando Google.Apis.Auth) =====

    - Método único: BuscaGmailService() : Task<GmailService>
      - Carrega credentials.json (client_id/client_secret) via API própria da lib
        (GoogleClientSecrets ou similar).
      - Chama GoogleWebAuthorizationBroker.AuthorizeAsync(...) passando: credenciais,
        scopes (ex: "https://www.googleapis.com/auth/gmail.modify"), pasta de cache de
        token. A lib cuida sozinha de: abrir navegador, listener local, troca de code por
        token, refresh automático, e cache em disco entre execuções — nada disso precisa
        ser escrito à mão como antes (SalvarTokenEmDisco/CarregarTokenDoDisco somem).
      - Monta e retorna um novo GmailService(...) já configurado com essas credenciais.
      - Um método só é suficiente: é basicamente orquestrar chamadas prontas da lib, sem
        lógica de negócio própria significativa (SRP não exige split aqui).

    - Caminhos: pasta base fixa (decidida por nós, não muda por usuário/ambiente) +
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) (isso sim varia
      automaticamente por SO/usuário, calculado pelo próprio .NET em runtime — não é
      "hardcoded por ambiente", é dinâmico por natureza).
      - Constante única para a PASTA BASE: algo como
        PastaConfiguracao = Path.Combine(UserProfile, ".config", "boletoprotest")
      - credentials.json e a pasta de cache de token são montados A PARTIR dessa base
        (Path.Combine(PastaConfiguracao, "credentials.json") etc.), em vez de 2 constantes
        totalmente independentes — evita repetir ".config/boletoprotest" hardcoded 2x.
*/
    public static async Task<string> MontaRascunho(AppConfig appConf)
    {
        List<Boleto> boletos = await GmailMessageService.BaixaPdf(appConf);
        // byte[] arquivoPdf = await LerArquivoPdf(boleto, appConf);

        string apsGerenciados = string.Join(", ", appConf.ApartamentosGerenciados);

        string rascunho = $"{appConf.CorpoRascunho.Replace("[[apto]]", $"{apsGerenciados}")}";

        return rascunho;
    }
}
