# AsyncProcessor

**AsyncProcessor** is a .NET library that provides a comprehensive suite of asynchronous utilities designed to simplify and enhance the control over task execution. 
It includes robust implementations for 
- **task throttling** to limit how frequently an asynchronous action can execute using a configurable time interval,
- **one-time execution** to ensure that a specific asynchronous action is executed only once per unique key,
- **sequential processing** to process tasks one after another and guarantee order of execution,
- **concurrency control** to manage exclusive access and limit parallel execution with built-in locking mechanisms,
- **and many more**.

With full support for cancellation tokens and error handling via customizable callbacks, **AsyncProcessor** leverages .NET primitives like `SemaphoreSlim`, `ConcurrentDictionary`, and `ConcurrentQueue` to provide a safe, efficient, and flexible framework for managing complex asynchronous workflows. 
This library is ideal for developers seeking to build reliable and responsive applications by simplifying asynchronous task management.

## Documentation

The library contains several classes, each of which solves a specific problem. There are 5 classes in total:
1. `AsyncProcessor` - Provides utility methods for executing asynchronous tasks with error handling, retries, timeouts, and parallel execution.
2. `AsyncUtils` - Provides a set of utility methods for handling asynchronous operations.
3. `ConcurrencyProcessor` - Provides methods to process tasks with concurrency control using locks and semaphores.
4. `QueueProcessor` - Provides functionality for processing asynchronous tasks in a queue with limited concurrency.
5. `SequenceProcessor` - Provides methods to process tasks in sequence, ensuring that tasks are executed one after another.

### AsyncProcessor

`AsyncProcessor` contains the following methods:
1. `RunAsync()` - Executes an asynchronous task and handles exceptions using the provided error handler.
2. `RunWithRetryAsync()` - Executes an asynchronous task with retry logic, handling exceptions using the provided error handler.
3. `RunWithTimeoutAsync()` - Executes an asynchronous task with a timeout constraint.
4. `RunParallelAsync()` - Executes a set of asynchronous tasks in parallel with a limited degree of concurrency.
5. `FireAndWaitAsync()` - Starts an asynchronous task, executes a subsequent action, then waits for the initial task to complete.

### AsyncUtils

`AsyncUtils` contains the following methods:
1. `ThrottleAsync()` - Ensures that the provided asynchronous action does not execute more frequently than the specified interval.
2. `RunOnceAsync()` - Ensures that the specified asynchronous action executes only once per key.
3. `ResetTasks()` - Resets the stored tasks for one-time execution, allowing the same keys to be reused for new executions.
4. `ResetSemaphores()` - Resets the stored semaphores used for throttling, removing all existing rate-limiting locks.

### ConcurrencyProcessor

`ConcurrencyProcessor` contains the following methods:
1. `LockAsync()` - Executes a task with exclusive access, ensuring only one task runs at a time.
2. `SemaphoreAsync()` - Executes a task with a semaphore, limiting concurrent executions to a specified maximum number.

### QueueProcessor
`QueueProcessor` contains a `EnqueueAsync()` method that enqueues an asynchronous task for execution while ensuring controlled concurrency.

### SequenceProcessor
`SequenceProcessor` contains a `RunInSequenceAsync()` method that enqueues and executes a task in sequence, ensuring that tasks are run one after the other.

## Usage

### AsyncProcessor
`AsyncProcessor` is a static class, so methods in it are called without creating an instance of the class:
```csharp
bool executed = false;
async Task Action(CancellationToken _) => executed = true;

await AsyncProcessor.RunAsync(Action);

Console.WriteLine(executed);
```

### AsyncUtils
`AsyncUtils` is also a static class, so methods are called without creating an instance:
```csharp
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

Task task1 = AsyncUtils.RunOnceAsync("key1", action1);
Task task2 = AsyncUtils.RunOnceAsync("key2", action2);
await Task.WhenAll(task1, task2);

Console.WriteLine(1 == executionCount1);
Console.WriteLine(1 == executionCount2);
```

### ConcurrencyProcessor
`ConcurrencyProcessor` is also a static class:
```csharp
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
Console.WriteLine(3 == counter);
```

### QueueProcessor
To use the `QueueProcessor` functionality, you need to initialize an instance of the class by passing the `maxConcurrency` parameter to the constructor, which means the maximum number of tasks that can be run simultaneously.
```csharp
var queueProcessor = new QueueProcessor(2);
var taskCompleted = new TaskCompletionSource<bool>();

await queueProcessor.EnqueueAsync(async _ =>
{
    await Task.Delay(100);
    taskCompleted.SetResult(true);
});

Console.WriteLine(await taskCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1)));
```

### SequenceProcessor
`SequenceProcessor` is also a static class, so methods are called without creating an instance:
```csharp
var executionOrder = new List<int>();
async Task Task1(CancellationToken _) { executionOrder.Add(1); await Task.Delay(100); }
async Task Task2(CancellationToken _) { executionOrder.Add(2); await Task.Delay(100); }

await Task.WhenAll(
    SequenceProcessor.RunInSequenceAsync(Task1),
    SequenceProcessor.RunInSequenceAsync(Task2)
);
```

## Installation

To install the package, use the following command:
```bash
dotnet add package AsyncProcessor
```
or via NuGet Package Manager:
```bash
Install-Package AsyncProcessor
```
