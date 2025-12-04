namespace GodotSimpleTools;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ReceiverAttribute : Attribute
{
    public ReceiverAttribute(string @event)
    {
        Event = @event;
    }
    
    public string Event { get; init; }
}
