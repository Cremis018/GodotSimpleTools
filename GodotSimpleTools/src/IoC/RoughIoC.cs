namespace GodotSimpleTools.IoC;

public class RoughIoC
{
    private readonly Dictionary<Type, object> _singletons = new();
    private readonly Dictionary<Type, Type> _registrations = new();
    private readonly Dictionary<Type, Lifecycle> _lifecycles = new();
    
    /// <summary>
    /// 注册
    /// </summary>
    /// <param name="lifecycle">生命周期</param>
    /// <typeparam name="TService">服务接口</typeparam>
    /// <typeparam name="TImplementation">服务实现</typeparam>
    public void Register<TService, TImplementation>(Lifecycle lifecycle = Lifecycle.Transient) 
        where TImplementation : TService
    {
        _registrations[typeof(TService)] = typeof(TImplementation);
        _lifecycles[typeof(TService)] = lifecycle;
    }

    /// <summary>
    /// 解析
    /// </summary>
    /// <typeparam name="TService">服务接口</typeparam>
    /// <returns>服务类实现对象</returns>
    /// <exception cref="KeyNotFoundException">未注册异常</exception>
    public TService Resolve<TService>()
    {
        var serviceType = typeof(TService);
        if (!_registrations.TryGetValue(serviceType, out var implType))
            throw new KeyNotFoundException($"类型 {serviceType} 还未注册");

        var lifecycle = _lifecycles[serviceType];

        if (lifecycle != Lifecycle.Singleton) return (TService)CreateInstance(implType);
        if (_singletons.TryGetValue(serviceType, out var singletonObj)) return (TService)singletonObj;
        singletonObj = CreateInstance(implType);
        _singletons[serviceType] = singletonObj;
        return (TService)singletonObj;
    }
    
    /// <summary>
    /// 解析
    /// </summary>
    /// <param name="serviceType">服务接口类型</param>
    /// <returns>装箱服务实现类型</returns>
    /// <exception cref="KeyNotFoundException">未注册异常</exception>
    public object Resolve(Type serviceType)
    {
        if (!_registrations.TryGetValue(serviceType, out var implType))
            throw new KeyNotFoundException($"类型 {serviceType} 还未注册");

        var lifecycle = _lifecycles[serviceType];

        if (lifecycle != Lifecycle.Singleton) return CreateInstance(implType);
        if (_singletons.TryGetValue(serviceType, out var singletonObj))
            return singletonObj;

        var instance = CreateInstance(implType);
        _singletons[serviceType] = instance;
        return instance;
    }
    
    private object CreateInstance(Type type)
    {
        var constructors = type.GetConstructors();
        var constructor = constructors.FirstOrDefault();
        if (constructor == null) throw new InvalidOperationException("未找到公共的构造器");

        var parameters = constructor.GetParameters();
        var parameterInstances = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            parameterInstances[i] = Resolve(paramType);
        }
        
        return constructor.Invoke(parameterInstances);
    }
}