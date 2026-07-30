namespace BoletoProtest.Infrastructure.Helpers;

public static class Helper
{
    public static void MensagemEx(Exception ex, string mensagem)
    {
        Console.WriteLine(
            $"{mensagem}\n##################\nDetalhes técnicos da exceção:\n{ex.Message}"
        );
    }

    public static string CapturaStringEntreStrings(string texto, string inicio, string final)
    {
        int indiceInicio = texto.IndexOf(inicio);
        if (indiceInicio == -1)
        {
            return string.Empty;
        }

        indiceInicio += inicio.Length;

        int indiceFinal = texto.IndexOf(final);
        if (indiceFinal == -1)
        {
            return string.Empty;
        }

        return texto[indiceInicio..indiceFinal];
    }
}
