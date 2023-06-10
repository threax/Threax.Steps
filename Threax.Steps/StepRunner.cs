using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;

namespace Threax.Steps;

public interface IStepRunner
{
    Task RunAsync(Delegate del);
    Task RunAsync(MethodInfo methodInfo);
    Task RunAsync(Type stepType);
    Task RunAsync<T>();
}

public class StepRunner : IStepRunner
{
    private readonly IServiceProvider serviceProvider;

    public StepRunner(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public Task RunAsync(Delegate del)
    {
        return RunAsync(del.GetMethodInfo());
    }

    public Task RunAsync<T>()
    {
        return RunAsync(typeof(T));
    }

    public Task RunAsync(Type stepType)
    {
        var runFunc = stepType.GetMethod("Run");
        if (runFunc == null)
        {
            throw new InvalidOperationException($"Cannot find a 'Run' function on step '{stepType.FullName}'");
        }
        return RunAsync(runFunc);
    }

    public async Task RunAsync(MethodInfo methodInfo)
    {
        using var scope = serviceProvider.CreateScope();
        var stepType = methodInfo.DeclaringType;
        var stepName = stepType.Name;
        var sw = new Stopwatch();
        var loggerType = typeof(ILogger<>).MakeGenericType(stepType);
        var logger = scope.ServiceProvider.GetRequiredService(loggerType) as ILogger;
        var scopeType = scope.ServiceProvider.GetRequiredService<ICurrentScopeType>() as CurrentScopeType;
        scopeType.Current = stepType;

        sw.Start();
        logger.LogInformation("-----------------------------------------------------");
        logger.LogInformation("-");
        logger.LogInformation($"- Running Step '{stepName}'");
        logger.LogInformation("-");
        logger.LogInformation("-----------------------------------------------------");

        var stepArgs = methodInfo.GetParameters()
            .Select(i => scope.ServiceProvider.GetRequiredService(i.ParameterType))
            .ToArray();

        Task task = null;
        Object instance = null;

        if (!methodInfo.IsStatic)
        {
            var declaringType = methodInfo.DeclaringType;
            var constructor = declaringType.GetConstructors().First();
            var constructorArgs = constructor.GetParameters()
                .Select(i => scope.ServiceProvider.GetRequiredService(i.ParameterType))
                .ToArray();
            if (constructorArgs.Count() > 0)
            {
                instance = Activator.CreateInstance(declaringType, constructorArgs);
            }
            else
            {
                instance = Activator.CreateInstance(declaringType);
            }
        }

        try
        {
            task = methodInfo.Invoke(instance, stepArgs) as Task;

            if (task != null)
            {
                await task;
            }

            sw.Stop();
            logger.LogInformation("-----------------------------------------------------");
            logger.LogInformation("-");
            logger.LogInformation($"- Step '{stepName}' completed in '{sw.Elapsed}'");
            logger.LogInformation("-");
            logger.LogInformation("-----------------------------------------------------");
        }
        catch (StepRunnerHandledException ex)
        {
            sw.Stop();
            logger.LogError("-----------------------------------------------------");
            logger.LogError("-");
            logger.LogError($"- Child Step of Step '{stepName}' failed in '{sw.Elapsed}'");
            logger.LogError("-");
            logger.LogError("-----------------------------------------------------");
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError("-----------------------------------------------------");
            logger.LogError("-");
            logger.LogError("- Root Failure:");
            logger.LogError($"- Step '{stepName}' failed in '{sw.Elapsed}'");
            foreach (var line in ex.ToString().Split('\n'))
            {
                logger.LogError($"- {line.Replace("\r", "")}");
            }
            logger.LogError("-");
            logger.LogError("-----------------------------------------------------");
            throw new StepRunnerHandledException(stepName, $"Step '{stepName}' failed.", ex);
        }
    }
}

public interface IStepScoped<T>
{
    T Instance { get; }
}

class StepScoped<T> : IStepScoped<T>
{
    public T Instance { get; set; }
}

public interface ICurrentScopeType
{
    Type Current { get; }
}

class CurrentScopeType : ICurrentScopeType
{
    public Type Current { get; set; }
}

class StepRunnerHandledException : Exception
{
    public StepRunnerHandledException(String step, String message, Exception innerException)
        : base(message, innerException)
    {
        this.Step = step;
    }

    public String Step { get; init; }
}