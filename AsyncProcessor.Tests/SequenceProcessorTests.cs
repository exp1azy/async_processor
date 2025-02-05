namespace AsyncProcessor.Tests
{
    public class SequenceProcessorTests
    {
        [Fact]
        public async Task RunInSequence_ShouldExecuteTasksInOrder()
        {
            // Arrange
            var executionOrder = new List<int>();
            async Task Task1(CancellationToken _) { executionOrder.Add(1); await Task.Delay(100); }
            async Task Task2(CancellationToken _) { executionOrder.Add(2); await Task.Delay(100); }

            // Act
            await Task.WhenAll(
                SequenceProcessor.RunInSequenceAsync(Task1),
                SequenceProcessor.RunInSequenceAsync(Task2)
            );

            // Assert
            Assert.Equal([1, 2], executionOrder);
        }

        [Fact]
        public async Task RunInSequence_ShouldHandleExceptions()
        {
            // Arrange
            var exceptionHandled = false;
            async Task ThrowingTask(CancellationToken _) { throw new TestException("Test exception"); }
            void OnError(Exception ex) { exceptionHandled = ex is TestException; }

            // Act
            await SequenceProcessor.RunInSequenceAsync(ThrowingTask, OnError);

            // Assert
            Assert.True(exceptionHandled);
        }

        [Fact]
        public async Task RunInSequence_ShouldNotSkipTasksAfterException()
        {
            // Arrange
            var executionOrder = new List<int>();

            async Task ThrowingTask(CancellationToken _) { executionOrder.Add(1); throw new TestException("Test exception"); }
            async Task NormalTask(CancellationToken _) { executionOrder.Add(2); }

            // Act
            await SequenceProcessor.RunInSequenceAsync(ThrowingTask, _ => { });
            await SequenceProcessor.RunInSequenceAsync(NormalTask);

            // Assert
            Assert.Equal([1, 2], executionOrder);
        }

        [Fact]
        public async Task RunInSequence_ShouldRespectCancellation()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            async Task CanceledTask(CancellationToken token) { await Task.Delay(100, token); }

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(
                () => SequenceProcessor.RunInSequenceAsync(CanceledTask, cancellationToken: cts.Token)
            );
        }

        [Fact]
        public async Task RunInSequence_ShouldProcessMultipleTasksCorrectly()
        {
            // Arrange
            var executionOrder = new List<int>();

            async Task Task1(CancellationToken _) { executionOrder.Add(1); await Task.Delay(50); }
            async Task Task2(CancellationToken _) { executionOrder.Add(2); await Task.Delay(50); }
            async Task Task3(CancellationToken _) { executionOrder.Add(3); await Task.Delay(50); }

            // Act
            await Task.WhenAll(
                SequenceProcessor.RunInSequenceAsync(Task1),
                SequenceProcessor.RunInSequenceAsync(Task2),
                SequenceProcessor.RunInSequenceAsync(Task3)
            );

            // Assert
            Assert.Equal([1, 2, 3], executionOrder);
        }
    }
}
