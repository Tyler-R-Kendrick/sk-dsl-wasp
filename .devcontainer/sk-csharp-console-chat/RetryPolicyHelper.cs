namespace Plugins;

internal class RetryPolicyHelper
{        
    internal static async Task<T?> RetryAsync<T>(
        Func<int, Task<T?>> action,
        Func<T?, bool> retryCondition,
        int maxAttempts)
    {
        T? result = default;
        int attempt = 1;
        do
        {
            result = await action(attempt);
        }
        while(attempt++ <= maxAttempts && retryCondition(result));
        return result;
    }
    
    internal static TResult Retry<TResult>(
        Func<TResult> resultGenerator,
        Func<TResult, bool> retryCondition,
        int maxRetries = 3)
    {
        int retries = 0;
        TResult result;
        do
        {
            result = resultGenerator();
            if (!retryCondition(result))
            {
                return result;
            }
            retries++;
        } while (retries < maxRetries);
        return result;
    }

}
