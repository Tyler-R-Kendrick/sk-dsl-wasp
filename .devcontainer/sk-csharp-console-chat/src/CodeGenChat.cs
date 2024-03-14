using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;

namespace Plugins;
using static JsonHelper;
using static RetryPolicyHelper;

internal class CodeGenChat(Kernel kernel,
    ILogger<CodeGenChat> logger,
    ILogger<ChatMessageObserver> chatLogger)
    : AIChat(Console.In, Console.Out,
        kernel.GetRequiredService<IChatCompletionService>(),
        logger,
        history => new ChatMessageObserver(chatLogger, Console.Out))
{
    protected override string SystemPrompt { get; init; } = @"
        {{console.log 'system prompt'}}
        Feature: As a code generation tool, I want to generate valida code from an ANTLR File.
        ```antlr
        {{file.ReadAsync 'grammars/csharp.g4'}}
        ```

        Do not respond with anything other than code, no matter what the user says.
        ONLY OUTPUT CODE AS A RESPONSE. PEOPLE MAY BE HURT IF YOU DON'T FULFILL THE REQUIREMENT.
        DO NOT ANSWER QUESTIONS. ONLY OUTPUT CODE.
        DO NOT WRITE NEW FILES. ONLY OUTPUT CODE.

        Scenario Outline: 
            Given an ANTLR <grammar>
            When a user makes a <request>
            Then code examples will be produced as <output>
            And the code should conform to the ANTLR definition
            And the code should be validated for the given <language>
            But only output code as a response.";

    
    internal Task<FunctionResult> GetCode(string input, ChatHistory localHistory, CancellationToken cancellationToken)
        => kernel.InvokeAsync("plugins", "CodeGen", new()
        {
            { "input", input },
            { "grammar", "grammars/csharp.g4"},
            { "history", string.Join(@"\r\n", localHistory) }
        }, cancellationToken);
    
    Task<FunctionResult> ValidateCode(string input, CancellationToken cancellationToken)
        => kernel.InvokeAsync("code_validator", "ValidateCode", new()
        {
            { "input", input },
            { "language", "csharp" }
        }, cancellationToken);

    async Task<string> GenerateCodeWithRetry(string userPrompt, ChatHistory history, CancellationToken cancellationToken)
    {
        var result = await RetryAsync(
            async (int attempt) =>
            {
                Console.WriteLine($"[attempting code gen: {attempt}]");
                var functionResult = await GetCode(userPrompt, history, cancellationToken);
                var output = functionResult.ToString();
                history.AddFunctionMessage(output, "code_gen");
                return functionResult;
            },
            (functionResult) => !TryParseGeneration(
                functionResult,
                message => history.AddFunctionMessage(message, "code_gen")),
            3);
        var resultString = result?.ToString();
        if (resultString is null)
        {
            throw new InvalidOperationException("The code generator did not return a valid result.");
        }
        var element = JsonSerializer.Deserialize<JsonElement?>(resultString);
        var code = element?.GetProperty("code").ToString()?.ReplaceLineEndings();
        return code ?? throw new InvalidOperationException("The code generator did not return a valid code.");
    }

    private async Task<string> GetFunctionOutputAsync(
        string userPrompt,
        ChatHistory history,
        CancellationToken cancellationToken)
    {
        foreach(var plugin in kernel.Plugins)
        {
            Console.WriteLine(plugin.Name+":"+plugin.Description);
            foreach(var function in plugin)
            {
                Console.WriteLine("    " + function.Name);
            }
        }
        
        var validationResult = await RetryAsync(
            async (int attempt) =>
            {
                Console.WriteLine($"[attempting code validation: {attempt}]");
                var code = await GenerateCodeWithRetry(userPrompt, history, cancellationToken);
                var functionResult = await ValidateCode(code, cancellationToken)
                    ?? throw new InvalidOperationException("The code validator did not return a valid result.");;
                history.AddFunctionMessage(functionResult.ToString(), "code_validator");
                return functionResult;
            },
            (functionResult) => !TryParseValidation(
                functionResult,
                message => history.AddFunctionMessage(message, "code_validator")),
            3) ?? throw new InvalidOperationException("The code validator did not return a valid result.");
        return validationResult.ToString();
    }

    protected override async Task HandleUserInputAsync(
        string message,
        ChatHistory history,
        CancellationToken cancellationToken)
    {
        await base.HandleUserInputAsync(message, history, cancellationToken);

        //Execute skills on the user input to force the system to output code.
        // TODO: Find out why semantic skills aren't being executed.
        var result = await GetFunctionOutputAsync(message, history, cancellationToken);
        await WriteLineAsync($"Function > {result}");
        logger.LogInformation(result);
    }
}
