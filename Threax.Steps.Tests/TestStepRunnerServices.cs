using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Threax.Extensions.DependencyInjection;

namespace Threax.Steps.Tests;

public static class TestStepRunnerServices
{
    public static void AddTestStepRunner(this IServiceCollection services)
    {
        services.AddLogging(o =>
        {
            o.AddConsole().AddSimpleConsole(co =>
            {
                co.IncludeScopes = false;
                co.SingleLine = true;
            });
        });
        services.AddThreaxSteps("Threax.Steps.Tests")
            .AddThreaxStepScopedLog();
    }
}
