using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class MY_UIBag_Script : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private float thresholdY = 100f;
    [SerializeField] private float endDuration;

    private float   endYMove;
    private Vector2 dragStartPos;
    private bool    thresholdPassed = false;
    private bool    _isOpen;

    // IBagController 참조 — 닫힐 때 NotifyBagClosing() 호출
    [SerializeField] private UnityEngine.MonoBehaviour _bagControllerMono;
    [SerializeField] private MY_UIButton_Script _buttonscript;
    private IBagController BagCtrl => _bagControllerMono as IBagController;

    public bool IsOpen => _isOpen;

    // 닫힌 상태 Y 위치 (height 기반으로 동적 계산)
    private float closedY;

    private void Awake()
    {
        float h     = this.gameObject.GetComponent<RectTransform>().rect.height;
        this.endYMove = h / 2f;
        this.closedY  = -410.0f;
        this._isOpen  = false;
    }

    public void OnBeginDrag(BaseEventData eventData)
    {
        PointerEventData pointerData = (PointerEventData)eventData;
        this.dragStartPos    = pointerData.position;
        this.thresholdPassed = false;
    }

    public void OnDrag(BaseEventData eventData)
    {
        if (this.thresholdPassed) return;

        var   pointerData = (PointerEventData)eventData;
        float deltaY      = pointerData.position.y - this.dragStartPos.y;

        if (deltaY >= this.thresholdY)
        {
            this.thresholdPassed = true;
            this.OnDragUp();
        }
        else if (deltaY <= -thresholdY && this._isOpen)
        {
            this.thresholdPassed = true;
        }
    }

    private void OnDragUp()
    {
        this._isOpen = true;
        this.gameObject.GetComponent<RectTransform>()
            .DOAnchorPosY(this.endYMove, this.endDuration);
    }
    

    private void OnDragDown()
    {
        this._isOpen = false;
        this.gameObject.GetComponent<RectTransform>()
            .DOAnchorPosY(this.closedY, this.endDuration * 0.5f);
    }

    // 유저가 드래그로 닫을 때 — pending 플로우 알림 후 닫기
    private void NotifyClosingAndDragDown()
    {
        if (BagCtrl != null)
            BagCtrl.NotifyBagClosing();
        OnDragDown();
    }

    public void Click_ButtonClick_Func()
    {
        if (!this._isOpen)
        {
            OnDragUp();
        }
        else
        {
            // 버튼으로 닫을 때도 pending 플로우 알림
            if (BagCtrl != null)
                BagCtrl.NotifyBagClosing();
            OnDragDown();
        }
    }
}
