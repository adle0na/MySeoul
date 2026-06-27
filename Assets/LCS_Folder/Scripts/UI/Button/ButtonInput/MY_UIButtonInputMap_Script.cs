using UnityEngine;

public class MY_UIButtonInputMap_Script : MY_UIButtonInputBase_Script
{
    private bool is_Open = false;

    public override void EventTrigger_ButtonClick_Func()
    {
        this.is_Open = !this.is_Open;
        MY_UIPanelManager_Script.Instance.PanelOpen_Func(MY_UIPanelManager_Script.PanelType.Map_Panel, this.is_Open);
    }
}
