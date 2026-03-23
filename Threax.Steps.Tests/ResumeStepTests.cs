using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using System;
using System.Threading.Tasks;

namespace Threax.Steps.Tests;

[TestClass]
public class ResumeStepTests
{
    [TestMethod]
    public async Task ResumeStepTest()
    {
        var services = new ServiceCollection();
        services.AddTestStepRunner();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var stepThread = scope.ServiceProvider.GetRequiredService<IStepThread>();
        var ex = await Assert.ThrowsExactlyAsync<AggregateException>(() => stepThread.Run(typeof(FiringStep)));
        Assert.IsExactInstanceOfType<StepRunnerResumeWithStepException>(ex.InnerException, $"The inner exception is not a 'StepRunnerResumeWithStepException'");
    }

    record ResumeStep
    (
        ILogger<ResumeStep> Logger,
        IStepRunner StepRunner
    )
    {
        public async Task Run()
        {
            
        }
    }

    record FiringStep
    (
        ILogger<ResumeStep> Logger,
        IStepRunner StepRunner
    )
    {
        public async Task Run()
        {
            StepRunner.ResumeWithStep<ResumeStep>();
        }
    }
}
