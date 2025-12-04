using Godot.NativeInterop;
using GodotSimpleTools;

namespace GodotSimpleToolsTest;

//用户写的
public partial class ReceiverSample
{
    private readonly NotifySample _notifySample1 = new();
    private readonly NotifySample _notifySample2 = new();
    [Notify] public int MyValue { get => GetMyValue(); set => SetMyValue(value); }
    
    //用户在自动生成后可以补充的
     public ReceiverSample()
     {
         InitNotifies();
     }
     
     public void Destroy()
     {
         DestroyNotifies();
     }
    
    [Receiver(nameof(_notifySample1.IsMenChanged))]
    private void OnNotifySampleSexChanged(bool value)
    {
        
    }

    private void OnNotifySampleName2(string value)
    {
        
    }
    
    [Receiver(nameof(_notifySample2.Value1Changed))]
    private void ValueChanged(int value)
    {
        
    }
    
    [Receiver(nameof(MyValueChanged))]
    [Receiver(nameof(_notifySample1.Value1Changed))]
    private void OnManyValueChanged(int value)
    {
        
    }
}

//自动生成的
// public partial class ReceiverSample
// {
//     public void InitNotifies()
//     {
//         _notifySample2.Name2Changed += OnNotifySampleName2;
//         _notifySample2.Name2Changing += OnNotifySampleName2;
//         _notifySample1.Value1Changed += ValueChanged;
//         MyValueChanged += OnManyValueChanged;
//         _notifySample2.Value1Changed += OnManyValueChanged;
//     }
//
//     public void DestroyNotifies()
//     {
//         _notifySample2.Name2Changed -= OnNotifySampleName2;
//         _notifySample2.Name2Changing -= OnNotifySampleName2;
//         _notifySample1.Value1Changed -= ValueChanged;
//         MyValueChanged -= OnManyValueChanged;
//         _notifySample2.Value1Changed -= OnManyValueChanged;
//     }
// }
