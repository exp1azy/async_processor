namespace AsyncProcessor.Tests
{
    public class AsyncUtilsTests
    {
        public AsyncUtilsTests()
        {
            AsyncUtils.ResetTasks();
            AsyncUtils.ResetSemaphores();
        }

        [Fact]
        public async Task Throttle_ShouldExecuteAction()
        {
            // Arrange
            var executed = false;

            async Task Action(CancellationToken _)
            {
                await Task.Delay(50);
                executed = true;
            }

            // Act
            await AsyncUtils.ThrottleAsync("test_key", TimeSpan.FromMilliseconds(200), Action);

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public async Task Throttle_ShouldThrottleSubsequentCalls()
        {
            // Arrange
            var executionCount = 0;

            async Task Action(CancellationToken _)
            {
                await Task.Delay(50);
                Interlocked.Increment(ref executionCount);
            }

            // Act
            var firstCall = AsyncUtils.ThrottleAsync("test_key", TimeSpan.FromMilliseconds(200), Action);
            var secondCall = AsyncUtils.ThrottleAsync("test_key", TimeSpan.FromMilliseconds(200), Action);

            await Task.WhenAll(firstCall, secondCall);

            // Assert
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public async Task Throttle_ShouldAllowExecutionAfterInterval()
        {
            // Arrange
            var executionCount = 0;

            async Task Action(CancellationToken _)
            {
                await Task.Delay(50);
                Interlocked.Increment(ref executionCount);
            }

            // Act
            await AsyncUtils.ThrottleAsync("test_key", TimeSpan.FromMilliseconds(100), Action);
            await Task.Delay(150);
            await AsyncUtils.ThrottleAsync("test_key", TimeSpan.FromMilliseconds(100), Action);

            // Assert
            Assert.Equal(2, executionCount);
        }

        [Fact]
        public async Task Throttle_ShouldCallOnError_WhenExceptionOccurs()
        {
            // Arrange
            var exceptionThrown = false;

            async Task Action(CancellationToken _)
            {
                await Task.Delay(50);
                throw new TestException("Test exception");
            }

            void OnError(Exception ex)
            {
                if (ex is TestException)
                    exceptionThrown = true;
            }

            // Act
            await AsyncUtils.ThrottleAsync("test_key", TimeSpan.FromMilliseconds(200), Action, OnError);

            // Assert
            Assert.True(exceptionThrown);
        }

        [Fact]
        public async Task Throttle_ShouldReturnResultOfFunction()
        {
            // Arrange
            async Task<int> Action(CancellationToken _)
            {
                await Task.Delay(50);
                return 42;
            }

            // Act
            var result = await AsyncUtils.ThrottleAsync("test_key", TimeSpan.FromMilliseconds(200), Action);

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task Throttle_ShouldReturnDefault_WhenExceptionOccurs()
        {
            // Arrange
            async Task<int> Action(CancellationToken _)
            {
                await Task.Delay(50);
                throw new TestException("Test exception");
            }

            void OnError(Exception ex) { }

            // Act
            var result = await AsyncUtils.ThrottleAsync("test_key", TimeSpan.FromMilliseconds(200), Action, OnError);

            // Assert
            Assert.Equal(default, result);
        }

        [Fact]
        public async Task RunOnce_ShouldExecuteActionOncePerKey()
        {
            // Arrange
            int executionCount = 0;

            Func<CancellationToken, Task> action = async _ =>
            {
                Interlocked.Increment(ref executionCount);
                await Task.Delay(100);
            };

            // Act
            var task1 = AsyncUtils.RunOnceAsync("test-key", action);
            var task2 = AsyncUtils.RunOnceAsync("test-key", action);
            await Task.WhenAll(task1, task2);

            // Assert
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public async Task RunOnce_ShouldExecuteMultipleActionsForDifferentKeys()
        {
            // Arrange
            int executionCount1 = 0, executionCount2 = 0;
            async Task action1(CancellationToken _)
            {
                Interlocked.Increment(ref executionCount1);
                await Task.Delay(100);
            }
            async Task action2(CancellationToken _)
            {
                Interlocked.Increment(ref executionCount2);
                await Task.Delay(100);
            }

            // Act
            Task task1 = AsyncUtils.RunOnceAsync("key1", action1);
            Task task2 = AsyncUtils.RunOnceAsync("key2", action2);
            await Task.WhenAll(task1, task2);

            // Assert
            Assert.Equal(1, executionCount1);
            Assert.Equal(1, executionCount2);
        }

        [Fact]
        public async Task RunOnce_ShouldNotSuppressExceptionsIfNoErrorHandler()
        {
            // Arrange
            static Task action(CancellationToken _) => throw new TestException("Test exception");

            // Act & Assert
            await Assert.ThrowsAsync<TestException>(() => AsyncUtils.RunOnceAsync("test-key", action));
        }

        [Fact]
        public async Task RunOnce_ShouldInvokeOnErrorHandlerWhenExceptionOccurs()
        {
            // Arrange
            Exception? capturedException = null;
            Task action(CancellationToken _) => throw new TestException("Test exception");
            void onError(Exception ex) => capturedException = ex;

            // Act
            await AsyncUtils.RunOnceAsync("test-key", action, onError);

            // Assert
            Assert.NotNull(capturedException);
            Assert.IsType<TestException>(capturedException);
            Assert.Equal("Test exception", capturedException.Message);
        }

        [Fact]
        public async Task RunOnce_ShouldThrowOperationCanceledException_WhenTaskIsCancelled()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();
            async Task action(CancellationToken ct)
            {
                await Task.Delay(100, ct);
            }

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => AsyncUtils.RunOnceAsync("test-key", action, cancellationToken: cts.Token));
        }
    }
}
