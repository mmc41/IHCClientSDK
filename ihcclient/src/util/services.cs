using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ihc
{
    /**
     * Utility helpers for service implementations.
     */
    internal static class ServiceHelpers
    {
        /// <summary>
        /// Generic implementation of GetResourceValueChanges pattern used across IHC services.
        /// Handles subscription management, long polling, error recovery, and cleanup.
        /// </summary>
        /// <param name="activity">Existing activity to add tags and errors to</param>
        /// <param name="resourceIds">Array of resource IDs to monitor</param>
        /// <param name="enableSubscription">Async function to enable subscription/notifications for resources</param>
        /// <param name="waitForChanges">Async function that waits for changes and returns them as ResourceValue array</param>
        /// <param name="disableSubscription">Async function to disable subscription/notifications for resources</param>
        /// <param name="logger">Logger instance for diagnostics</param>
        /// <param name="asyncContinueOnCapturedContext">If true, continue on captured context for async operations</param>
        /// <param name="cancellationToken">Cancellation token to stop the async stream</param>
        /// <param name="timeout_between_waits_in_seconds">Timeout in seconds for each wait call (default 15s, should be less than 20s)</param>
        /// <returns>Async enumerable stream of ResourceValue changes</returns>
        public static async IAsyncEnumerable<ResourceValue> GetResourceValueChanges(
            Activity activity,
            int[] resourceIds,
            Func<int[], Task> enableSubscription,
            Func<int, Task<ResourceValue[]>> waitForChanges,
            Func<int[], Task> disableSubscription,
            ILogger logger,
            bool asyncContinueOnCapturedContext,
            [EnumeratorCancellation] CancellationToken cancellationToken = default,
            int timeout_between_waits_in_seconds = 15)
        {
            try
            {
                await enableSubscription(resourceIds).ConfigureAwait(asyncContinueOnCapturedContext);
            }
            catch (Exception e)
            {
                activity.SetError(e);
                logger.LogError(e, "enable subscription error");
                throw;
            }

            try
            {
                int sequentialErrorCount = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(25, cancellationToken).ConfigureAwait(asyncContinueOnCapturedContext); // Give the client a short rest between calls.
                    ResourceValue[] changes;
                    try
                    {
                        changes = await waitForChanges(timeout_between_waits_in_seconds).ConfigureAwait(asyncContinueOnCapturedContext);
                        sequentialErrorCount = 0;
                    }
                    catch (Exception e)
                    {
                        activity.SetError(e);
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
                            await Task.Delay(sequentialErrorCount * sequentialErrorCount * 100, cancellationToken).ConfigureAwait(asyncContinueOnCapturedContext);
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
                    await Task.Delay(25, cancellationToken).ConfigureAwait(asyncContinueOnCapturedContext); // Give the client a short rest between calls.
                    await disableSubscription(resourceIds).ConfigureAwait(asyncContinueOnCapturedContext);
                }
                catch (Exception e)
                {
                    activity.SetError(e);
                    logger.LogError(e, "disable subscription error");
                    // Do not re-throw in finally block to avoid masking exceptions from try block
                }
            }
        }
    }
}