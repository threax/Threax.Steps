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
Then this can be run either by invoking it dynamically or using the StepRunner
```
await StepRunner.RunAsync<Example>();
```