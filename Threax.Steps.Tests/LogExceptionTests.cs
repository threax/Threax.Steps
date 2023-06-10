using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using System;
using System.Threading.Tasks;

namespace Threax.Steps.Tests;

[TestClass]
public class LogExceptionTests
{
    [TestMethod]
    public async Task BgStepTest()
    {
        var services = new ServiceCollection();
        services.AddTestStepRunner();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var stepThread = scope.ServiceProvider.GetRequiredService<IStepThread>();
        await Assert.ThrowsExceptionAsync<AggregateException>(() => stepThread.Run(typeof(BgStep)));
    }

    record BgStep
    (
        ILogger<BgStep> Logger
    )
    {
        public async Task Run()
        {
            await Task.Run(() => Logger.LogInformation("Background thread"));
            throw new Exception("This is the message.");
        }
    }

    [TestMethod]
    public async Task VoidStepTest()
    {
        var services = new ServiceCollection();
        services.AddTestStepRunner();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var stepThread = scope.ServiceProvider.GetRequiredService<IStepThread>();
        await Assert.ThrowsExceptionAsync<AggregateException>(() => stepThread.Run(typeof(VoidStep)));
    }

    record VoidStep
    (
        ILogger<BgStep> Logger
    )
    {
        public void Run()
        {
            throw new Exception("This is the message.");
        }
    }

    [TestMethod]
    public async Task MidStepTest()
    {
        var services = new ServiceCollection();
        services.AddTestStepRunner();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var stepThread = scope.ServiceProvider.GetRequiredService<IStepThread>();
        await Assert.ThrowsExceptionAsync<AggregateException>(() => stepThread.Run(typeof(MidStep)));
    }

    record MidStep
    (
        IStepRunner StepRunner
    )
    {
        public async Task Run()
        {
            await StepRunner.RunAsync<VoidStep>();
        }
    }

    [TestMethod]
    public async Task TopStepTest()
    {
        var services = new ServiceCollection();
        services.AddTestStepRunner();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var stepThread = scope.ServiceProvider.GetRequiredService<IStepThread>();
        await Assert.ThrowsExceptionAsync<AggregateException>(() => stepThread.Run(typeof(TopStep)));
    }

    record TopStep
    (
        IStepRunner StepRunner
    )
    {
        public async Task Run()
        {
            await StepRunner.RunAsync<MidStep>();
        }
    }

    [TestMethod]
    public async Task TaskWhenAllSingleStepFailure()
    {
        var services = new ServiceCollection();
        services.AddTestStepRunner();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var stepThread = scope.ServiceProvider.GetRequiredService<IStepThread>();
        await Assert.ThrowsExceptionAsync<AggregateException>(() => stepThread.Run(typeof(PassAndFailStep)));
    }

    record SuccessStep
    (
        ILogger<SuccessStep> Logger
    )
    {
        public async Task Run()
        {
            Logger.LogInformation("Waiting...");
            await Task.Delay(250);
            Logger.LogInformation("Success");
        }
    }

    record FailStep
    (
        ILogger<FailStep> Logger
    )
    {
        public void Run()
        {
            throw new InvalidOperationException();
        }
    }

    record PassAndFailStep
    (
        IStepRunner StepRunner
    )
    {
        public async Task Run()
        {
            await Task.WhenAll
            (
                StepRunner.RunAsync<FailStep>(),
                StepRunner.RunAsync<SuccessStep>()
            );
        }
    }

    [TestMethod]
    public async Task TaskWhenAllMultiStepFailure()
    {
        var services = new ServiceCollection();
        services.AddTestStepRunner();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var stepThread = scope.ServiceProvider.GetRequiredService<IStepThread>();
        await Assert.ThrowsExceptionAsync<AggregateException>(() => stepThread.Run(typeof(FailAndFailStep)));
    }

    record SlowFailStep
    (
        ILogger<FailStep> Logger
    )
    {
        public async Task Run()
        {
            Logger.LogInformation("Waiting...");
            await Task.Delay(250);
            throw new InvalidOperationException();
        }
    }

    record FailAndFailStep
    (
        IStepRunner StepRunner
    )
    {
        public async Task Run()
        {
            await Task.WhenAll
            (
                StepRunner.RunAsync<SlowFailStep>(),
                StepRunner.RunAsync<FailStep>()
            );
        }
    }

    [TestMethod]
    public async Task FailMultiInChildTest()
    {
        var services = new ServiceCollection();
        services.AddTestStepRunner();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var stepThread = scope.ServiceProvider.GetRequiredService<IStepThread>();
        var exception = await Assert.ThrowsExceptionAsync<AggregateException>(() => stepThread.Run(typeof(FailMultiInChild)));

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LogExceptionTests>>();
        logger.LogInformation("Sample top level exception.");
        foreach (var line in exception.ToString().Split('\n'))
        {
            logger.LogError($"- {line.Replace("\r", "")}");
        }
    }

    record FailMultiInChild
    (
        IStepRunner StepRunner
    )
    {
        public async Task Run()
        {
            await Task.WhenAll
            (
                StepRunner.RunAsync<FailAndFailStep>()
            );
        }
    }

    [TestMethod]
    public async Task FailInBgThreadTest()
    {
        var services = new ServiceCollection();
        services.AddTestStepRunner();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var stepThread = scope.ServiceProvider.GetRequiredService<IStepThread>();
        var exception = await Assert.ThrowsExceptionAsync<AggregateException>(() => stepThread.Run(typeof(FailInBgThread)));

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LogExceptionTests>>();
        logger.LogInformation("Sample top level exception.");
        foreach (var line in exception.ToString().Split('\n'))
        {
            logger.LogError($"- {line.Replace("\r", "")}");
        }
    }

    record FailInBgThread
    (
        IStepRunner StepRunner
    )
    {
        public async Task Run()
        {
            await Task.Run(() =>
            {
                throw new InvalidOperationException("Exception from bg thread");
            });
        }
    }
}
