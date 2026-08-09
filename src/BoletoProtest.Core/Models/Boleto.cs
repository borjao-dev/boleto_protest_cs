namespace BoletoProtest.Core.Models;

public record Boleto(string Link, string Apartamento, DateTime Vencimento);

// Estágio intermediário: representa o que já se sabe sobre um boleto logo após
// encontrá-lo no corpo do email (link + data de vencimento reais, extraídos via
// Regex do texto do email da PROTEST). Ainda não tem o Apartamento, que só é
// conhecido depois de baixar e ler o PDF (ver PdfService.BuscaNumeroApartamentoFormatado).
// Vira um Boleto completo assim que o apartamento é identificado, em BaixaPdf.
public record BoletoEncontrado(string Link, DateTime Vencimento);
