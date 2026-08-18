using System;
using System.Threading;
using System.Threading.Tasks;

namespace Condor.Infrastructure.Retry;

public static class RetryPolicy
{
    public static async Task<bool> ExecuteAsync(
        Func<CancellationToken, Task<bool>> action,
        int maxAttempts,
        TimeSpan delayBetweenAttempts,
        CancellationToken cancellationToken)
    {
        if (maxAttempts < 1)
        {
            maxAttempts = 1;
        }

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            try
            {
                var ok = await action(cancellationToken);
                if (ok)
                {
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                // Reintentar.
            }

            if (attempt < maxAttempts - 1 || delayBetweenAttempts > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delayBetweenAttempts, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }

        return false;
    }
}
