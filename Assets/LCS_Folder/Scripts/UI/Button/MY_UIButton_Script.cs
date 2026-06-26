using DG.Tweening;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MY_UIButton_Script : MonoBehaviour
{
    [Header("Button Setting")]
    [SerializeField] private GameObject _buttonGameObject;
    [SerializeField] private GameObject _buttonTextGameObject;
    [SerializeField] private GameObject _buttonBackGroundGameObject;
    [SerializeField] private bool _isShowText;
    [SerializeField] private bool _isShowBackground;
    [SerializeField] private MY_UIButtonInputBase_Script _clickeventScript;
    [SerializeField] private MY_UIButtonManager_Script.UIButtonType _buttonType;

    private ButtonInfo_Class _buttonInfo;

    [Header("PressedAnimation Setting")]
    [SerializeField] private float pressedScale = 0.9f;
    [SerializeField] private float duration = 0.1f;
    private Vector3 _defaultScale;
    private RectTransform _buttonRectTr;

    private Tween _scaleTween;

    private void Start()
    {
        this._buttonInfo = MY_UIButtonManager_Script.Instance.Get_UIButtonSpriteTypeToSprite_Func(this._buttonType);

        if(this._buttonInfo != null)
        {
            this._buttonGameObject.GetComponent<Image>().sprite = this._buttonInfo._buttonSprite;

            if(this._isShowText == true)
                this._buttonTextGameObject.GetComponent<TextMeshProUGUI>().text = this._buttonInfo._buttonName;

            if (this._isShowBackground == true)
                this._buttonBackGroundGameObject.GetComponent<Image>().sprite = this._buttonInfo._buttonBackSprite;

        }

        this._buttonTextGameObject.SetActive(this._isShowText);
        this._buttonBackGroundGameObject.SetActive(this._isShowBackground);

        this._defaultScale = this.transform.localScale;
        this._buttonRectTr = this.GetComponent<RectTransform>();
    }

    public void EventTrigger_PointDown_Func()
    {
        this._scaleTween?.Kill();

        this._scaleTween = this._buttonRectTr.DOScale(this._defaultScale * this.pressedScale, this.duration).SetEase(Ease.OutQuad);
    }

    public void EventTrigger_PointUp_Func()
    {
        this._scaleTween?.Kill();

        this._scaleTween = this._buttonRectTr.DOScale(this._defaultScale, this.duration).SetEase(Ease.OutBack);
    }

    public void EventTrigger_ButtonClick_Func()
    {
        this._clickeventScript.EventTrigger_ButtonClick_Func();
    }
}
