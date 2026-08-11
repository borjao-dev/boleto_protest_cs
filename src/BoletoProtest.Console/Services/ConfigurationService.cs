using BoletoProtest.Core.Models;
using Microsoft.Extensions.Configuration;

namespace BoletoProtest.Console.Services;

public class ConfigurationService
{
    public static AppConfig AppConfiguration()
    {
        System.Console.WriteLine("Configurando a aplicação...");

        string env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        string basePath = AppContext.BaseDirectory;
        string baseJsonFile = "appsettings.json";
        string jsonFile = $"appsettings.{env}.json";

        System.Console.WriteLine($"Ambiente: {env}");

        IConfiguration conf = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(baseJsonFile)
            .AddJsonFile(jsonFile, optional: true)
            .Build();

        AppConfig appConfig = new();
        conf.GetSection("App").Bind(appConfig);

        return appConfig;
    }
}
