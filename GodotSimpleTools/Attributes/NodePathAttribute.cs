namespace GodotSimpleTools;

/// <summary>
/// 必须注解在节点类型的成员变量上
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class NodePathAttribute : Attribute
{
    public NodePathAttribute(string? value = null)
    {
        Value = value;
    }
    
    /// <summary>
    /// 节点路径
    /// 如果未填写，则优先选择对应类型的子节点中的第一个节点
    /// </summary>
    public string? Value { get; set; }
}