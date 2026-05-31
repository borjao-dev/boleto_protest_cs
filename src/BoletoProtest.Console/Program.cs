using BoletoProtest.Console.Services;
using BoletoProtest.Core.Models;

public class Program
{
    public static void Main(string[] args)
    {
        string env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        AppConfig appConf = ConfigurationService.AppConfiguration();
    }
}
