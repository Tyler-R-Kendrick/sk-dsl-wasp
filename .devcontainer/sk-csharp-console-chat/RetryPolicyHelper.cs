namespace Plugins;

internal class RetryPolicyHelper
{
    internal static TResult Invoke<TResult, TContext>(
        Func<TResult> resultGenerator, Func<TResult, TContext> contextFactory,
        Func<TContext, bool> retryCondition, int maxRetries = 3)
    {
        int retries = 0;
        TResult result;
        do
        {
            result = resultGenerator();
            var context = contextFactory(result);
            if (!retryCondition(context))
            {
                return result;
            }
            retries++;
        } while (retries < maxRetries);
        return result;
    }

}
