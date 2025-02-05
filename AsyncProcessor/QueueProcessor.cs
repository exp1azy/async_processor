using System.Collections.Concurrent;

namespace AsyncProcessor
{
    /// <summary>
    /// Provides functionality for processing asynchronous tasks in a queue with limited concurrency.
    /// </summary>
    /// <param name="maxConcurrency">The maximum number of concurrent tasks allowed.</param>
    public class QueueProcessor(int maxConcurrency)
    {
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _queue = new();
        private readonly SemaphoreSlim _semaphore = new(maxConcurrency, maxConcurrency);
        private readonly Lock _lock = new();
        private bool _isProcessing;

        public Task EnqueueAsync(Func<CancellationToken, Task> task, Action<Exception>? onError = null, CancellationToken cancellationToken = default)
        {
            ParameterValidationHelper.ThrowIfNull(task);

            _queue.Enqueue(task);

            bool shouldStartProcessing = false;
            lock (_lock)
            {
                if (!_isProcessing)
                {
                    _isProcessing = true;
                    shouldStartProcessing = true;
                }
            }

            if (shouldStartProcessing)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (_queue.TryDequeue(out var taskToRun))
                        {
                            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                            try
                            {
                                await taskToRun(cancellationToken).ConfigureAwait(false);
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
                                _semaphore.Release();
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    finally
                    {
                        lock (_lock)
                        {
                            _isProcessing = false;
                        }
                    }
                });
            }

            return Task.CompletedTask;
        }
    }
}
