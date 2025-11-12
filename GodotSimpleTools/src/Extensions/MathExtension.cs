namespace GodotSimpleTools.Extensions;

public static class MathExtension
{
    #region 判断
    /// <summary>
    /// 近似为
    /// </summary>
    /// <param name="origin">原数值</param>
    /// <param name="value">比较值</param>
    /// <param name="tolerance">近似程度</param>
    public static bool IsEqualsApprox(this float origin, float value, float tolerance = 1e-5f) 
        => Math.Abs(origin - value) < tolerance;
    
    /// <summary>
    /// 近似为
    /// </summary>
    /// <param name="origin">原数值</param>
    /// <param name="value">比较值</param>
    /// <param name="tolerance">近似程度</param>
    public static bool IsEqualsApprox(this double origin, double value, double tolerance = 1e-5) 
        => Math.Abs(origin - value) < tolerance;
    #endregion
    #region 循环计算
    /// <summary>
    /// 获取循环计算结果。当增量超过循环限制时会从头开始返回计算结果
    /// 即循环为10时，原数字为9，当增量为2时，会返回1，因为(9+2)%10=1
    /// 当增量为1时，会返回0，因为(9+1)%10=0
    /// </summary>
    /// <param name="origin">原数值</param>
    /// <param name="loop">循环数</param>
    /// <param name="add">增量</param>
    /// <returns>计算结果</returns>
    public static int LoopAdd(this int origin, int loop, int add)
    {
        add = add % loop;
        return (origin + add + loop) % loop;
    }
    /// <summary>
    /// 获取循环计算结果。当增量超过循环限制时会从头开始返回计算结果
    /// 即循环为10时，原数字为9，当增量为2时，会返回1，因为(9+2)%10=1
    /// 当增量为1时，会返回0，因为(9+1)%10=0
    /// </summary>
    /// <param name="origin">原数值</param>
    /// <param name="loop">循环数</param>
    /// <param name="add">增量</param>
    /// <returns>计算结果</returns>
    public static long LoopAdd(this long origin, long loop, long add)
    {
        add = add % loop;
        return (origin + add + loop) % loop;
    }
    /// <summary>
    /// 获取循环计算结果。当增量超过循环限制时会从头开始返回计算结果
    /// 即循环为10时，原数字为9，当增量为2时，会返回1，因为(9+2)%10=1
    /// 当增量为1时，会返回0，因为(9+1)%10=0
    /// </summary>
    /// <param name="origin">原数值</param>
    /// <param name="loop">循环数</param>
    /// <param name="add">增量</param>
    /// <returns>计算结果</returns>
    public static float LoopAdd(this float origin, float loop, float add)
    {
        add = add % loop;
        return (origin + add + loop) % loop;
    }
    /// <summary>
    /// 获取循环计算结果。当增量超过循环限制时会从头开始返回计算结果
    /// 即循环为10时，原数字为9，当增量为2时，会返回1，因为(9+2)%10=1
    /// 当增量为1时，会返回0，因为(9+1)%10=0
    /// </summary>
    /// <param name="origin">原数值</param>
    /// <param name="loop">循环数</param>
    /// <param name="add">增量</param>
    /// <returns>计算结果</returns>
    public static double LoopAdd(this double origin, double loop, double add)
    {
        add = add % loop;
        return (origin + add + loop) % loop;
    }
    
