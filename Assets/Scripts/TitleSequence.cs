using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

namespace SeoulLast
{
    /// <summary>
    /// 타이틀 연출 시퀀스
    /// ① 로고 형광등 점등 → ② 로고 상단 이동 → ③ 텍스트 페이드인 → ④ 텍스트 루프
    /// 화면 클릭 시 즉시 스킵
    /// </summary>
    public class TitleSequence : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Image            logoImage;        // TitleLogo
        [SerializeField] TextMeshProUGUI  titleText;        // TitleText
        [SerializeField] Button           skipButton;       // StartBtn (전체화면)

        [Header("Logo 점등")]
        [SerializeField] float flickerDuration  = 1.5f;    // 전체 점등 시간
        [SerializeField] int   flickerCount     = 6;       // 깜빡 횟수

        [Header("Logo 이동")]
        [SerializeField] float moveDelay        = 0.3f;    // 점등 후 대기
        [SerializeField] float moveDuration     = 1.0f;    // 이동 시간
        [SerializeField] Vector2 logoTargetPos  = new Vector2(0f, 900f); // 상단 목표 위치
        [SerializeField] float  logoTargetScale = 0.85f;   // 도착 시 축소 비율

        [Header("텍스트 등장")]
        [SerializeField] float textFadeDuration = 0.5f;    // 페이드인 시간
        [SerializeField] float textRiseAmount   = 10f;     // 떠오르기 픽셀

        [Header("텍스트 루프 (발광 호흡)")]
        [SerializeField] float breathMinAlpha   = 0.7f;
        [SerializeField] float breathMaxAlpha   = 1.0f;
        [SerializeField] float breathDuration   = 1.8f;    // 왕복 주기

        Vector2  _logoStartPos;
        bool     _skipped = false;
        Sequence _mainSeq;
        Tween    _breathTween;

        void Start()
        {
            if (logoImage   != null) _logoStartPos = logoImage.rectTransform.anchoredPosition;

            // 초기 상태: 로고 투명, 텍스트 투명
            if (logoImage  != null) SetAlpha(logoImage,  0f);
            if (titleText  != null) SetAlpha(titleText,  0f);

            // 스킵 버튼 연결
            if (skipButton != null) skipButton.onClick.AddListener(Skip);

            StartCoroutine(PlaySequence());
        }

        void OnDisable()
        {
            _mainSeq?.Kill();
            _breathTween?.Kill();
        }

        // ── 메인 시퀀스 ──────────────────────────────────────────────
        IEnumerator PlaySequence()
        {
            // ① 형광등 점등
            yield return StartCoroutine(FlickerLogo());
            if (_skipped) yield break;

            // ② 점등 후 잠깐 대기
            yield return new WaitForSecondsRealtime(moveDelay);
            if (_skipped) yield break;

            // ③ 로고 상단 이동 + 축소
            yield return StartCoroutine(MoveLogo());
            if (_skipped) yield break;

            // ④ 텍스트 페이드인 + 떠오르기
            yield return StartCoroutine(ShowText());
            if (_skipped) yield break;

            // ⑤ 텍스트 루프 (무한)
            StartBreathLoop();
        }

        // ── ① 형광등 점등 ────────────────────────────────────────────
        IEnumerator FlickerLogo()
        {
            if (logoImage == null) yield break;

            // 불규칙 깜빡 타이밍 생성
            // 처음엔 약하고 짧게, 점점 강하고 길게
            float[] onTimes  = { 0.04f, 0.03f, 0.06f, 0.08f, 0.12f, 0.20f };
            float[] offTimes = { 0.18f, 0.22f, 0.14f, 0.10f, 0.08f, 0.05f };
            float[] alphas   = { 0.25f, 0.20f, 0.45f, 0.60f, 0.80f, 1.00f };

            int count = Mathf.Min(flickerCount, onTimes.Length);

            for (int i = 0; i < count; i++)
            {
                if (_skipped) { SetAlpha(logoImage, 1f); yield break; }

                // 켜짐
                SetAlpha(logoImage, alphas[i]);
                yield return new WaitForSecondsRealtime(onTimes[i]);

                if (_skipped) { SetAlpha(logoImage, 1f); yield break; }

                // 꺼짐 (마지막은 안 끔)
                if (i < count - 1)
                {
                    SetAlpha(logoImage, 0f);
                    yield return new WaitForSecondsRealtime(offTimes[i]);
                }
            }

            // 완전 점등 확정
            SetAlpha(logoImage, 1f);
        }

        // ── ② 로고 상단 이동 ─────────────────────────────────────────
        IEnumerator MoveLogo()
        {
            if (logoImage == null) yield break;

            var rt   = logoImage.rectTransform;
            bool done = false;

            _mainSeq = DOTween.Sequence()
                .Append(rt.DOAnchorPos(logoTargetPos, moveDuration).SetEase(Ease.OutCubic))
                .Join(rt.DOScale(logoTargetScale, moveDuration).SetEase(Ease.OutCubic))
                .SetUpdate(true)
                .OnComplete(() => done = true);

            yield return new WaitUntil(() => done || _skipped);

            if (_skipped)
            {
                _mainSeq?.Kill();
                rt.anchoredPosition = logoTargetPos;
                rt.localScale = Vector3.one * logoTargetScale;
            }
        }

        // ── ③ 텍스트 페이드인 + 떠오르기 ────────────────────────────
        IEnumerator ShowText()
        {
            if (titleText == null) yield break;

            var rt = titleText.rectTransform;
            Vector2 startPos = rt.anchoredPosition - new Vector2(0f, textRiseAmount);
            Vector2 endPos   = rt.anchoredPosition;

            rt.anchoredPosition = startPos;
            SetAlpha(titleText, 0f);

            bool done = false;
            _mainSeq = DOTween.Sequence()
                .Append(rt.DOAnchorPos(endPos, textFadeDuration).SetEase(Ease.OutCubic))
                .Join(titleText.DOFade(1f, textFadeDuration).SetEase(Ease.OutCubic))
                .SetUpdate(true)
                .OnComplete(() => done = true);

            yield return new WaitUntil(() => done || _skipped);

            if (_skipped)
            {
                _mainSeq?.Kill();
                rt.anchoredPosition = endPos;
                SetAlpha(titleText, 1f);
            }
        }

        // ── ④ 발광 호흡 루프 ─────────────────────────────────────────
        void StartBreathLoop()
        {
            if (titleText == null) return;
            _breathTween = titleText
                .DOFade(breathMinAlpha, breathDuration / 2f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        // ── 스킵 ─────────────────────────────────────────────────────
        void Skip()
        {
            if (_skipped) return;
            _skipped = true;

            _mainSeq?.Kill();
            _breathTween?.Kill();
            StopAllCoroutines();

            // 즉시 최종 상태로
            if (logoImage != null)
            {
                SetAlpha(logoImage, 1f);
                logoImage.rectTransform.anchoredPosition = logoTargetPos;
                logoImage.rectTransform.localScale = Vector3.one * logoTargetScale;
            }
            if (titleText != null)
            {
                SetAlpha(titleText, 1f);
                StartBreathLoop();
            }
        }

        // ── 유틸 ─────────────────────────────────────────────────────
        static void SetAlpha(Graphic g, float a)
        {
            if (g == null) return;
            var c = g.color; c.a = a; g.color = c;
        }
    }
}
