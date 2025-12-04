namespace GodotSimpleTools;

public static class EnumExtension
{
    #region 处理
    /// <summary>
    /// 枚举值
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="hasDebug">是否有Debug辅助枚举值</param>
    /// <returns>枚举值字符串数组</returns>
    /// <exception cref="ArgumentException">类型错误</exception>
    public static string[] EnumValues(this Type enumType,bool hasDebug = true)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        var values = enumType.IsEnum 
            ? Enum.GetNames(enumType) 
            : throw new ArgumentException($"传入的类型{nameof(enumType)}不是枚举类型！");
        if (hasDebug) return values;
        var list = values.ToList();
        list.RemoveAll(x =>
            string.Compare(x, "Unknown", StringComparison.OrdinalIgnoreCase) == 0 ||
            string.Compare(x, "Count", StringComparison.OrdinalIgnoreCase) == 0);
        values = list.ToArray();
        return values;
    }
    
    /// <summary>
    /// 枚举值
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="ignore">忽略的枚举值</param>
    /// <returns>枚举值字符串数组</returns>
    /// <exception cref="ArgumentException">类型错误</exception>
    public static string[] EnumValues(this Type enumType,params string[] ignore)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        var values = enumType.IsEnum 
            ? Enum.GetNames(enumType) 
            : throw new ArgumentException($"传入的类型{nameof(enumType)}不是枚举类型！");
        if (ignore.Length <= 0) return values;
        var list = values.ToList();
        foreach (var s in ignore) list.RemoveAll(x => string.Compare(x, s, StringComparison.OrdinalIgnoreCase) == 0);
        values = list.ToArray();
        return values;
    }
    #endregion
}