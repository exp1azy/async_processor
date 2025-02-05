namespace AsyncProcessor
{
    /// <summary>
    /// Provides utility methods for executing asynchronous tasks with error handling,
    /// retries, timeouts, and parallel execution.
    /// </summary>
    public static class AsyncProcessor
    {
        /// <summary>
        /// Executes an asynchronous task and handles exceptions using the provided error handler.
        /// </summary>
        /// <param name="action">The asynchronous task to execute.</param>
        /// <param name="onError">The action to invoke when an exception occurs.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the execution of the task.</returns>
        public static async Task RunAsync(Func<CancellationToken, Task> action, Action<Exception>? onError = null, CancellationToken cancellationToken = default) =>
            await RunInternalAsync(async ct => 
            { 
                await action(ct); 
                return (object?)null; 
            }, onError, cancellationToken);

        /// <summary>
        /// Executes an asynchronous task that returns a result and handles exceptions using the provided error handler.
        /// </summary>
        /// <typeparam name="T">The return type of the asynchronous task.</typeparam>
        /// <param name="action">The asynchronous task to execute.</param>
        /// <param name="onError">The action to invoke when an exception occurs.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The result of the task, or default(T) if an exception occurs.</returns>
        public static async Task<T?> RunAsync<T>(Func<CancellationToken, Task<T>> action, Action<Exception>? onError = null, CancellationToken cancellationToken = default) =>
            await RunInternalAsync(action, onError, cancellationToken);

        private static async Task<T?> RunInternalAsync<T>(Func<CancellationToken, Task<T>> action, Action<Exception>? onError, CancellationToken cancellationToken)
        {
            ParameterValidationHelper.ThrowIfNull(action);

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
                onError?.Invoke(ex);
                return default;
            }
        }

        /// <summary>
        /// Executes an asynchronous task with retry logic, handling exceptions using the provided error handler.
        /// </summary>
        /// <param name="delay">The delay between retry attempts.</param>
        /// <param name="action">The asynchronous task to execute.</param>
        /// <param name="onError">The action to invoke when an exception occurs.</param>
        /// <param name="retries">The maximum number of retry attempts.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the execution of the task.</returns>
        public static async Task RunWithRetryAsync(TimeSpan delay, Func<CancellationToken, Task> action, Action<Exception>? onError = null, int retries = 3, CancellationToken cancellationToken = default) =>
            await RunWithRetryInternalAsync(async ct => 
            { 
                await action(ct); 
                return (object?)null; 
            }, onError, retries, delay, cancellationToken);

        /// <summary>
        /// Executes an asynchronous task with retry logic that returns a result, handling exceptions using the provided error handler.
        /// </summary>
        /// <typeparam name="T">The return type of the asynchronous task.</typeparam>
        /// <param name="delay">The delay between retry attempts.</param>
        /// <param name="action">The asynchronous task to execute.</param>
        /// <param name="onError">The action to invoke when an exception occurs.</param>
        /// <param name="retries">The maximum number of retry attempts.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The result of the task, or default(T) if all retries fail.</returns>
        public static async Task<T?> RunWithRetryAsync<T>(TimeSpan delay, Func<CancellationToken, Task<T>> action, Action<Exception>? onError = null, int retries = 3, CancellationToken cancellationToken = default) =>
            await RunWithRetryInternalAsync(action, onError, retries, delay, cancellationToken);

        private static async Task<T?> RunWithRetryInternalAsync<T>(Func<CancellationToken, Task<T>> task, Action<Exception>? onError, int retries, TimeSpan delay, CancellationToken cancellationToken)
        {
            ParameterValidationHelper.ThrowIfNull(task);

            int retryCount = 0;

            while (true)
            {
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
                    retryCount++;
                    onError?.Invoke(ex);

                    if (retryCount >= retries)
                        return default;

                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Executes an asynchronous task with a timeout constraint.
        /// </summary>
        /// <param name="timeout">The time span after which execution is canceled.</param>
        /// <param name="action">The asynchronous task to execute.</param>
        /// <param name="onError">The action to invoke when an exception occurs.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the execution of the task.</returns>
        public static async Task RunWithTimeoutAsync(TimeSpan timeout, Func<CancellationToken, Task> action, Action<Exception>? onError = null, CancellationToken cancellationToken = default)
        {
            ParameterValidationHelper.ThrowIfNull(action);

            while (true)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await action(cancellationToken);
                    await Task.Delay(timeout, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                    return;
                }
            }
        }

        /// <summary>
        /// Executes a set of asynchronous tasks in parallel with a limited degree of concurrency.
        /// </summary>
        /// <typeparam name="T">The type of the items to process.</typeparam>
        /// <param name="items">The collection of items to process asynchronously.</param>
        /// <param name="action">The asynchronous task to execute for each item.</param>
        /// <param name="onError">The action to invoke when an exception occurs.</param>
        /// <param name="maxDegreeOfParallelism">The maximum number of concurrent tasks.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the completion of all tasks.</returns>
        public static async Task RunParallelAsync<T>(IEnumerable<T> items, Func<T, CancellationToken, Task> action, Action<Exception>? onError = null, int maxDegreeOfParallelism = 4, CancellationToken cancellationToken = default)
        {
            ParameterValidationHelper.ThrowIfNull(items, action);

            using var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
            var tasks = items.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken);

                try
                {
                    await action(item, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Starts an asynchronous task, executes a subsequent action, then waits for the initial task to complete.
        /// </summary>
        /// <param name="taskToFire">The asynchronous task to start.</param>
        /// <param name="action">The asynchronous action to execute after starting the task.</param>
        /// <param name="onError">The error handler for exceptions.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the operation.</returns>
        public static async Task FireAndWaitAsync(Func<CancellationToken, Task> taskToFire, Func<CancellationToken, Task> action, Action<Exception>? onError = null, CancellationToken cancellationToken = default) =>
            await FireAndWaitInternalAsync(
                async ct => { await taskToFire(ct); return null; },
                async ct => { await action(ct); return (object?)null; },
                onError,
                cancellationToken
            );

        /// <summary>
        /// Starts an asynchronous task that returns a result, executes a subsequent action, then waits for the initial task to complete.
        /// </summary>
        /// <typeparam name="T">The result type of both tasks.</typeparam>
        /// <param name="taskToFire">The asynchronous task to start.</param>
        /// <param name="action">The asynchronous action to execute after starting the task.</param>
        /// <param name="onError">The error handler for exceptions.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the operation, returning <typeparamref name="T"/> or default on error.</returns>
        public static async Task<T?> FireAndWaitAsync<T>(Func<CancellationToken, Task<T>> taskToFire, Func<CancellationToken, Task<T>> action, Action<Exception>? onError = null, CancellationToken cancellationToken = default) =>
            await FireAndWaitInternalAsync(taskToFire, action, onError, cancellationToken);

        private static async Task<T?> FireAndWaitInternalAsync<T>(Func<CancellationToken, Task<T>> taskToFire, Func<CancellationToken, Task<T>> action, Action<Exception>? onError, CancellationToken cancellationToken)
        {
            ParameterValidationHelper.ThrowIfNull(taskToFire, action);

            try
            {
                var firedTask = taskToFire(cancellationToken);
                await action(cancellationToken);
                return await firedTask;
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
        }
    }
}
