using BoletoProtest.Core.Models;
using BoletoProtest.Console.Services;

public class Program {
    public static void Main(string[] args)
    {
        string env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        ConfigurationService confServ = new ConfigurationService();
        AppConfig appConf = confServ.AppConfiguration();

        Console.Write("Confirmando configuração:\n");
        Console.Write($"ENV => {env}\n");
        Console.Write($"Prefixo CPF => {appConf.CpfPrefixo}\n");
        Console.Write($"Pasta Destino => {appConf.PastaDestino}\n");
    }
}