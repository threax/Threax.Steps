namespace Threax.Steps;

public interface IStepThread
{
    Task Run(Type stepType, String runFuncName = "Run");
}

public class StepThread : IStepThread
{
    private readonly IStepRunner stepRunner;

    public StepThread(IStepRunner stepRunner)
    {
        this.stepRunner = stepRunner;
    }

    public async Task Run(Type stepType, String runFuncName = "Run")
    {
        var runFunc = stepType.GetMethod(runFuncName);
        if (runFunc == null)
        {
            throw new InvalidOperationException($"Cannot find the '{runFuncName}' function on step '{stepType.FullName}'");
        }

        var originalContext = SynchronizationContext.Current;
        try
        {
            var syncCtx = new MainThreadSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(syncCtx);

            var task = stepRunner.RunAsync(runFunc).ContinueWith(a =>
            {
                if (a.IsFaulted)
                {
                    syncCtx.CompleteWithException(a.Exception);
                }
                else
                {
                    syncCtx.Complete();
                }
            }, TaskScheduler.Default);
            syncCtx.RunOnCurrentThread();
            await task;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
    }
}
