namespace AsyncProcessor.Tests
{
    public class ConcurrencyProcessorTests
    {
        [Fact]
        public async Task Lock_ShouldExecuteTaskExclusively()
        {
            int counter = 0;

            async Task TaskToRun(CancellationToken ct)
            {
                int temp = counter;
                await Task.Delay(100, ct);
                counter = temp + 1;
            }

            var tasks = new[]
            {
                Task.Run(() => ConcurrencyProcessor.LockAsync(TaskToRun)),
                Task.Run(() => ConcurrencyProcessor.LockAsync(TaskToRun)),
                Task.Run(() => ConcurrencyProcessor.LockAsync(TaskToRun))
            };

            await Task.WhenAll(tasks);
            Assert.Equal(3, counter);
        }

        [Fact]
        public async Task Lock_ShouldHandleException()
        {
            Exception? capturedException = null;

            async Task FaultyTask(CancellationToken ct) => throw new TestException("Test Exception");

            await ConcurrencyProcessor.LockAsync(FaultyTask, ex => capturedException = ex);

            Assert.NotNull(capturedException);
            Assert.IsType<TestException>(capturedException);
            Assert.Equal("Test Exception", capturedException.Message);
        }

        [Fact]
        public async Task LockT_ShouldReturnResult()
        {
            async Task<int> TaskWithResult(CancellationToken ct) => 42;

            int? result = await ConcurrencyProcessor.LockAsync(TaskWithResult);

            Assert.Equal(42, result);
        }

        [Fact]
        public async Task LockT_ShouldHandleException()
        {
            Exception? capturedException = null;

            async Task FaultyTask(CancellationToken ct) => throw new TestException("Test Exception");

            await ConcurrencyProcessor.LockAsync(FaultyTask, ex => capturedException = ex);

            Assert.NotNull(capturedException);
            Assert.IsType<TestException>(capturedException);
        }

        [Fact]
        public async Task Lock_ShouldThrowOnCancellation()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            async Task TaskToCancel(CancellationToken ct)
            {
                await Task.Delay(1000, ct);
            }

            await Assert.ThrowsAsync<TaskCanceledException>(() => 
                ConcurrencyProcessor.LockAsync(TaskToCancel, cancellationToken: cts.Token));
        }

        [Fact]
        public async Task LockT_ShouldThrowOnCancellation()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            async Task<int> TaskToCancel(CancellationToken ct)
            {
                await Task.Delay(1000, ct);
                return 42;
            }

            await Assert.ThrowsAsync<TaskCanceledException>(() => 
                ConcurrencyProcessor.LockAsync(TaskToCancel, cancellationToken: cts.Token));
        }

        [Fact]
        public async Task Lock_ShouldExecuteMultipleCallsSequentially()
        {
            int counter = 0;

            async Task IncrementTask(CancellationToken ct)
            {
                await Task.Delay(50, ct);
                Interlocked.Increment(ref counter);
            }

            await ConcurrencyProcessor.LockAsync(IncrementTask);
            await ConcurrencyProcessor.LockAsync(IncrementTask);
            await ConcurrencyProcessor.LockAsync(IncrementTask);

            Assert.Equal(3, counter);
        }

        [Fact]
        public async Task Semaphore_ShouldLimitConcurrentExecutions()
        {
            int runningTasks = 0;
            int maxConcurrentTasks = 0;

            async Task LimitedTask(CancellationToken ct)
            {
                Interlocked.Increment(ref runningTasks);
                maxConcurrentTasks = Math.Max(maxConcurrentTasks, runningTasks);
                await Task.Delay(100, ct);
                Interlocked.Decrement(ref runningTasks);
            }

            int maxConcurrency = 2;
            var tasks = new[]
            {
                Task.Run(() => ConcurrencyProcessor.SemaphoreAsync(maxConcurrency, LimitedTask)),
                Task.Run(() => ConcurrencyProcessor.SemaphoreAsync(maxConcurrency, LimitedTask)),
                Task.Run(() => ConcurrencyProcessor.SemaphoreAsync(maxConcurrency, LimitedTask))
            };

            await Task.WhenAll(tasks);

            Assert.Equal(maxConcurrency, maxConcurrentTasks);
        }

        [Fact]
        public async Task Semaphore_ShouldHandleException()
        {
            Exception? capturedException = null;

            async Task FaultyTask(CancellationToken ct) => throw new TestException("Test Exception");

            await ConcurrencyProcessor.SemaphoreAsync(2, FaultyTask, ex => capturedException = ex);

            Assert.NotNull(capturedException);
            Assert.IsType<TestException>(capturedException);
            Assert.Equal("Test Exception", capturedException.Message);
        }

        [Fact]
        public async Task SemaphoreT_ShouldReturnResult()
        {
            async Task<int> TaskWithResult(CancellationToken ct) => 42;

            int? result = await ConcurrencyProcessor.SemaphoreAsync(2, TaskWithResult);

            Assert.Equal(42, result);
        }

        [Fact]
        public async Task SemaphoreT_ShouldHandleException()
        {
            Exception? capturedException = null;

            async Task FaultyTask(CancellationToken ct) => throw new TestException("Test Exception");

            await ConcurrencyProcessor.SemaphoreAsync(2, FaultyTask, ex => capturedException = ex);

            Assert.NotNull(capturedException);
            Assert.IsType<TestException>(capturedException);
        }

        [Fact]
        public async Task Semaphore_ShouldThrowOnCancellation()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            async Task TaskToCancel(CancellationToken ct)
            {
                await Task.Delay(1000, ct);
            }

            await Assert.ThrowsAsync<TaskCanceledException>(() => 
                ConcurrencyProcessor.SemaphoreAsync(2, TaskToCancel, cancellationToken: cts.Token));
        }

        [Fact]
        public async Task SemaphoreT_ShouldThrowOnCancellation()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            async Task<int> TaskToCancel(CancellationToken ct)
            {
                await Task.Delay(1000, ct);
                return 42;
            }

            await Assert.ThrowsAsync<TaskCanceledException>(() => 
                ConcurrencyProcessor.SemaphoreAsync(2, TaskToCancel, cancellationToken: cts.Token));
        }

        [Fact]
        public async Task Semaphore_ShouldExecuteMultipleCalls()
        {
            int counter = 0;

            async Task IncrementTask(CancellationToken ct)
            {
                await Task.Delay(50, ct);
                Interlocked.Increment(ref counter);
            }

            await ConcurrencyProcessor.SemaphoreAsync(2, IncrementTask);
            await ConcurrencyProcessor.SemaphoreAsync(2, IncrementTask);
            await ConcurrencyProcessor.SemaphoreAsync(2, IncrementTask);

            Assert.Equal(3, counter);
        }
    }
}
