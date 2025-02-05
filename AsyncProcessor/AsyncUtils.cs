using System.Collections.Concurrent;

namespace AsyncProcessor
{
    /// <summary>
    /// Provides a set of utility methods for handling asynchronous operations.
    /// </summary>
    public static class AsyncUtils
    {
        private readonly static ConcurrentDictionary<string, SemaphoreSlim> _throttleLocks = new();
        private readonly static ConcurrentDictionary<string, Task> _runOnceTasks = new();

        /// <summary>
        /// Ensures that the provided asynchronous action does not execute more frequently than the specified interval.
        /// </summary>
        /// <param name="key">A unique key representing the throttled operation.</param>
        /// <param name="interval">The time span to wait before allowing the next execution.</param>
        /// <param name="action">The asynchronous action to be throttled.</param>
        /// <param name="onError">An action to handle any exceptions that occur during execution.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task ThrottleAsync(string key, TimeSpan interval, Func<CancellationToken, Task> action, Action<Exception>? onError = null, CancellationToken cancellationToken = default) =>
            await ThrottleInternalAsync(async ct =>
            {
                await action(ct);
                return (object?)null;
            }, onError, key, interval, cancellationToken);

        /// <summary>
        /// Ensures that the provided asynchronous function does not execute more frequently than the specified interval.
        /// </summary>
        /// <typeparam name="T">The return type of the asynchronous function.</typeparam>
        /// <param name="key">A unique key representing the throttled operation.</param>
        /// <param name="interval">The time span to wait before allowing the next execution.</param>
        /// <param name="action">The asynchronous function to be throttled.</param>
        /// <param name="onError">An action to handle any exceptions that occur during execution.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the result of the asynchronous function.</returns>
        public static async Task<T?> ThrottleAsync<T>(string key, TimeSpan interval, Func<CancellationToken, Task<T>> action, Action<Exception>? onError = null, CancellationToken cancellationToken = default) =>
            await ThrottleInternalAsync(action, onError, key, interval, cancellationToken);

        private static async Task<T?> ThrottleInternalAsync<T>(Func<CancellationToken, Task<T>> action, Action<Exception>? onError, string key, TimeSpan interval, CancellationToken cancellationToken)
        {
            ParameterValidationHelper.ThrowIfNull(key, action);

            var semaphore = _throttleLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            if (!await semaphore.WaitAsync(0, cancellationToken)) return default;

            try
            {
                return await action(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (onError != null)
                    onError(ex);
                else
                    throw;

                return default;
            }
            finally
            {
                _ = Task.Delay(interval, cancellationToken).ContinueWith(_ => semaphore.Release());
            }
        }

        /// <summary>
        /// Ensures that the specified asynchronous action executes only once per key.
        /// </summary>
        /// <param name="key">A unique key representing the operation.</param>
        /// <param name="action">The asynchronous action to execute.</param>
        /// <param name="onError">An action to handle any exceptions that occur during execution.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RunOnceAsync(string key, Func<CancellationToken, Task> action, Action<Exception>? onError = null, CancellationToken cancellationToken = default)
        {
            ParameterValidationHelper.ThrowIfNull(key, action);

            try
            {
                await _runOnceTasks.GetOrAdd(key, _ => action(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (onError != null)                
                    onError(ex);              
                else
                    throw;
            }
        }

        /// <summary>
        /// Resets the stored tasks for one-time execution, allowing the same keys to be reused for new executions.
        /// </summary>
        public static void ResetTasks() => _runOnceTasks.Clear();

        /// <summary>
        /// Resets the stored semaphores used for throttling, removing all existing rate-limiting locks.
        /// </summary>
        public static void ResetSemaphores() => _throttleLocks.Clear();
    }
}
