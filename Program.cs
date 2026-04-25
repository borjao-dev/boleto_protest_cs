using BoletoProtest.Utils;

public class Program
{
    public static void Main(string[] args)
    {
        string texto = "EXEMPLO!";
        Logger.Log(texto);
        Logger.Log("");
        // Logger.Log(); //! => causa ERROR
    }
}