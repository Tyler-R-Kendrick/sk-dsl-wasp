using System.Text.Json;
using System.Text.Json.Nodes;
using Json.More;
using Microsoft.SemanticKernel;
namespace Plugins;

internal static class JsonHelper
{
    internal delegate bool TryGetProperty(string name, out JsonElement? property);
    public static JsonElement? Get(this JsonElement element, string name) => 
        element.ValueKind != JsonValueKind.Null && element.ValueKind != JsonValueKind.Undefined && element.TryGetProperty(name, out var value) 
            ? value : (JsonElement?)null;
    
    public static JsonElement? Get(this JsonElement element, int index)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            return null;
        // Throw if index < 0
        return index < element.GetArrayLength() ? element[index] : null;
    }
    internal static bool TryParseProperty(JsonElement element, string name, out JsonElement? property)
    {
        Console.WriteLine("element: " + element);
        Console.WriteLine("name: " + name);
        property = element.Get(name);
        Console.WriteLine("property: " + property);
        return !property?.ValueKind.Equals(JsonValueKind.Undefined) ?? false;
    }

    internal static bool TryParseJson(string? json, Action<string> onError, Func<TryGetProperty, bool> parseProperties)
    {
        //KernelFunction
        //KernelReturnParameterMetadata
        if (json is null)
        {
            onError("The code generator did not return a valid JSON document.");
            return false;
        }
        var jsonDocument = JsonSerializer.Deserialize<JsonElement?>(json)
            ?? throw new InvalidOperationException("The code generator did not return a valid JSON document.");
        return parseProperties((string name, out JsonElement? property) =>
        {
            var result = TryParseProperty(jsonDocument, name, out var localProperty);
            property = localProperty;
            return result;
        });
    }

    internal static bool TryParseJson(FunctionResult? result, Action<string> onError,
        Func<TryGetProperty, bool> parseProperties)
    {
        if (result is null)
        {
            onError("The code generator did not return a valid result.");
            return false;
        }
        var stringResult = result.ToString();
        return TryParseJson(stringResult, onError, parseProperties);
    }

    internal static bool TryParseValidation(FunctionResult? result, Action<string> onError)
        => TryParseJson(result, onError, (TryGetProperty TryGetProperty) =>
        {
            var isValidProp = TryGetProperty("isValid", out var isValidProperty)
                && isValidProperty?.GetBoolean() == true;
            if (TryGetProperty("errors", out var errors))
            {
                var errorsArray = errors?.EnumerateArray().Select(x => x.GetString());
                if(errorsArray is not null && errorsArray.Any())
                {
                    onError(string.Join(@"\n", errorsArray));
                    return false;
                }
            }
            return isValidProp;
        });
    internal static bool TryParseGeneration(FunctionResult? result, Action<string> onError)
        => TryParseJson(result, onError, (TryGetProperty TryGetProperty) =>
        {
            var hasCode = TryGetProperty("code", out var code);
            if (TryGetProperty("errors", out var errors))
            {
                var errorsArray = errors?.EnumerateArray().Select(x => x.GetString());
                if(errorsArray is not null && errorsArray.Any())
                {
                    onError(string.Join(@"\n", errorsArray));
                    return false;
                }
            }
            return hasCode;
        });
}
