using Moq;

namespace AsyncProcessor.Tests
{
    public class AsyncProcessorTests
    {
        [Fact]
        public async Task Run_ShouldExecuteActionSuccessfully()
        {
            // Arrange
            bool executed = false;
            async Task Action(CancellationToken _) => executed = true;

            // Act
            await AsyncProcessor.RunAsync(Action);

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public async Task Run_ShouldInvokeOnErrorOnException()
        {
            // Arrange
            var exceptionThrown = new InvalidOperationException("Test Exception");
            var onErrorMock = new Mock<Action<Exception>>();

            async Task Action(CancellationToken _) => throw exceptionThrown;

            // Act
            await AsyncProcessor.RunAsync(Action, onErrorMock.Object);

            // Assert
            onErrorMock.Verify(e => e(It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task Run_ShouldReturnResultOnSuccess()
        {
            // Arrange
            async Task<int> Action(CancellationToken _) => 42;

            // Act
            var result = await AsyncProcessor.RunAsync(Action);

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task Run_ShouldReturnDefaultOnException()
        {
            // Arrange
            async Task<int> Action(CancellationToken _) => throw new Exception("Test");

            // Act
            var result = await AsyncProcessor.RunAsync(Action);

            // Assert
            Assert.Equal(default, result);
        }

        [Fact]
        public async Task Run_ShouldThrowOperationCanceledExceptionOnCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            async Task Action(CancellationToken ct) => await Task.Delay(1000, ct);
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => AsyncProcessor.RunAsync(Action, null, cts.Token));
        }

        [Fact]
        public async Task RunWithRetry_ShouldExecuteActionSuccessfully()
        {
            // Arrange
            bool executed = false;
            async Task Action(CancellationToken _) => executed = true;

            // Act
            await AsyncProcessor.RunWithRetryAsync(TimeSpan.Zero, Action);

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public async Task RunWithRetry_ShouldRetryOnException()
        {
            // Arrange
            int attemptCount = 0;
            async Task Action(CancellationToken _)
            {
                attemptCount++;
                if (attemptCount < 3) throw new Exception("Test");
            }

            // Act
            await AsyncProcessor.RunWithRetryAsync(TimeSpan.Zero, Action, retries: 3);

            // Assert
            Assert.Equal(3, attemptCount);
        }

        [Fact]
        public async Task RunWithRetry_ShouldInvokeOnErrorOnException()
        {
            // Arrange
            var onErrorMock = new Mock<Action<Exception>>();
            async Task Action(CancellationToken _) => throw new Exception("Test");

            // Act
            await AsyncProcessor.RunWithRetryAsync(TimeSpan.Zero, Action, onErrorMock.Object, retries: 2);

            // Assert
            onErrorMock.Verify(e => e(It.IsAny<Exception>()), Times.Exactly(2));
        }

        [Fact]
        public async Task RunWithRetry_ShouldReturnResultOnSuccess()
        {
            // Arrange
            async Task<int> Action(CancellationToken _) => 42;

            // Act
            var result = await AsyncProcessor.RunWithRetryAsync(TimeSpan.Zero, Action);

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task RunWithRetry_ShouldReturnDefaultOnAllFailures()
        {
            // Arrange
            async Task<int> Action(CancellationToken _) => throw new Exception("Test");

            // Act
            var result = await AsyncProcessor.RunWithRetryAsync(TimeSpan.Zero, Action, retries: 2);

            // Assert
            Assert.Equal(default, result);
        }

        [Fact]
        public async Task RunWithRetry_ShouldThrowOperationCanceledExceptionOnCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            async Task Action(CancellationToken ct) => await Task.Delay(1000, ct);
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => AsyncProcessor.RunWithRetryAsync(TimeSpan.Zero, Action, null, 3, cts.Token));
        }

        [Fact]
        public async Task RunWithTimeout_ShouldExecuteActionSuccessfully()
        {
            // Arrange
            var executed = false;
            async Task TestAction(CancellationToken _)
            {
                executed = true;
                await Task.CompletedTask;
            }

            // Act
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            try
            {
                await AsyncProcessor.RunWithTimeoutAsync(TimeSpan.FromMilliseconds(200), TestAction, null, cts.Token);
            }
            catch { }

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public async Task RunWithTimeout_ShouldCallOnErrorOnException()
        {
            // Arrange
            var exceptionThrown = false;
            async Task TestAction(CancellationToken _)
            {
                throw new InvalidOperationException("Test Exception");
            }

            void OnError(Exception ex)
            {
                exceptionThrown = ex is InvalidOperationException;
            }

            // Act
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await AsyncProcessor.RunWithTimeoutAsync(TimeSpan.FromMilliseconds(200), TestAction, OnError, cts.Token);

            // Assert
            Assert.True(exceptionThrown);
        }

        [Fact]
        public async Task RunWithTimeout_ShouldRespectCancellationToken()
        {
            // Arrange
            var executed = false;
            async Task TestAction(CancellationToken _)
            {
                executed = true;
                await Task.CompletedTask;
            }

            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                AsyncProcessor.RunWithTimeoutAsync(TimeSpan.FromMilliseconds(200), TestAction, null, cts.Token));

            Assert.False(executed);
        }

        [Fact]
        public async Task RunParallel_ShouldProcessAllItems()
        {
            // Arrange
            var items = Enumerable.Range(1, 10).ToList();
            var processedItems = new HashSet<int>();

            async Task ProcessItem(int item, CancellationToken _)
            {
                await Task.Delay(100);
                lock (processedItems)
                {
                    processedItems.Add(item);
                }
            }

            // Act
            await AsyncProcessor.RunParallelAsync(items, ProcessItem);

            // Assert
            Assert.Equal(items.Count, processedItems.Count);
            Assert.All(items, item => Assert.Contains(item, processedItems));
        }

        [Fact]
        public async Task RunParallel_ShouldLimitConcurrency()
        {
            // Arrange
            var items = Enumerable.Range(1, 10).ToList();
            var maxConcurrentTasks = 0;
            var currentConcurrentTasks = 0;

            async Task ProcessItem(int item, CancellationToken _)
            {
                Interlocked.Increment(ref currentConcurrentTasks);
                maxConcurrentTasks = Math.Max(maxConcurrentTasks, currentConcurrentTasks);

                await Task.Delay(200);

                Interlocked.Decrement(ref currentConcurrentTasks);
            }

            int maxDegreeOfParallelism = 3;

            // Act
            await AsyncProcessor.RunParallelAsync(items, ProcessItem, maxDegreeOfParallelism: maxDegreeOfParallelism);

            // Assert
            Assert.InRange(maxConcurrentTasks, 1, maxDegreeOfParallelism);
        }

        [Fact]
        public async Task RunParallel_ShouldCallOnError_WhenExceptionOccurs()
        {
            // Arrange
            var items = Enumerable.Range(1, 5).ToList();
            var exceptionThrown = false;

            async Task ProcessItem(int item, CancellationToken _)
            {
                if (item == 3)
                    throw new InvalidOperationException("Test exception");

                await Task.Delay(50);
            }

            void OnError(Exception ex)
            {
                if (ex is InvalidOperationException)
                    exceptionThrown = true;
            }

            // Act
            await AsyncProcessor.RunParallelAsync(items, ProcessItem, OnError);

            // Assert
            Assert.True(exceptionThrown);
        }

        [Fact]
        public async Task RunParallel_ShouldRespectCancellationToken()
        {
            // Arrange
            var items = Enumerable.Range(1, 5).ToList();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            async Task ProcessItem(int item, CancellationToken token)
            {
                await Task.Delay(50, token);
            }

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                AsyncProcessor.RunParallelAsync(items, ProcessItem, cancellationToken: cts.Token));
        }

        [Fact]
        public async Task FireAndWait_ShouldExecuteActionAndWaitForTask()
        {
            // Arrange
            var taskCompleted = false;
            var actionExecuted = false;

            async Task TaskToFire(CancellationToken _)
            {
                await Task.Delay(200);
                taskCompleted = true;
            }

            async Task Action(CancellationToken _)
            {
                await Task.Delay(100);
                actionExecuted = true;
            }

            // Act
            await AsyncProcessor.FireAndWaitAsync(TaskToFire, Action);

            // Assert
            Assert.True(taskCompleted);
            Assert.True(actionExecuted);
        }

        [Fact]
        public async Task FireAndWait_ShouldCallOnError_WhenExceptionOccurs()
        {
            // Arrange
            var exceptionThrown = false;

            async Task TaskToFire(CancellationToken _)
            {
                await Task.Delay(100);
                throw new InvalidOperationException("Test exception");
            }

            async Task Action(CancellationToken _) => await Task.Delay(50);

            void OnError(Exception ex)
            {
                if (ex is InvalidOperationException)
                    exceptionThrown = true;
            }

            // Act
            await AsyncProcessor.FireAndWaitAsync(TaskToFire, Action, OnError);

            // Assert
            Assert.True(exceptionThrown);
        }

        [Fact]
        public async Task FireAndWaitAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            async Task TaskToFire(CancellationToken token) => await Task.Delay(50, token);
            async Task Action(CancellationToken token) => await Task.Delay(50, token);

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                AsyncProcessor.FireAndWaitAsync(TaskToFire, Action, cancellationToken: cts.Token));
        }

        [Fact]
        public async Task FireAndWaitAsyncT_ShouldReturnResultOfTaskToFire()
        {
            // Arrange
            async Task<int> TaskToFire(CancellationToken _)
            {
                await Task.Delay(200);
                return 42;
            }

            async Task<int> Action(CancellationToken _)
            {
                await Task.Delay(100);
                return 10;
            }

            // Act
            var result = await AsyncProcessor.FireAndWaitAsync(TaskToFire, Action);

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task FireAndWaitAsyncT_ShouldReturnDefaultValue_WhenExceptionOccurs()
        {
            // Arrange
            async Task<int> TaskToFire(CancellationToken _)
            {
                await Task.Delay(100);
                throw new InvalidOperationException("Test exception");
            }

            async Task<int> Action(CancellationToken _)
            {
                await Task.Delay(50);
                return default;
            }

            void OnError(Exception ex) { }

            // Act
            var result = await AsyncProcessor.FireAndWaitAsync(TaskToFire, Action, OnError);

            // Assert
            Assert.Equal(default, result);
        }
    }
}
