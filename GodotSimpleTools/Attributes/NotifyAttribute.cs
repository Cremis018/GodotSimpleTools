namespace GodotSimpleTools;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class NotifyAttribute : Attribute
{
    public NotifyAttribute()
    {
        
    }
    
    public NotifyAttribute(string? value = null)
    {
        Value = value;
    }
    
    public NotifyAttribute(
        NotifyReturnMode returnMode = NotifyReturnMode.NoReturn,
        bool hasChanging = false,
        string? value = null)
    {
        Value = value;
        ReturnMode = returnMode;
        HasChanging = hasChanging;
    }
    
    /// <summary>
    /// 默认值
    /// 当特性为属性注解时该属性才有效，它会为生成的字段赋值
    /// </summary>
    public string? Value { get; set; }
    /// <summary>
    /// 是否有Changing事件，一旦调用Set操作就会触发Changing事件
    /// 与Changed事件不同，而Changed事件在调用Set操作后如果值发生变化了才触发
    /// </summary>
    public bool HasChanging { get; set; }
    /// <summary>
    /// 事件返回类型
    /// NoReturn 即不返回任何值，事件类型就是Action
    /// ReturnValue 即返回赋的值，事件类型就是Action[属性的类型]
    /// ReturnSelf 即返回自身，事件类型就是Action[自身类型]
    /// </summary>
    public NotifyReturnMode ReturnMode { get; set; }
}

public enum NotifyReturnMode
{
    NoReturn,
    ReturnValue,
    ReturnSelf,
}