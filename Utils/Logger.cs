namespace BoletoProtest.Utils;

public class Logger {
    public static void Log(string texto)
    {
        Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] => {texto}");
    }
}