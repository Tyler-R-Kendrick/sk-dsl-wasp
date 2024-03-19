using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a chat completion service to the list. It can be either an OpenAI or Azure OpenAI backend service.
    /// </summary>
    /// <param name="kernelBuilder"></param>
    /// <param name="kernelSettings"></param>
    /// <exception cref="ArgumentException"></exception>
    internal static IServiceCollection AddChatCompletionService(
        this IServiceCollection serviceCollection, KernelSettings kernelSettings)
        => kernelSettings.ServiceType.ToUpperInvariant() switch
        {
            ServiceTypes.AzureOpenAI => serviceCollection.AddAzureOpenAIChatCompletion(
                kernelSettings.DeploymentId,
                modelId: kernelSettings.ModelId,
                endpoint: kernelSettings.Endpoint,
                apiKey: kernelSettings.ApiKey,
                serviceId: kernelSettings.ServiceId),
            ServiceTypes.OpenAI => serviceCollection.AddOpenAIChatCompletion(
                modelId: kernelSettings.ModelId,
                apiKey: kernelSettings.ApiKey,
                orgId: kernelSettings.OrgId,
                serviceId: kernelSettings.ServiceId),
            _ => throw new ArgumentException($"Invalid service type value: {kernelSettings.ServiceType}"),
        };
    internal static IKernelBuilder AddChatCompletionService(
        this IKernelBuilder builder, KernelSettings kernelSettings)
        => kernelSettings.ServiceType.ToUpperInvariant() switch
        {
            ServiceTypes.AzureOpenAI => builder.AddAzureOpenAIChatCompletion(
                kernelSettings.DeploymentId,
                modelId: kernelSettings.ModelId,
                endpoint: kernelSettings.Endpoint,
                apiKey: kernelSettings.ApiKey,
                serviceId: kernelSettings.ServiceId),
            ServiceTypes.OpenAI => builder.AddOpenAIChatCompletion(
                modelId: kernelSettings.ModelId,
                apiKey: kernelSettings.ApiKey,
                orgId: kernelSettings.OrgId,
                serviceId: kernelSettings.ServiceId),
            _ => throw new ArgumentException($"Invalid service type value: {kernelSettings.ServiceType}"),
        };
}
