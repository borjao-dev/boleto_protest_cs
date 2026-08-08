namespace BoletoProtest.Infrastructure.Utils;

public static class Util
{
    public static void MensagemEx(Exception ex, string mensagem)
    {
        Console.WriteLine(
            $"{mensagem}\n##################\nDetalhes técnicos da exceção:\n{ex.Message}"
        );
    }
}
