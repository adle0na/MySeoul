using UnityEngine;

public class MY_UIButtonInputTest_Script : MY_UIButtonInputBase_Script
{
    public override void EventTrigger_ButtonClick_Func()
    {
        Debug.Log("MY_UIButtonInputBase_Script 를 상속하여 상속된 스크립트를 버튼에게 할당해주고, My_UIButton_Script의 _clickeventScript로 값을 할당해주셔야 합니다.");
    }
}
