using System.Collections.Concurrent;

namespace AsyncProcessor.Tests
{
    public class QueueProcessorTests
    {
        [Fact]
        public async Task Enqueue_ShouldProcessTask()
        {
            // Arrange
            var queueProcessor = new QueueProcessor(2);
            var taskCompleted = new TaskCompletionSource<bool>();

            // Act
            await queueProcessor.EnqueueAsync(async _ =>
            {
                await Task.Delay(100);
                taskCompleted.SetResult(true);
            });

            // Assert
            Assert.True(await taskCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public async Task Enqueue_ShouldHandleException()
        {
            // Arrange
            var queueProcessor = new QueueProcessor(2);
            Exception? capturedException = null;

            // Act
            await queueProcessor.EnqueueAsync(_ => throw new TestException("Test Exception"),
                ex => capturedException = ex);

            // Allow time for the background processing
            await Task.Delay(200);

            // Assert
            Assert.NotNull(capturedException);
            Assert.IsType<TestException>(capturedException);
            Assert.Equal("Test Exception", capturedException.Message);
        }

        [Fact]
        public async Task Enqueue_ShouldProcessMultipleTasksConcurrently()
        {
            // Arrange
            var queueProcessor = new QueueProcessor(2);
            var concurrentTasks = new ConcurrentBag<int>();
            var semaphore = new SemaphoreSlim(0, 2);

            // Act
            for (int i = 0; i < 5; i++)
            {
                int taskId = i;
                await queueProcessor.EnqueueAsync(async _ =>
                {
                    concurrentTasks.Add(taskId);
                    await Task.Delay(200);
                    semaphore.Release();
                });
            }

            await semaphore.WaitAsync(TimeSpan.FromSeconds(1));
            await semaphore.WaitAsync(TimeSpan.FromSeconds(1));

            // Assert
            Assert.True(concurrentTasks.Count >= 2);
        }

        [Fact]
        public async Task Enqueue_ShouldRespectCancellationToken()
        {
            // Arrange
            var queueProcessor = new QueueProcessor(2);
            var cts = new CancellationTokenSource();
            var taskStarted = new TaskCompletionSource<bool>();
            var taskCancelled = false;

            // Act
            await queueProcessor.EnqueueAsync(async token =>
            {
                taskStarted.SetResult(true);
                try
                {
                    await Task.Delay(500, token);
                }
                catch (OperationCanceledException)
                {
                    taskCancelled = true;
                }
            }, cancellationToken: cts.Token);

            await taskStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            cts.Cancel();
            await Task.Delay(200);

            // Assert
            Assert.True(taskCancelled);
        }
    }
}
