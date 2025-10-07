using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ihc
{
    /**
     * Utility helpers for service implementations.
     */
    public static class ServiceHelpers
    {
        /**
         * Generic implementation of GetResourceValueChanges pattern used across IHC services.
         * Handles subscription management, long polling, error recovery, and cleanup.
         *
         * @param resourceIds Array of resource IDs to monitor
         * @param enableSubscription Async function to enable subscription/notifications for resources
         * @param waitForChanges Async function that waits for changes and returns them as ResourceValue array
         * @param disableSubscription Async function to disable subscription/notifications for resources
         * @param logger Logger instance for diagnostics
         * @param cancellationToken Cancellation token to stop the async stream
         * @param timeout_between_waits_in_seconds Timeout in seconds for each wait call (default 15s, should be < 20s)
         * @returns Async enumerable stream of ResourceValue changes
         */
        public static async IAsyncEnumerable<ResourceValue> GetResourceValueChanges(
            int[] resourceIds,
            Func<int[], Task> enableSubscription,
            Func<int, Task<ResourceValue[]>> waitForChanges,
            Func<int[], Task> disableSubscription,
            ILogger logger,
            [EnumeratorCancellation] CancellationToken cancellationToken = default,
            int timeout_between_waits_in_seconds = 15)
        {
            try
            {
                await enableSubscription(resourceIds);
            }
            catch (Exception e)
            {
                logger.LogError(e, "enable subscription error");
                throw;
            }

            try
            {
                int sequentialErrorCount = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(25, cancellationToken); // Give the client a short rest between calls.
                    ResourceValue[] changes;
                    try
                    {
                        changes = await waitForChanges(timeout_between_waits_in_seconds);
                        sequentialErrorCount = 0;
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "wait for changes failed #" + sequentialErrorCount);
                        changes = new ResourceValue[] { };
                        if (++sequentialErrorCount > 10)
                        {
                            logger.LogError(e, "wait for changes repeated failure");
                            throw; // Fail hard if exception repeats.
                        }
                        else
                        {
                            // Allow server to recover.
                            await Task.Delay(sequentialErrorCount * sequentialErrorCount * 100, cancellationToken);
                        }
                    }

                    foreach (var change in changes)
                    {
                        yield return change;
                    }
                }
            }
            finally
            {
                try
                {
                    await Task.Delay(25); // Give the client a short rest between calls.
                    await disableSubscription(resourceIds);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "disable subscription error");
                    throw;
                }
            }
        }
    }
}