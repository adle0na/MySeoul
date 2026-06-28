using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulLast
{
    /// <summary>
    /// Speed Wipe 화면 전환 연출.
    /// 덮기: 우→좌 스윕 (Progress 0→1)
    /// 걷히기: 덮기의 역재생 (Progress 1→0) → 좌측부터 사라져 우측이 마지막으로 나타남
    /// </summary>
    public class ScreenSpeedTransition : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] RectTransform overlayRect;
        [SerializeField] RawImage      overlayImage;
        [SerializeField] Material      transitionMaterial;

        [Header("Timing (seconds)")]
        [SerializeField] float coverDuration = 0.40f;   // 덮는 시간
        [SerializeField] float holdDuration  = 0.70f;   // 완전히 덮인 유지 시간
        [SerializeField] float clearDuration = 0.40f;   // 걷히는 시간 (역재생)

        [Header("Shader Params")]
        [SerializeField] float intensity = 1.8f;
        [SerializeField] float speed     = 8.0f;

        [Header("Options")]
        [SerializeField] bool useUnscaledTime = true;

        Material _mat;
        Coroutine _seq;

        static readonly int PropProgress  = Shader.PropertyToID("_Progress");
        static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        static readonly int PropSpeed     = Shader.PropertyToID("_Speed");

        void Awake()
        {
            if (transitionMaterial != null)
                _mat = Instantiate(transitionMaterial);

            if (overlayImage != null)
            {
                if (_mat != null) overlayImage.material = _mat;
                overlayImage.raycastTarget = false;
            }

            SetProgress(0f);
        }

        public void Play(Action onCovered = null, Action onComplete = null)
        {
            if (_seq != null) StopCoroutine(_seq);
            _seq = StartCoroutine(Sequence(onCovered, onComplete));
        }

        IEnumerator Sequence(Action onCovered, Action onComplete)
        {
            // ── 1단계: 덮기 우→좌 (Progress 0→1, EaseOutCubic) ──────
            SetProgress(0f);
            yield return Tween(0f, 1f, coverDuration, EaseOutCubic);

            // ── 2단계: 홀드 ──────────────────────────────────────────
            onCovered?.Invoke();
            yield return Wait(holdDuration);

            // ── 3단계: 걷히기 역재생 (Progress 1→0, EaseInCubic) ─────
            // Progress가 줄어들수록 좌측부터 사라짐 → 덮기의 역재생
            yield return Tween(1f, 0f, clearDuration, EaseInCubic);

            SetProgress(0f);
            _seq = null;
            onComplete?.Invoke();
        }

        IEnumerator Tween(float from, float to, float duration, Func<float, float> ease)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetProgress(Mathf.Lerp(from, to, ease(t)));
                yield return null;
            }
            SetProgress(to);
        }

        IEnumerator Wait(float duration)
        {
            if (useUnscaledTime)
            {
                float elapsed = 0f;
                while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; yield return null; }
            }
            else
                yield return new WaitForSeconds(duration);
        }

        void SetProgress(float p)
        {
            if (_mat == null) return;
            _mat.SetFloat(PropProgress,  p);
            _mat.SetFloat(PropIntensity, intensity);
            _mat.SetFloat(PropSpeed,     speed);
        }

        // ── Easing ───────────────────────────────────────────────────
        static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        static float EaseInCubic(float t)  => t * t * t;

#if UNITY_EDITOR
        void OnDestroy() { if (_mat != null) Destroy(_mat); }
#endif
    }
}
