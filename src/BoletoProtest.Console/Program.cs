using BoletoProtest.Core.Models;
using Microsoft.Extensions.Configuration;

public class Program {
    public static void Main(string[] args){
        string env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        string basePath = AppContext.BaseDirectory;
        string baseJsonFile = "appsettings.json";
        string jsonFile = $"appsettings.{env}.json";
        IConfiguration conf = new ConfigurationBuilder().SetBasePath(basePath).AddJsonFile(baseJsonFile).AddJsonFile(jsonFile, optional: true).Build();

        AppConfig appConfig = new AppConfig();
        conf.GetSection("App").Bind(appConfig);

        Console.Write("Confirmando configuração:\n");
        Console.Write($"ENV => {env}\n");
        Console.Write($"Prefixo CPF => {appConfig.CpfPrefixo}\n");
        Console.Write($"Pasta Destino => {appConfig.PastaDestino}\n");
    }
}