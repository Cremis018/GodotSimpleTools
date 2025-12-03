using GodotSimpleTools.Attributes;

namespace GodotSimpleToolsTest;

//用户写的
public partial class ReceiverSample
{
    private readonly NotifySample _notifySample1 = new();
    private readonly NotifySample _notifySample2 = new();
    
     public ReceiverSample()
     {
         InitNotifies();
     }
    
     public void Destroy()
     {
         DestroyNotifies();
     }
    
    [Receiver(nameof(_notifySample1), NotifySample.Name_name, NotifyMethod.Changed)]
    private void OnNotifySampleNameChanged(string value)
    {
        
    }

    [Receiver(nameof(_notifySample2), NotifySample.Name2_name, NotifyMethod.All)]
    private void OnNotifySampleName2(string value)
    {
        
    }
    
    [Receiver(nameof(_notifySample1), NotifySample.Value1_name, NotifyMethod.Changed)]
    [Receiver(nameof(_notifySample2), NotifySample.Value1_name, NotifyMethod.Changed)]
    private void ValueChanged(int value)
    {
        
    }
}

//自动生成的
// public partial class ReceiverSample
// {
//     public void InitNotifies()
//     {
//         _notifySample1.NameChanged += OnNotifySampleNameChanged;
//         _notifySample2.Name2Changed += OnNotifySampleName2;
//         _notifySample2.Name2Changing += OnNotifySampleName2;
//         _notifySample1.Value1Changed += ValueChanged;
//         _notifySample2.Value1Changed += ValueChanged;
//     }
//
//     public void DestroyNotifies()
//     {
//         _notifySample1.NameChanged -= OnNotifySampleNameChanged;
//         _notifySample2.Name2Changed -= OnNotifySampleName2;
//         _notifySample2.Name2Changing -= OnNotifySampleName2;
//         _notifySample1.Value1Changed -= ValueChanged;
//         _notifySample2.Value1Changed -= ValueChanged;
//     }
// }
