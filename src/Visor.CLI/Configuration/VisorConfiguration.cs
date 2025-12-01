using System.Text.Json.Serialization;

namespace Visor.CLI.Configuration;

public class VisorConfiguration
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("connectionString")]
    public string? ConnectionString { get; set; }

    [JsonPropertyName("output")]
    public string? Output { get; set; }

    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }
}
