using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Threax.Extensions.DependencyInjection;

namespace Threax.Steps.Tests;

[TestClass]
public class ResumeMainThreadTests
{
    [TestMethod]
    public async Task TestRun()
    {
        var services = new ServiceCollection();
        services.AddTestStepRunner();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var stepThread = scope.ServiceProvider.GetRequiredService<IStepThread>();
        await stepThread.Run(typeof(BgStep));
    }

    record BgStep
    (
        ILogger<BgStep> Logger
    )
    {
        public async Task Run()
        {
            for (int i = 0; i < 100; i++)
            {
                var beforeAwaitThread = Thread.CurrentThread;
                await Task.Run(() => Logger.LogInformation("In bg thread"));
                var afterAwaitThread = Thread.CurrentThread;
                Assert.AreEqual(beforeAwaitThread, afterAwaitThread, $"The resuming thread {afterAwaitThread} is not the same as the before await thread {beforeAwaitThread}");
            }
        }
    }
}