    /// <summary>
    /// 获取循环计算结果。当增量超过循环限制时会从头开始返回计算结果
    /// 即循环为1-10时，原数字为9，当增量为2时，会返回1，因为(9+2)%(10-1)=2
    /// 当增量为1时，会返回1，因为(9+1)%(10-1)=1
    /// </summary>
    /// <param name="origin">原数值</param>
    /// <param name="loopStart">循环开始数</param>
    /// <param name="loopEnd">循环结束数</param>
    /// <param name="add">增量</param>
    /// <returns>计算结果</returns>
    public static int LoopAdd(this int origin,int loopStart, int loopEnd, int add)
    {
        var loop = loopEnd - loopStart;
        add = add % loop;
        return (origin + add + loop) % loop + loopStart;
    }
    /// <summary>
    /// 获取循环计算结果。当增量超过循环限制时会从头开始返回计算结果
    /// 即循环为1-10时，原数字为9，当增量为2时，会返回1，因为(9+2)%(10-1)=2
    /// 当增量为1时，会返回1，因为(9+1)%(10-1)=1
    /// </summary>
    /// <param name="origin">原数值</param>
    /// <param name="loopStart">循环开始数</param>
    /// <param name="loopEnd">循环结束数</param>
    /// <param name="add">增量</param>
    /// <returns>计算结果</returns>
    public static long LoopAdd(this long origin, long loopStart, long loopEnd, long add)
    {
        var loop = loopEnd - loopStart;
        add = add % loop;
        return (origin + add + loop) % loop + loopStart;
    }
    /// <summary>
    /// 获取循环计算结果。当增量超过循环限制时会从头开始返回计算结果
    /// 即循环为1-10时，原数字为9，当增量为2时，会返回1，因为(9+2)%(10-1)=2
    /// 当增量为1时，会返回1，因为(9+1)%(10-1)=1
    /// </summary>
    /// <param name="origin">原数值</param>
    /// <param name="loopStart">循环开始数</param>
    /// <param name="loopEnd">循环结束数</param>
    /// <param name="add">增量</param>
    /// <returns>计算结果</returns>
    public static float LoopAdd(this float origin, float loopStart, float loopEnd, float add)
    {
        var loop = loopEnd - loopStart;
        add = add % loop;
        return (origin + add + loop) % loop + loopStart;
    }
    /// <summary>
    /// 获取循环计算结果。当增量超过循环限制时会从头开始返回计算结果
    /// 即循环为1-10时，原数字为9，当增量为2时，会返回1，因为(9+2)%(10-1)=2
    /// 当增量为1时，会返回1，因为(9+1)%(10-1)=1
    /// </summary>
    /// <param name="origin">原数值</param>
    /// <param name="loopStart">循环开始数</param>
    /// <param name="loopEnd">循环结束数</param>
    /// <param name="add">增量</param>
    /// <returns>计算结果</returns>
    public static double LoopAdd(this double origin, double loopStart, double loopEnd, double add)
    {
        var loop = loopEnd - loopStart;
        add = add % loop;
        return (origin + add + loop) % loop + loopStart;
    }
    #endregion
    #region 数值转换
    /// <summary>
    /// 弧度转角度
    /// </summary>
    /// <param name="rad">弧度值</param>
    /// <returns>角度值</returns>
    public static float RadToDeg(this float rad) => rad * 180 / MathF.PI;
    
    /// <summary>
    /// 角度转弧度
    /// </summary>
    /// <param name="deg">角度值</param>
    /// <returns>弧度值</returns>
    public static float DegToRad(this float deg) => deg * MathF.PI / 180;

    /// <summary>
    /// 毫秒数转时间字符串
    /// </summary>
    /// <param name="ms">毫秒数</param>
    /// <returns>时间字符串</returns>
    public static string MsToTime(this int ms)
    {
        var timeSpan = TimeSpan.FromMilliseconds(ms);
        return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }

    /// <summary>
    /// 快速获取随机整数，范围为0-2147483647
    /// </summary>
    /// <returns>随机值</returns>
    public static int RandomInt()
    {
        var rnd = new Random();
        return rnd.Next();
    }
    
    /// <summary>
    /// 快速获取随机数
    /// </summary>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>随机值</returns>
    public static int Random(int min, int max)
    {
        var rnd = new Random();
        return rnd.Next(min, max);
    }
    
    /// <summary>
    /// 快速获取随机长整数，范围为0-9223372036854775807
    /// </summary>
    /// <returns>随机值</returns>
    public static long RandomLong()
    {
        var rnd = new Random();
        return rnd.NextInt64();
    }
    
    /// <summary>
    /// 快速获取随机数
    /// </summary>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>随机值</returns>
    public static long Random(long min, long max)
    {
        var rnd = new Random();
        return rnd.NextInt64(min, max);
    }
    
    /// <summary>
    /// 快速获取随机单精度实数，范围为0-1
    /// </summary>
    /// <returns>随机值</returns>
    public static float RandomFloat()
    {
        var rnd = new Random();
        return rnd.NextSingle();
    }

    /// <summary>
    /// 快速获取随机数
    /// </summary>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>随机值</returns>
    public static float Random(float min, float max)
    {
        var rnd = new Random();
        return rnd.NextSingle() * (max - min) + min;
    }
    
    /// <summary>
    /// 快速获取随机双精度实数，范围为0-1
    /// </summary>
    /// <returns>随机值</returns>
    public static double RandomDouble()
    {
        var rnd = new Random();
        return rnd.NextDouble();
    }
    
    /// <summary>
    /// 快速获取随机数
    /// </summary>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>随机值</returns>
    public static double Random(double min, double max)
    {
        var rnd = new Random();
        return rnd.NextDouble() * (max - min) + min;
    }

    /// <summary>
    /// 限制在0-1之间
    /// </summary>
    /// <param name="value">处理值</param>
    /// <returns>限制值</returns>
    public static float Clamp01(this float value) => Math.Clamp(value, 0, 1);
    #endregion
}