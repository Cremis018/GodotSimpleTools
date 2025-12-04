using Godot;

namespace GodotSimpleTools;

public static class StringExtension
{
    #region 判断
    /// <summary>
    /// 是否为有效文件路径
    /// </summary>
    /// <param name="path">判断路径</param>
    /// <returns>是否有效</returns>
    public static bool IsValidFilePath(this string path)
    {
        if (path.IsValidFileName()) return true;
        try
        {
            Path.GetFullPath(path);
            return true;
        }
        catch
        {
            GD.PushWarning($"文件路径\"{path}\"不合法");
            return false;
        }
    }
    
    /// <summary>
    /// 是否为有效Uri
    /// </summary>
    /// <param name="uri">判断Uri</param>
    /// <returns>是否有效</returns>
    public static bool IsValidUri(this string uri)
    {
        if (string.IsNullOrEmpty(uri)) return false;
        return Uri.TryCreate(uri, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp ||
                   uriResult.Scheme == Uri.UriSchemeHttps ||
                   uriResult.Scheme == Uri.UriSchemeFtp);
    }
    #endregion
    #region 操作
    /// <summary>
    /// 限制字符串
    /// 将字符串限制在指定长度内，超出部分截断
    /// </summary>
    /// <param name="origin">原字符串</param>
    /// <param name="length">限制长度</param>
    /// <returns>截取结果</returns>
    public static string RestrictString(this string origin, int length)
    {
        if (length <= 0) return string.Empty;
        return origin.Length >= length ? origin[..length] : origin;
    }
        
    /// <summary>
    /// 获取颜色
    /// </summary>
    /// <param name="code">十六进制颜色代码或颜色单词</param>
    /// <returns>颜色</returns>
    public static Color Color(this string code) =>
        Godot.Color.HtmlIsValid(code) ? 
            Godot.Color.FromHtml(code) : 
            Godot.Color.FromString(code,Colors.White);

    /// <summary>
    /// 获取绝对路径
    /// </summary>
    /// <param name="filePath">文件路径或虚拟路径</param>
    /// <returns>绝对路径</returns>
    public static string GetAbsolutePath(this string filePath)
    {
        try
        {
            return ProjectSettings.GlobalizePath(filePath);
        }
        catch
        {
            return Path.GetFullPath(filePath);
        }
    }
    #endregion
}