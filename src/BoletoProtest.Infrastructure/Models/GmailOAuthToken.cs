using System.Text.Json.Serialization;

namespace BoletoProtest.Infrastructure.Models;

public class GmailOAuthToken
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; } = 0;

    // O `-60` é uma margem de segurança: considera expirado 1 minuto antes para evitar usar um token que expira durante a chamada.
    [JsonPropertyName("is_expired")]
    public bool IsExpired => DateTime.UtcNow > CreatedAt.AddSeconds(ExpiresIn - 60);

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";
}
