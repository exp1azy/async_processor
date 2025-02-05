using System.Collections.Concurrent;

namespace AsyncProcessor
{
    /// <summary>
    /// Provides methods to process tasks with concurrency control using locks and semaphores.
    /// </summary>
    public static class ConcurrencyProcessor
    {
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _semaphores = new();

        /// <summary>
        /// Executes a task with exclusive access, ensuring only one task runs at a time.
        /// </summary>
        /// <param name="task">The asynchronous task to be executed.</param>
        /// <param name="onError">Action to handle any exceptions thrown during task execution.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task LockAsync(Func<CancellationToken, Task> task, Action<Exception>? onError = null, CancellationToken cancellationToken = default) =>
            await LockInternalAsync(async ct => 
            { 
                await task(ct); 
                return (object?)null; 
            }, onError, cancellationToken);

        /// <summary>
        /// Executes a task with exclusive access, ensuring only one task runs at a time, and returns a result.
        /// </summary>
        /// <typeparam name="T">The result type of the task.</typeparam>
        /// <param name="task">The asynchronous task to be executed.</param>
        /// <param name="onError">Action to handle any exceptions thrown during task execution.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation, with the result of type T.</returns>
        public static async Task<T?> LockAsync<T>(Func<CancellationToken, Task<T>> task, Action<Exception>? onError = null, CancellationToken cancellationToken = default) =>
            await LockInternalAsync(task, onError, cancellationToken);

        private static async Task<T?> LockInternalAsync<T>(Func<CancellationToken, Task<T>> task, Action<Exception>? onError, CancellationToken cancellationToken)
        {
            ParameterValidationHelper.ThrowIfNull(task);

            await _lock.WaitAsync(cancellationToken);

            try
            {
                return await task(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                return default;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Executes a task with a semaphore, limiting concurrent executions to a specified maximum number.
        /// </summary>
        /// <param name="maxConcurrency">The maximum number of concurrent executions allowed.</param>
        /// <param name="task">The asynchronous task to be executed.</param>
        /// <param name="onError">Action to handle any exceptions thrown during task execution.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task SemaphoreAsync(int maxConcurrency, Func<CancellationToken, Task> task, Action<Exception>? onError = null, CancellationToken cancellationToken = default) =>
            await SemaphoreInternalAsync(async ct =>
            {
                await task(ct);
                return (object?)null;
            }, onError, maxConcurrency, cancellationToken);

        /// <summary>
        /// Executes a task with a semaphore, limiting concurrent executions to a specified maximum number, and returns a result.
        /// </summary>
        /// <typeparam name="T">The result type of the task.</typeparam>
        /// <param name="maxConcurrency">The maximum number of concurrent executions allowed.</param>
        /// <param name="task">The asynchronous task to be executed.</param>
        /// <param name="onError">Action to handle any exceptions thrown during task execution.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation, with the result of type T.</returns>
        public static async Task<T?> SemaphoreAsync<T>(int maxConcurrency, Func<CancellationToken, Task<T>> task, Action<Exception>? onError = null, CancellationToken cancellationToken = default) =>
            await SemaphoreInternalAsync(task, onError, maxConcurrency, cancellationToken);

        private static async Task<T?> SemaphoreInternalAsync<T>(Func<CancellationToken, Task<T>> task, Action<Exception>? onError, int maxConcurrency, CancellationToken cancellationToken)
        {
            ParameterValidationHelper.ThrowIfNull(task);

            var semaphore = _semaphores.GetOrAdd(maxConcurrency, _ => new SemaphoreSlim(maxConcurrency, maxConcurrency));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                return await task(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                return default;
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
