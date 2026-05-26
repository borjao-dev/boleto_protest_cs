using System.Text.Json.Serialization;

namespace BoletoProtest.Infrastructure.Models;

public class GmailFilteredEmails
{
    [JsonPropertyName("messages")]
    public List<GmailFilteredEmailsMessages> Messages { get; set; } = [];

    [JsonPropertyName("resultSizeEstimate")]
    public int ResultSizeEstimate { get; set; } = 0;
}

public class GmailFilteredEmailsMessages
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("threadId")]
    public string ThreadId { get; set; } = "";
}
