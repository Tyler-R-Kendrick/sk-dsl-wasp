using System.Text.Json.Serialization;

namespace Plugins
{
    public record CodeValidationJsonResponse(
        [property: JsonPropertyName("isValid")] bool IsValid,
        [property: JsonPropertyName("errors")] string[] Errors);
}