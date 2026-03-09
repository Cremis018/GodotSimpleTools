namespace GodotSimpleTools;

[AttributeUsage(AttributeTargets.Class)]
public class NewableNodeAttribute : Attribute
{
    public NewableNodeAttribute(string path)
    {
        Path = path;
    }

    public string Path { get; set; }
}