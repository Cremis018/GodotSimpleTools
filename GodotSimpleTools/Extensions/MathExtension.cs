namespace GodotSimpleTools.Extensions;

public static class MathExtension
{
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
}