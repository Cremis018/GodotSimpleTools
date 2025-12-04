namespace GodotSimpleTools;

public static class EnumerableExtension
{
    #region 判断
    public static bool IsEnumerableNullOrEmpty<T>(this IEnumerable<T>? origin) => origin is null || !origin.Any();
    #endregion
}