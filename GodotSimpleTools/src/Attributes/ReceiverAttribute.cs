namespace GodotSimpleTools.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ReceiverAttribute : Attribute
{
    public ReceiverAttribute(string notifyClassName, string notifyMemberName, NotifyMethod method)
    {
        NotifyClassName = notifyClassName;
        NotifyMemberName = notifyMemberName;
        Method = method;
    }

    public string NotifyClassName { get; }
    public string NotifyMemberName { get; }
    public NotifyMethod Method { get; }
}

public enum NotifyMethod
{
    Changed,
    Changing,
    All
}
