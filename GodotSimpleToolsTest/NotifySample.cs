using GodotSimpleTools;

namespace GodotSimpleToolsTest;

//用户写的
public partial class NotifySample
{
    [Notify] public string Name { get => GetName(); set => SetName(value); }
    [Notify(HasChanging = true,DefaultValue = "Hello")] public string Name2 { get => GetName2(); set => SetName2(value); }
    [Notify(1)] public int Value1 { get => GetValue1(); set => SetValue1(value); }
    [Notify(true)] public bool IsMen { get => GetIsMen(); set => SetIsMen(value); }
    
    public void Update(string _){}
}

// 生成器生成的
 // public partial class NotifySample
 // {
 //     public const string ClassName = "GodotSimpleToolsTest.NotifySample";
 //
 //     public const string NameName = "Name";
 //     public const string Name2Name = "Name2";
 //     public const string Value1Name = "Value1";
 //     
 //     private string _name;
 //     private string _name2;
 //     private int _value1 = 1;
 //     
 //     public event Action<string> NameChanged;
 //     public event Action<string> Name2Changed;
 //     public event Action<string> Name2Changing;
 //     public event Action<int> Value1Changed;
 //
 //     public string GetName() => _name;
 //     public string GetName2() => _name2;
 //     public int GetValue1() => _value1;
 //     
 //     public void SetName(string value,Action? callback = null)
 //     {
 //         callback?.Invoke();
 //         if (Equals(_name, value)) return;
 //         _name = value;
 //         NameChanged?.Invoke(value);
 //     }
 //     
 //     public void SetName2(string value,Action? callback = null)
 //     {
 //         callback?.Invoke();
 //         Name2Changing?.Invoke(value);
 //         if (Equals(_name2, value)) return;
 //         _name2 = value;
 //         Name2Changed?.Invoke(value);
 //     }
 //     
 //     public void SetValue1(int value,Action? callback = null)
 //     {
 //         callback?.Invoke();
 //         if (Equals(_value1, value)) return;
 //         _value1 = value;
 //         Value1Changed?.Invoke(value);
 //     }
 // }