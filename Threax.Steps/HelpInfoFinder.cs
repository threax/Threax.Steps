using System.Reflection;

namespace Threax.Steps;

public record StepHelpInfo(String StepName, String HelpText);

public interface IHelpInfoFinder
{
    IEnumerable<StepHelpInfo> GetHelpInfo();
}

public class HelpInfoFinder : IHelpInfoFinder
{
    private readonly string ns;
    private readonly Assembly assembly;

    public HelpInfoFinder(String ns, Assembly assembly)
    {
        this.ns = ns;
        this.assembly = assembly;
    }

    public IEnumerable<StepHelpInfo> GetHelpInfo()
    {
        var isNamespace = ns;
        var startsNamespace = $"{ns}.";
        var types = assembly.GetTypes()
            .Where(i => !i.IsAbstract && !i.IsInterface && !i.Name.StartsWith("<") && (i.Namespace?.StartsWith(startsNamespace) == true || i.Namespace?.Equals(isNamespace) == true));

        return types.Select(i => new StepHelpInfo
        (
            i.Name,
            i.GetCustomAttribute<HelpInfoAttribute>()?.Description ?? "Unknown"
        ));
    }
}
