using System.Text.Json;
using Microsoft.SemanticKernel;
namespace Plugins;

internal static class JsonHelper
{
    internal delegate bool TryGetProperty(string name, out JsonElement property);
    internal static JsonDocument? ToJsonDocument(this object? value)
    {
        if (value is null)
        {
            return null;
        }
        var json = value switch
        {
            JsonDocument document => document,
            string str => JsonDocument.Parse(str),
            _ => throw new InvalidOperationException("The value is not a valid JSON document.")
        };
        return json;
    }
    internal static bool TryParseProperty(JsonElement element, string name, out JsonElement property)
    {
        property = element.GetProperty(name);
        return !property.ValueKind.Equals(JsonValueKind.Undefined);
    }

    internal static bool TryParseJson(FunctionResult? result, Action<string> onError,
        Func<TryGetProperty, bool> parseProperties)
    {
        if (result is null)
        {
            onError("The code generator did not return a valid result.");
            return false;
        }
        var json = result.GetValue<object>().ToJsonDocument()
            ?? throw new InvalidOperationException("The code generator did not return a valid JSON document.");
        var root = json.RootElement;
        return parseProperties((string name, out JsonElement property) =>
        {
            var result = TryParseProperty(root, name, out var localProperty);
            property = localProperty;
            return result;
        });
    }
    internal static bool TryParseValidation(FunctionResult? result, Action<string> onError)
        => TryParseJson(result, onError, (TryGetProperty TryGetProperty) =>
        {
            var isValidProp = TryGetProperty("isValid", out var isValidProperty)
                && isValidProperty.GetBoolean();
            if (TryGetProperty("errors", out var errors))
            {
                onError(string.Join(@"\n", errors.EnumerateArray().Select(x => x.GetString())));
                return false;
            }
            return isValidProp;
        });
    internal static bool TryParseGeneration(FunctionResult? result, Action<string> onError)
        => TryParseJson(result, onError, (TryGetProperty TryGetProperty) =>
        {
            var hasCode = TryGetProperty("code", out var code);
            if (TryGetProperty("errors", out var errors))
            {
                onError(string.Join(@"\n", errors.EnumerateArray().Select(x => x.GetString())));
                return false;
            }
            return hasCode;
        });
}
