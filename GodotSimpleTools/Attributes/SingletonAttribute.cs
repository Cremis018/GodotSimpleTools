namespace GodotSimpleTools;

[AttributeUsage(AttributeTargets.Class)]
public class SingletonAttribute : Attribute
{
    public SingletonAttribute(SingletonMode mode = SingletonMode.Lazy)
    {
        Mode = mode;
    }
    
    public SingletonMode Mode { get; set; }
}

public enum SingletonMode
{
    Lazy,
    Eager,
    AutoLoad
}