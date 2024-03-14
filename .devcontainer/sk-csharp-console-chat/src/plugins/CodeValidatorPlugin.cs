using System.ComponentModel;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.SemanticKernel;
namespace Plugins;

public class CodeValidatorPlugin
{
    [KernelFunction]
    [Description("Validates code for a language.")]
    public JsonElement ValidateCode(string code, string language = "csharp")
    {
        if(language != "csharp")
        {
            ConsoleAnnotator.WriteLine("Only C# is supported at the moment.", ConsoleColor.Red);
            return JsonSerializer.Deserialize<JsonElement>(@"{""errors"": [""Only C# is supported at the moment.""]}");
        }
        try
        {
            return ValidateCSharpCode(code);
        }
        catch (Exception ex)
        {
            ConsoleAnnotator.WriteLine($"An error occurred: {ex.Message}", ConsoleColor.Red);
            return JsonSerializer.Deserialize<JsonElement>("{\"isValid\": false}");
        }
    }

    private static JsonElement ValidateCSharpCode(string code)
    {
        ConsoleAnnotator.WriteLine($"Validating C# code: {code}", ConsoleColor.DarkBlue);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);
        CSharpCompilation compilation = CSharpCompilation.Create("ValidationCompilation")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddSyntaxTrees(syntaxTree);
        
        var errors = compilation.GetDiagnostics()
            .Where(x => x.Severity == DiagnosticSeverity.Error)
            .Select(x => x.GetMessage());
        if(errors.Any())
        {
            var errorMessage = string.Join("\n", errors);
            ConsoleAnnotator.WriteLine($"Validating code: {errorMessage}", ConsoleColor.Red);
            return JsonSerializer.Deserialize<JsonElement>($"{{\"errors\": [\"{errorMessage}\"]}}");
        }
        return JsonSerializer.Deserialize<JsonElement>("{\"isValid\": true}");
    }
}