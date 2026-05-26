namespace BoletoProtest.Core.Models;

public class AppConfig
{
    //* PROPRIEDADES da classe
    public string CpfPrefixo { get; set; } = "";
    public string PastaDestino { get; set; } = "";
    public string TemplateGmail { get; set; } = "";
    public string EmailDestinatario { get; set; } = "";
    public string EmailRemetente { get; set; } = "";
    public string AssuntoEmail { get; set; } = "";
    public string UrlBoleto { get; set; } = "";
    public List<string> ApartamentosGerenciados { get; set; } = [];
}
