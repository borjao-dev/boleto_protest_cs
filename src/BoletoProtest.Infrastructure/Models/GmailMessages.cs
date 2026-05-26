using System.Text.Json.Serialization;

namespace BoletoProtest.Infrastructure.Models;

public class GmailMessages
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("payload")]
    public GmailMessagesPayload Payload { get; set; } = new();
}

public class GmailMessagesPayload
{
    [JsonPropertyName("headers")]
    public List<GmailMessagesHeaders> Headers { get; set; } = [];

    [JsonPropertyName("body")]
    public GmailMessagesBody Body { get; set; } = new();
}

public class GmailMessagesHeaders
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

public class GmailMessagesBody
{
    [JsonPropertyName("data")]
    public string Data { get; set; } = "";
}
