using BoletoProtest.Console.Services;
using BoletoProtest.Core.Models;
using BoletoProtest.Infrastructure.Services;

public class Program
{
    public static void Main(string[] args)
    {
        string env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        ConfigurationService confServ = new ConfigurationService();
        AppConfig appConf = confServ.AppConfiguration();

        Console.WriteLine("Confirmando configuração:");
        Console.WriteLine($"ENV => {env}");
        Console.WriteLine($"Prefixo CPF => {appConf.CpfPrefixo}");
        Console.WriteLine($"Pasta Destino => {appConf.PastaDestino}");
    }
}
