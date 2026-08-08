namespace BoletoProtest.Core.Models;

public class AppConfig
{
    //* PROPRIEDADES da classe
    public string CpfPrefixo { get; set; } = "";
    public string PastaDestino { get; set; } = "";
    public string Assunto { get; set; } = "";
    public string Corpo { get; set; } = "";
    public string Destinatario { get; set; } = "";
    public string Remetente { get; set; } = "";
    public string Nome { get; set; } = "";
    public string NomeDestinatario { get; set; } = "";
    public string AssuntoBusca { get; set; } = "";
    public string UrlBoleto { get; set; } = "";
    public List<string> ApartamentosPossuidos { get; set; } = [];
    public List<string> ApartamentosGerenciados { get; set; } = [];
}
