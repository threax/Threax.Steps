# Threax.Steps
A library to create applications that run one or more steps from input.

## Usage
Add it to a ServiceCollection like the following
```
services.AddThreaxSteps("YourApp.Steps", Assembly.GetExecutingAssembly())
    .AddThreaxStepScopedLog();
```

This will add the IStepRunner, which you can get from DI.

```
var stepType = Assembly.GetExecutingAssembly().GetTypes().Where(i => i.Name == command).FirstOrDefault()
               ?? typeof(Help);

var runFunc = stepType.GetMethod("Run");
if (runFunc == null)
{
    throw new InvalidOperationException($"Cannot find a 'Run' function on step '{stepType.FullName}'");
}

var stepRunner = serviceProvider.GetRequiredService<IStepRunner>();
await stepRunner.RunAsync(runFunc);
```

## Make a Step
To make a step make a record or class like the following:
```
record Example
(
    IInjectedService
)
{
    public void Run()
    {
        ...Do Stuff
    }
}
```


## Invoke The Step Thread with the Startup Step
This method will run the steps using an async model that works like javascript. All awaits will resume on the main thread instead of random thread pool threads (default c# behavior) when used this way. If you want background processing use Task.Run. Awaiting the task returned will give up the main thread to the next batch of work. When the background job completes it must wait until the main thread is given up by the current work in order to resume.
```
var stepThread = scope.ServiceProvider.GetRequiredService<IStepThread>();
await stepThread.Run(typeof(Example));
```

When running this way you can background work with Task.Run.

```
public async Task Run()
{
    //main thread work

    await Task.Run(() =>
    {
        //Do bg stuff, be careful with what goes on here since it is in another thread
    });

    //main thread work, but it can happen in any order with other work done in parallel as shown below
}
```

However, if you just run steps like this in a linear fashion you won't actually get multiple things happening, since you will be running a background task, but you are just awaiting the result. To run multiple steps at the same time use Task.WhenAll and call each step you want to run as an argument. When you do this the code runs in order until a background thread is run with work. Calling await on the backgrounded task will allow the next step to start running. That step will continue to run until it completes or gives up its time slice to a background thread. Once that happens the original step might resume or it will continue on to the next one. That depends on how the scheduler resolves it.
```
public async Task Run()
{
    //main thread work

    //Anything run inside the Task.WhenAll function call can happen in any order after an async call gives up the thread. If no async calls are made or they aren't run in a bg thread with Task.Run or through some other mechanism you won't get any parallel work even writing the code this way. There is not automatically a background thread with async in c#.

    await Task.WhenAll
    (
        StepRunner.RunAsync<Step1>(),
        StepRunner.RunAsync<Step2>(),
        StepRunner.RunAsync<Step3>(),
        StepRunner.RunAsync<Step4>()
    );

    //main thread work. All work from the Task.WhenAll will be complete by here
}
```

### Using a Custom Sync Context

If you don't want to use the threading model described above you can also use the StepRunner directly. This will not modify the synchronization context like the StepThread does.

When running steps this way nothing in the above section applies. You will get default c# behavior for a null SynchronizationContext.
```
var stepThread = scope.ServiceProvider.GetRequiredService<IStepRunner>();
await StepRunner.RunAsync<Example>();
```

## References
 * [Parallel Computing - It's All About the SynchronizationContext](https://learn.microsoft.com/en-us/archive/msdn-magazine/2011/february/msdn-magazine-parallel-computing-it-s-all-about-the-synchronizationcontext)
 * [Await, SynchronizationContext, and Console Apps](https://devblogs.microsoft.com/pfxteam/await-synchronizationcontext-and-console-apps/) This article is also reproduced in this repo as AwaitSyncContextAndConsoleApps.md.