using System.Text.Json.Serialization;

namespace Plugins
{
    public record CodeGenJsonResponse(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message);
}