using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Threax.Steps;

namespace Threax.Extensions.DependencyInjection;

public static class StepsRegistration
{
    public static IServiceCollection AddThreaxSteps(this IServiceCollection services, String @namespace, Assembly assembly = null)
    {
        if (assembly == null)
        {
            assembly = Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Cannot find entry assembly");
        }

        services.AddSingleton<IStepRunner>(s => new StepRunner(s));
        services.AddSingleton<IHelpInfoFinder>(s => new HelpInfoFinder(@namespace, assembly));
        services.AddScoped<ICurrentScopeType, CurrentScopeType>();

        return services;
    }

    public static IServiceCollection AddThreaxStepScoped<TRef>(this IServiceCollection services, Func<IServiceProvider, Type, TRef> createInstance)
    {
        services.TryAddScoped<IStepScoped<TRef>>(s => new StepScoped<TRef>
        {
            Instance = createInstance(s, s.GetRequiredService<ICurrentScopeType>().Current ?? typeof(StepRunner))
        });

        return services;
    }

    public static IServiceCollection AddThreaxStepScopedLog(this IServiceCollection services)
    {
        services.AddThreaxStepScoped<ILogger>((s, t) => s.GetRequiredService(typeof(ILogger<>).MakeGenericType(t)) as ILogger);

        return services;
    }

    [Obsolete]
    public static void RegisterDeriveType(this IServiceCollection services, Type type)
    {
        TryAddScopedDeriveType(services, type);
    }

    public static void TryAddScopedDeriveType<T>(this IServiceCollection services)
    {
        TryAddScopedDeriveType(services, typeof(T));
    }

    public static void TryAddScopedDeriveType(this IServiceCollection services, Type type)
    {
        services.TryAddScoped(type, s =>
        {
            var constructor = type.GetConstructors().First();
            var constructorArgs = constructor.GetParameters()
            .Select(i => s.GetRequiredService(i.ParameterType))
            .ToArray();
            var config = constructor.Invoke(constructorArgs);

            Derive(s, type, config);

            return config;
        });
    }

    public static void TryAddScopedDeriveType<T>(this IServiceCollection services, Func<IServiceProvider, T> createFunc)
        where T : class
    {
        services.TryAddScoped<T>(s =>
        {
            var config = createFunc.Invoke(s);

            Derive(s, typeof(T), config);

            return config;
        });
    }

    public static void TryAddScopedDeriveType<T>(this IServiceCollection services, Func<T> createFunc)
        where T : class
    {
        services.TryAddScoped(s =>
        {
            var config = createFunc.Invoke();

            Derive(s, typeof(T), config);

            return config;
        });
    }

    private static void Derive(IServiceProvider s, Type type, Object config)
    {
        var derive = type.GetMethod("Derive") ?? throw new InvalidOperationException("Cannot find Derive function.");
        var deriveArgs = derive.GetParameters()
        .Select(i => s.GetRequiredService(i.ParameterType))
        .ToArray();
        derive.Invoke(config, deriveArgs);
    }
}
