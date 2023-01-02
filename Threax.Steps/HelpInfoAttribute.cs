namespace Threax.Steps;

[AttributeUsage(AttributeTargets.Class)]
public class HelpInfoAttribute : Attribute
{
    public HelpInfoAttribute(String description)
    {
        this.Description = description;
    }

    public String Description { get; private set; }
}
