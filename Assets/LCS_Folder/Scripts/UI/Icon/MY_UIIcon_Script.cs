using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MY_UIIcon_Script : MonoBehaviour
{
    [Header("Icon Sprites")]
    [SerializeField] private Sprite _beforeSprite;   // 처음 오픈 시 스프라이트 (주의 단계)
    [SerializeField] private Sprite _afterSprite;    // 업그레이드 시 스프라이트 (위험 단계)

    [Header("Shake Settings")]
    [SerializeField] private float _amplitude = 20f;
    [SerializeField] private float _frequency = 8f;
    [SerializeField] private RectTransform _iconImgRectTransform;
    private Vector2 _originalPosition;

    [Header("Blink Settings")]
    [SerializeField] private GameObject _iconGameObject;
    [SerializeField] private float _blinkdelay = 0.5f;
    [SerializeField] private float _blinkWaiting = 1.5f;

    private bool _isWiggleWiggleOn = false;
    private bool _isBlinkOn = false;
    private bool _isClick = false;

    private Image _iconImage;
    private Coroutine _wiggleCo;
    private Coroutine _blinkCo;

    void Awake()
    {
        if (_iconGameObject != null)
            _iconImage = _iconGameObject.GetComponent<Image>();
    }

    // ── 외부 호출 API ────────────────────────────────────────────────

    /// <summary>
    /// 아이콘 표시. isUpgraded=false → beforeSprite + WiggleWiggle만
    ///                isUpgraded=true  → afterSprite + WiggleWiggle + Blink 주기 반복
    /// </summary>
    public void Show_Icon(bool isUpgraded)
    {
        gameObject.SetActive(true);

        // 스프라이트 교체
        if (_iconImage != null)
        {
            var sprite = isUpgraded ? _afterSprite : _beforeSprite;
            if (sprite != null) _iconImage.sprite = sprite;
        }

        StopAllIconCoroutines();

        // 처음 오픈: WiggleWiggle
        _isWiggleWiggleOn = true;
        _wiggleCo = StartCoroutine(WiggleWiggle_Update_Co());

        // 위험 단계 업그레이드: Blink도 주기적으로 추가
        if (isUpgraded)
        {
            _isBlinkOn = true;
            _blinkCo = StartCoroutine(Blink_Update_Co());
        }
    }

    /// <summary>아이콘 숨김</summary>
    public void Hide_Icon()
    {
        StopAllIconCoroutines();
        if (_iconGameObject != null) _iconGameObject.SetActive(true); // 숨기기 전 상태 복원
        gameObject.SetActive(false);
    }

    void StopAllIconCoroutines()
    {
        _isWiggleWiggleOn = false;
        _isBlinkOn = false;
        if (_wiggleCo != null) { StopCoroutine(_wiggleCo); _wiggleCo = null; }
        if (_blinkCo  != null) { StopCoroutine(_blinkCo);  _blinkCo  = null; }
        // 위치 복원
        if (_iconImgRectTransform != null)
            _iconImgRectTransform.anchoredPosition = _originalPosition;
        // 오브젝트 복원
        if (_iconGameObject != null) _iconGameObject.SetActive(true);
    }

    // ── 코루틴 ───────────────────────────────────────────────────────

    private IEnumerator WiggleWiggle_Update_Co()
    {
        if (_iconImgRectTransform == null) yield break;
        _originalPosition = _iconImgRectTransform.anchoredPosition;

        while (_isWiggleWiggleOn)
        {
            float offsetX = Mathf.Sin(Time.time * _frequency) * _amplitude;
            _iconImgRectTransform.anchoredPosition = _originalPosition + new Vector2(offsetX, 0f);
            yield return null;
        }

        _iconImgRectTransform.anchoredPosition = _originalPosition;
    }

    private IEnumerator Blink_Update_Co()
    {
        if (_iconGameObject == null) yield break;
        _iconGameObject.SetActive(true);

        while (_isBlinkOn)
        {
            _iconGameObject.SetActive(false);
            yield return new WaitForSeconds(_blinkdelay);
            _iconGameObject.SetActive(true);
            yield return new WaitForSeconds(_blinkdelay);
            _iconGameObject.SetActive(false);
            yield return new WaitForSeconds(_blinkdelay);
            _iconGameObject.SetActive(true);
            yield return new WaitForSeconds(_blinkWaiting);
        }
    }

    // ── 클릭 ─────────────────────────────────────────────────────────

    public void Click_Icon_Func()
    {
        _isClick = !_isClick;
        Color col = _isClick ? new Color(0.5f, 0.5f, 0.5f, 1f) : Color.white;
        if (_iconImage != null) _iconImage.color = col;
    }
}
