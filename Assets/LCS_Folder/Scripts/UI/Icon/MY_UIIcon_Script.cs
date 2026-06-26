using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MY_UIIcon_Script : MonoBehaviour
{
    [Header("Trigger")]
    private bool _isWiggleWiggleOn;
    private bool _isBlinkOn = true;

    [Header("Shake Settings")]
    [SerializeField] private float _amplitude = 20f;   // 흔들리는 거리(px)
    [SerializeField] private float _frequency = 8f;    // 흔들리는 속도
    [SerializeField] private RectTransform _iconImgRectTransform;
    private Vector2 _originalPosition;

    [Header("Bilnk Settings")]
    [SerializeField] private GameObject _iconGameObject;
    [SerializeField] private float _blinkdelay = 0.5f;
    [SerializeField] private float _blinkWaiting = 1.5f;

    #region 연출
    private void AnimCall_Func()
    {
        //StartCoroutine(this.WiggleWiggle_Update_Co());
        //StartCoroutine(this.Blink_Update_Co());
    }

    private IEnumerator WiggleWiggle_Update_Co()
    {
        float offsetX = 0.0f;
        this._originalPosition = this._iconImgRectTransform.anchoredPosition;

        while (this._isWiggleWiggleOn == true)
        {
            offsetX = Mathf.Sin(Time.time * this._frequency) * this._amplitude;
            this._iconImgRectTransform.anchoredPosition = this._originalPosition + new Vector2(offsetX, 0f);

            yield return null;
        }

        this._iconImgRectTransform.anchoredPosition = this._originalPosition;

        yield return null;
    }

    private IEnumerator Blink_Update_Co()
    {
        this._iconGameObject.SetActive(true);

        while (this._isBlinkOn == true)
        {
            this._iconGameObject.SetActive(false);
            yield return new WaitForSeconds(this._blinkdelay);
            this._iconGameObject.SetActive(true);

            yield return new WaitForSeconds(this._blinkdelay);

            this._iconGameObject.SetActive(false);
            yield return new WaitForSeconds(this._blinkdelay);
            this._iconGameObject.SetActive(true);

            yield return new WaitForSeconds(this._blinkWaiting);
        }

        yield return null;
    }
    #endregion
}
