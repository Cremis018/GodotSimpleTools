namespace GodotSimpleTools.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class NotifyAttribute : Attribute
{
    public NotifyAttribute(object? defaultValue)
    {
        DefaultValue = defaultValue;
    }
    
    public NotifyAttribute(
        object? defaultValue = null,
        bool hasChanging = false)
    {
        HasChanging = hasChanging;
        DefaultValue = defaultValue;
    }
    
    public bool HasChanging { get; set; } = false;
    public object? DefaultValue { get; set; }
}