using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace Plugins;
using static System.Environment;

public static class JsonHelper
{
    public delegate bool TryGetProperty(string name, out JsonElement? property);
    public static JsonElement? Get(this JsonElement element, string name) => 
        element.ValueKind != JsonValueKind.Null
        && element.ValueKind != JsonValueKind.Undefined
        && element.TryGetProperty(name, out var value) 
            ? value : null;
    
    public static JsonElement? Get(this JsonElement element, int index)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            return null;
        // Throw if index < 0
        return index < element.GetArrayLength() ? element[index] : null;
    }
    public static bool TryParseProperty(JsonElement element, string name, out JsonElement? property)
    {
        property = element.Get(name);
        return !property?.ValueKind.Equals(JsonValueKind.Undefined) ?? false;
    }

    public static bool TryParseJson(string? json, Action<string> onError, Func<TryGetProperty, bool> parseProperties)
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

    public static bool TryParseJson(FunctionResult? result, Action<string> onError,
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

    public static bool TryParseValidation(FunctionResult? result, Action<string> onError)
        => TryParseJson(result, onError, (TryGetProperty tryGetProperty) =>
        {
            var isValidProp = tryGetProperty("isValid", out var isValidProperty)
                && isValidProperty?.GetBoolean() == true;
            var hasErrors = HasErrors(tryGetProperty, onError);
            return isValidProp && !hasErrors;
        });

    public static bool TryParseGeneration(FunctionResult? result, Action<string> onError)
        => TryParseJson(result, onError, (TryGetProperty tryGetProperty) =>
        {
            var hasCode = tryGetProperty("code", out var code);
            var hasErrors = HasErrors(tryGetProperty, onError);
            return hasCode && !hasErrors;
        });
    
    private static bool HasErrors(TryGetProperty tryGetProperty, Action<string> onError)
    {
        var hasErrorsProp = tryGetProperty("errors", out var errors);
        if (hasErrorsProp)
        {
            var errorsArray = errors!.Value.EnumerateArray().Select(x => x.ToString());
            var hasErrors = errorsArray.Any();
            if(hasErrors)
            {
                onError(string.Join(NewLine, errorsArray));
                return true;
            }
            return false;
        }
        return false;
    }

    // private IObservable<string?> GenerateUserPrompt(TextReader reader)
    // {
    //     return Observable.Create<string?>(async (observer, cancellationToken) =>
    //     {
    //         var userPrompt = await reader.ReadLineAsync(cancellationToken);
    //         observer.OnNext(userPrompt);
    //         observer.OnCompleted();
    //     });
    // }
    // private ISubject<string?, string?> GenerateCodeFromUserPrompt(IObservable<string?> userPrompts)
    // {
    //     var observer = Observer.Create<string?>(
    //         onNext: userPrompt => { /* TODO: Generate code from user prompt. */ },
    //         onError: ex => { /* TODO: propogate errors. */ },
    //         onCompleted: () => { /* TODO: do something on completion. */ });
    //     return Subject.Create<string?, string?>(observer, userPrompts);
    // }
    // private ISubject<string?, string?> ValidateCodeFromUserPrompt(IObservable<string?> generatedCode)
    // {
    //     var observer = Observer.Create<string?>(
    //         onNext: code => { /* TODO: Validate code */ },
    //         onError: ex => { /* TODO: propogate errors. */ },
    //         onCompleted: () => { /* TODO: do something on completion. */ });
    //     return Subject.Create<string?, string?>(observer, generatedCode);
    // }
    // private IObservable<string?> GenerateCodeFromUserPrompt(TextReader reader)
    // {
    //     var prompts = GenerateUserPrompt(reader);
    //     var code = GenerateCodeFromUserPrompt(prompts);
    //     var validCode = ValidateCodeFromUserPrompt(code);
    //     return validCode;
    // }
    // private IObservable<string?> GenerateAssistantOutput(IObservable<string?> chatHistory)
    // {
    //     var observer = Observer.Create<string?>(
    //         onNext: userPrompt => { /* TODO: Generate assistant output from user prompt. */ },
    //         onError: ex => { /* TODO: propogate errors. */ },
    //         onCompleted: () => { /* TODO: do something on completion. */ });
    //     return Subject.Create<string?, string?>(observer, chatHistory);
    // }
}
