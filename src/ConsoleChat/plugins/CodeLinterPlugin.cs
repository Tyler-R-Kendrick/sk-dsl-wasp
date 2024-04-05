using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.SemanticKernel;

namespace Plugins;

[Description("Formats code using the editorconfig linter for a language.")]
public class CodeLinterPlugin
{
    [KernelFunction]
    [Description("Lints code using the ruleset contained at the path against a language.")]
    public string LintCode(
        [Description("The path to the editorconfig file")]
        string path,
        [Description("The code as input")]
        string code,
        [Description("The language of the code")]
        string language = "csharp")
    {
        ConsoleAnnotator.WriteLine("Linting code...");
        var file = EditorConfig.Core.EditorConfigFile.Parse(path);
        return language switch
        {
            "csharp" => LintCSharpCode(code, file),
            _ => "Only C# is supported at the moment.",
        };
    }

    private static string LintCSharpCode(string code, EditorConfig.Core.EditorConfigFile file)
    {
        var sections = file.Sections.Select(x => x.Glob);
        ConsoleAnnotator.WriteLine($"Sections found: {string.Join(", ", sections)}");
        var section = file.Sections
            .Where(x => x.Glob.Contains("*.cs"))
            .FirstOrDefault()
            ?? throw new Exception("No C# section found in the editorconfig file.");
        
        return LintCode(code, section);
    }
    
    private static string LintCode(string code, EditorConfig.Core.ConfigSection section)
    {
        var result = code;

        var indentStyle = section.IndentStyle;
        var indentSize = section.IndentSize;

        if(indentStyle.HasValue)
        {
            var indent = new string(' ', indentSize.NumberOfColumns ?? 4);
            result = indentStyle.Value == EditorConfig.Core.IndentStyle.Tab
                ? result.Replace(indent, "\t")
                : result.Replace("\t", indent);
        }
        return result;
    }
}