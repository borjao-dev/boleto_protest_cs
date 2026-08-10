using System.Globalization;
using System.Text.RegularExpressions;
using BoletoProtest.Core.Models;

namespace BoletoProtest.Infrastructure.Services;

// Responsabilidade isolada de PARSING: extrair data de vencimento e URLs de boleto
// de dentro do texto (já decodificado) de um email da PROTEST. Não faz nenhuma
// chamada de rede/API — recebe uma string pronta, devolve dados estruturados.
// Extraído de GmailMessageService para poder ser testado isoladamente, sem precisar
// de um GmailService real nem de rede (só passar uma string de exemplo).
public static class BoletoParserService
{
    public static List<BoletoEncontrado> ExtraiBoletosDoConteudo(string conteudo, string urlBoleto)
    {
        List<BoletoEncontrado> boletosEncontrados = [];

        DateTime? vencimento = ExtraiVencimento(conteudo);

        if (vencimento is null)
        {
            return boletosEncontrados;
        }

        List<string> urls = ExtraiUrlsDeBoletos(conteudo, urlBoleto);

        foreach (string url in urls)
        {
            boletosEncontrados.Add(new BoletoEncontrado(url, vencimento.Value));
        }

        return boletosEncontrados;
    }

    internal static DateTime? ExtraiVencimento(string conteudo)
    {
        Match matchData = Regex.Match(conteudo, @"vencimento:\s*(\d{2}/\d{2}/\d{4})");

        if (!matchData.Success)
        {
            return null;
        }

        return DateTime.ParseExact(
            matchData.Groups[1].Value,
            "dd/MM/yyyy",
            CultureInfo.InvariantCulture
        );
    }

    // Um mesmo email pode conter vários boletos empilhados (a PROTEST manda um aviso
    // por apartamento, mas a Dalgiza às vezes encaminha vários juntos numa "Forwarded
    // Conversation"). Regex.Matches (plural) captura TODAS as URLs do corpo, não só
    // a primeira.
    //
    // Descoberta via inspeção do conteúdo real decodificado: o HTML usa "&amp;" como
    // separador de parâmetros (entidade HTML de "&"), não "&" puro — usar "&" no Regex
    // cortava a URL no meio, gerando um link truncado/inválido que o servidor da
    // PROTEST devolvia como página de erro, não PDF. Além disso, cada URL aparece 2x
    // na mesma linha (dentro do href="" e como texto visível do link) — Distinct()
    // remove as duplicatas antes de baixar.
    //
    // Segunda descoberta: a URL base sozinha ("/Operacional") também bate com o link
    // de descadastro do rodapé de cada bloco ("/Operacional/RemoverEmail.aspx"), que
    // devolve uma página HTML, não um PDF. Exigir "/PopUp/pCli_BoletoNovo" como parte
    // obrigatória do padrão resolve, pois só a URL de boleto de verdade tem esse
    // caminho específico.
    internal static List<string> ExtraiUrlsDeBoletos(string conteudo, string urlBoleto)
    {
        MatchCollection matchesUrl = Regex.Matches(
            conteudo,
            Regex.Escape(urlBoleto) + @"/PopUp/pCli_BoletoNovo\.aspx[^\s""<>]*"
        );

        return
        [
            .. matchesUrl.Select(match => match.Value.Replace("&amp;", "&")).Distinct(),
        ];
    }
}
