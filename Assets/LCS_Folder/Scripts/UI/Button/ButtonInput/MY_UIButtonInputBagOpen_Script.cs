using UnityEngine;

public class MY_UIButtonInputBagOpen_Script : MY_UIButtonInputBase_Script
{
    [SerializeField] private MY_UIBag_Script _bagAreaScript;
    // BagPanelController 를 MonoBehaviour 로 보관, IBagController 로 캐스팅
    [SerializeField] private UnityEngine.MonoBehaviour _bagControllerMono;
    private IBagController BagCtrl => _bagControllerMono as IBagController;

    public override void EventTrigger_ButtonClick_Func()
    {
        if (BagCtrl != null)
            BagCtrl.UserToggle();
        if (_bagAreaScript != null)
            _bagAreaScript.Click_ButtonClick_Func();
    }
}
