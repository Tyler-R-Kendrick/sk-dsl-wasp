using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.SemanticKernel;

namespace Plugins;
using static System.Environment;

[Description("Validates code for a language.")]
public class CodeValidatorPlugin
{
    [KernelFunction]
    [Description("Validates code for a language.")]
    public JsonElement ValidateCode(
        [Description("The code as input")]
        string input,
        [Description("The language of the code")]
        string language = "csharp")
    {
        if(language != "csharp")
        {
            ConsoleAnnotator.WriteLine("Only C# is supported at the moment.", ConsoleColor.Red);
            return JsonSerializer.Deserialize<JsonElement>(@"{""errors"": [""Only C# is supported at the moment.""]}");
        }
        try
        {
            return ValidateCSharpCode(input);
        }
        catch (Exception ex)
        {
            ConsoleAnnotator.WriteLine($"An error occurred: {ex.Message}", ConsoleColor.Red);
            return JsonSerializer.Deserialize<JsonElement>("{\"isValid\": false}");
        }
    }

    private static JsonElement ValidateCSharpCode(string code)
    {
        code = code.ReplaceLineEndings(NewLine);
        ConsoleAnnotator.WriteLine($"code_validation:{NewLine}{code}{NewLine}", ConsoleColor.DarkBlue);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);
        var thisAssembly = typeof(CodeValidatorPlugin).Assembly;
        var referencedAssemblies = thisAssembly.GetReferencedAssemblies()
            .Select(x => MetadataReference.CreateFromFile(Assembly.Load(x).Location));
        CSharpCompilation compilation = CSharpCompilation.Create("ValidationCompilation")
            .AddReferences(referencedAssemblies)
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddSyntaxTrees(syntaxTree);
        
        var errors = compilation.GetDiagnostics()
            .Where(x => x.Severity == DiagnosticSeverity.Error)
            .Select(x => x.GetMessage());
        if(errors.Any())
        {
            var errorMessage = string.Join(NewLine, errors);
            ConsoleAnnotator.WriteLine($"validation errors: {NewLine}{errorMessage}", ConsoleColor.Red);
            return JsonSerializer.Deserialize<JsonElement>($"{{\"errors\": [\"{errorMessage}\"]}}");
        }
        return JsonSerializer.Deserialize<JsonElement>("{\"isValid\": true}");
    }
}
