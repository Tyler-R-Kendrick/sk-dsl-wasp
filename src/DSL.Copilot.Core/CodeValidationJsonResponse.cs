using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using static System.Environment;

namespace Plugins
{
    public record CodeValidationJsonResponse(
        [property: JsonPropertyName("isValid")] bool IsValid,
        [property: JsonPropertyName("errors")] string[] Errors);
    
    public class CodeValidationStrategy(ChatHistory history, PluginsFunctionsFacade plugins)
    {
        public async Task<bool> ValidateAsync(FunctionResult result)
        {
            var code = result?.ToString() ?? string.Empty;
            var validationResult = await plugins.ValidateCode(
                code, "csharp");
            var resultString = validationResult.ToString().Normalize();
            ConsoleAnnotator.WriteLine(resultString, ConsoleColor.DarkGray);
            history.AddSystemMessage(resultString);
            return CodeIsValid(code, resultString);
        }

        bool CodeIsValid(string? code, string resultString)
        {
            var jsonResult = JsonSerializer.Deserialize<CodeValidationJsonResponse>(resultString);
            var isValid = jsonResult?.IsValid == true;
            var hasErrors = jsonResult?.Errors?.Length > 0;
            if(!isValid && hasErrors && code != null)
            {
                var codeString = $"The following code has errors:{NewLine}{code}";
                var errors = string.Join(NewLine, jsonResult);
                var errorString = $"Correct the following errors in the code:{NewLine}{errors}";
                var errorMessage = codeString + NewLine + errorString;
                history.AddUserMessage(errorMessage);
                ConsoleAnnotator.WriteLine(errorMessage, ConsoleColor.Red);
                return false;
            }
            return isValid;
        }
    }
}