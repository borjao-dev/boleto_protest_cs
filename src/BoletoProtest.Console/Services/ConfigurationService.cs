using BoletoProtest.Core.Models;
using Microsoft.Extensions.Configuration;

namespace BoletoProtest.Console.Services;

public class ConfigurationService
{
    public AppConfig AppConfiguration()
    {
        string env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        string basePath = AppContext.BaseDirectory;
        string baseJsonFile = "appsettings.json";
        string jsonFile = $"appsettings.{env}.json";

        IConfiguration conf = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(baseJsonFile)
            .AddJsonFile(jsonFile, optional: true)
            .Build();

        AppConfig appConfig = new AppConfig();
        conf.GetSection("App").Bind(appConfig);

        return appConfig;
    }
}
