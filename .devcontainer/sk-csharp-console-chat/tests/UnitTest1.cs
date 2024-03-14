using Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Moq;

namespace sk_csharp_console_chat.Tests;

public class TestPlugin
{
    [KernelFunction]
    [Description("generate code for person-related functionality")]
    public string GenCode()
    {
        return @"{
            ""message"": ""The code generator succeeded."",
            ""code"": ""// Generated code for person-related functionality\npublic class Person {\n    private string name;\n    private int age;\n\n    public Person(string name, int age) {\n        this.name = name;\n        this.age = age;\n    }\n\n    public string GetName() {\n        return this.name;\n    }\n\n    public int GetAge() {\n        return this.age;\n    }\n\n    public void SetName(string name) {\n        this.name = name;\n    }\n\n    public void SetAge(int age) {\n        this.age = age;\n    }\n}"",
            ""language"": ""C#"",
            ""errors"": []
        }";
    }
}

public class JsonHelperTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void TestParseJsonString()
    {
        var json = @"{
            ""code"": ""// Generated code for person-related functionality\npublic class Person {\n    private string name;\n    private int age;\n\n    public Person(string name, int age) {\n        this.name = name;\n        this.age = age;\n    }\n\n    public string GetName() {\n        return this.name;\n    }\n\n    public int GetAge() {\n        return this.age;\n    }\n\n    public void SetName(string name) {\n        this.name = name;\n    }\n\n    public void SetAge(int age) {\n        this.age = age;\n    }\n}"",
            ""language"": ""C#"",
            ""errors"": []
        }";

        var result = Plugins.JsonHelper.TryParseJson(json, Console.WriteLine, getter => true);
        Assert.True(result);
    }

    [Test]
    public void TestParseJsonResult()
    {
        var json = @"{
            ""message"": ""The code generator succeeded."",
            ""code"": ""// Generated code for person-related functionality\npublic class Person {\n    private string name;\n    private int age;\n\n    public Person(string name, int age) {\n        this.name = name;\n        this.age = age;\n    }\n\n    public string GetName() {\n        return this.name;\n    }\n\n    public int GetAge() {\n        return this.age;\n    }\n\n    public void SetName(string name) {\n        this.name = name;\n    }\n\n    public void SetAge(int age) {\n        this.age = age;\n    }\n}"",
            ""language"": ""C#"",
            ""errors"": []
        }";
        var kernelBuilder = new KernelBuilder();
        kernelBuilder.Plugins.AddFromType<TestPlugin>();
        var kernel = kernelBuilder.Build();
        var kernelFunction = kernel.Plugins["TestPlugin"]["GenCode"];
        var result = new FunctionResult(kernelFunction, json);
        var stringResult = result.ToString();
        var parseResult = Plugins.JsonHelper.TryParseJson(result, Console.WriteLine, getter => 
        {
            return getter("message", out var message);
        });
        Assert.True(parseResult);

    }
}