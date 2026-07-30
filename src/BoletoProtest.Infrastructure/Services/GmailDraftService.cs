using BoletoProtest.Core.Models;

namespace BoletoProtest.Infrastructure.Services;

public class GmailDraftService
{
    /*
    ===== PLANO — GmailDraftService (decisões tomadas em par-programming) =====

    BLOQUEIO ANTERIOR A TUDO ISSO (resolver primeiro):
    - GmailMessageService.BaixaPdf() cria cada Boleto com Apartamento = "apartamento_aqui"
      (hardcoded). Sem o apartamento real por boleto, não há como saber quais aps foram
      efetivamente baixados nesta execução. Já catalogado em docs/revisao-tecnica.md (#3).
      Investigar: o nº do apartamento provavelmente vem no corpo/assunto do email da
      PROTEST, ou embutido na própria URL do boleto — extrair via Regex, igual à URL.

    PRINCÍPIO GERAL:
    - A lista de apartamentos usada no Assunto/Corpo do rascunho SEMPRE deve vir dos
      `Boleto`s efetivamente baixados nesta execução (List<Boleto> boletos), NUNCA de
      appConf.ApartamentosGerenciados. Isso evita o rascunho "mentir" (ex: dizer que
      há 3 boletos anexados quando só 2 foram baixados com sucesso).

    MÉTODOS PLANEJADOS:
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
       - Retorna a string em base64url pronta para o campo `raw`.

    3. MontaMensagemRascunho(...) : ainda a definir assinatura exata
       - Recebe o base64url de MontaMimeDoRascunho.
       - Monta os objetos GmailDraftMessage { raw } e GmailDraftRequest { message }
         (classes com [JsonPropertyName], seguindo o padrão já usado em GmailMessages.cs,
         GmailFilteredEmails.cs etc. — NUNCA montar o JSON via interpolação de string
         manual: foge do padrão do projeto e é frágil contra escaping de aspas/
         caracteres especiais).

    4. Método de envio (nome a definir) : Task<...>
       - Serializa o objeto de (3) com JsonSerializer.Serialize.
       - POST para https://gmail.googleapis.com/gmail/v1/users/me/drafts
         (mesmo padrão de HttpClient/Bearer token dos outros services: GmailAuthService.
         BuscaTokenDeAcessoAsync(), EnsureSuccessStatusCode(), etc.)
       - Fica no mesmo GmailDraftService (não justifica um service novo: é o mesmo
         endpoint/assunto — "drafts" — e os outros services do projeto (Search, Message)
         já misturam "montar dado" + "chamar API" no mesmo service).

    NÃO CRIAR:
    - Método isolado só para o Join de apartamentos (operação de 1 linha, sem motivo
      pra crescer em complexidade própria).
    - Método isolado só para a codificação base64url (idem: 1 linha, API pronta do C#).
    - Service novo separado para o envio do rascunho.
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
