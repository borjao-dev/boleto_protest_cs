namespace BoletoProtest.Infrastructure.Helpers;

public static class Helper
{
    public static void MensagemEx(Exception ex, string mensagem)
    {
        Console.WriteLine($"{mensagem}\n##################\nDetalhes técnicos da exceção:\n{ex.Message}");
    }
}
