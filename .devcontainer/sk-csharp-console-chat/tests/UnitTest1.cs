using Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Moq;

namespace ConsoleChat.Tests;

public class JsonHelperTests
{
    const string testJson = @"{
        ""message"": ""The code generator succeeded."",
        ""code"": ""// Generated code for person-related functionality\npublic class Person {\n    private string name;\n    private int age;\n\n    public Person(string name, int age) {\n        this.name = name;\n        this.age = age;\n    }\n\n    public string GetName() {\n        return this.name;\n    }\n\n    public int GetAge() {\n        return this.age;\n    }\n\n    public void SetName(string name) {\n        this.name = name;\n    }\n\n    public void SetAge(int age) {\n        this.age = age;\n    }\n}"",
        ""language"": ""C#"",
        ""errors"": []
    }";

    public class TestPlugin
    {
        [KernelFunction]
        [Description("generate code for person-related functionality")]
        public string GenCode() => testJson;
    }

    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void TestParseJsonString()
    {
        var result = Plugins.JsonHelper.TryParseJson(testJson, Console.WriteLine, getter => true);
        Assert.True(result);
    }

    [Test]
    public void TestParseJsonResult()
    {
        var kernelBuilder = new KernelBuilder();
        kernelBuilder.Plugins.AddFromType<TestPlugin>();
        var kernel = kernelBuilder.Build();
        var kernelFunction = kernel.Plugins["TestPlugin"]["GenCode"];
        var result = new FunctionResult(kernelFunction, testJson);
        var stringResult = result.ToString();
        var parseResult = Plugins.JsonHelper.TryParseJson(result, Console.WriteLine, getter => 
        {
            return getter("message", out var message);
        });
        Assert.True(parseResult);
    }
}