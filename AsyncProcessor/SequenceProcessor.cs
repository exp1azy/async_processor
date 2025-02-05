using System.Collections.Concurrent;

namespace AsyncProcessor
{
    /// <summary>
    /// Provides methods to process tasks in sequence, ensuring that tasks are executed one after another.
    /// </summary>
    public static class SequenceProcessor
    {
        private static readonly SemaphoreSlim _sequenceLock = new(1, 1);
        private static readonly ConcurrentQueue<Func<CancellationToken, Task>> _queue = new();

        /// <summary>
        /// Enqueues and executes a task in sequence, ensuring that tasks are run one after the other.
        /// </summary>
        /// <param name="task">The asynchronous task to be executed.</param>
        /// <param name="onError">Action to handle any exceptions thrown during task execution.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RunInSequenceAsync(Func<CancellationToken, Task> task, Action<Exception>? onError = null, CancellationToken cancellationToken = default)
        {
            ParameterValidationHelper.ThrowIfNull(task);

            _queue.Enqueue(task);
            await _sequenceLock.WaitAsync(cancellationToken);

            try
            {
                while (_queue.TryDequeue(out var taskToRun))
                    await taskToRun(cancellationToken);
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
                _sequenceLock.Release();
            }
        }
    }
}
