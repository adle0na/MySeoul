using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulLast
{
    /// <summary>
    /// Speed Wipe / Glitch 화면 전환 연출.
    /// Play() 호출 → 우→좌 고속 스윕으로 화면 장악 → onCovered 콜백(씬 교체 등)
    ///             → 좌측으로 빠져나감 → onComplete 콜백.
    /// </summary>
    public class ScreenSpeedTransition : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] RectTransform overlayRect;
        [SerializeField] RawImage      overlayImage;
        [SerializeField] Material      transitionMaterial;

        [Header("Timing (seconds)")]
        [SerializeField] float coverDuration = 0.22f;   // 덮는 시간
        [SerializeField] float holdDuration  = 0.07f;   // 완전히 덮인 시간
        [SerializeField] float clearDuration = 0.22f;   // 걷히는 시간

        [Header("Shader Params")]
        [SerializeField] float intensity = 1.8f;
        [SerializeField] float speed     = 8.0f;

        [Header("Options")]
        [SerializeField] bool useUnscaledTime = true;   // TimeScale 0 에서도 동작

        // 런타임 머티리얼 인스턴스
        Material _mat;
        Coroutine _seq;

        static readonly int PropProgress  = Shader.PropertyToID("_Progress");
        static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        static readonly int PropSpeed     = Shader.PropertyToID("_Speed");

        void Awake()
        {
            // 공유 머티리얼 수정 금지 → 인스턴스 생성
            if (transitionMaterial != null)
                _mat = Instantiate(transitionMaterial);

            if (overlayImage != null)
            {
                if (_mat != null) overlayImage.material = _mat;
                overlayImage.raycastTarget = false;
            }

            SetProgress(0f);  // Progress=0 = 완전 투명, 오브젝트는 항상 활성
        }

        // ── 공개 API ─────────────────────────────────────────────────
        /// <summary>
        /// 전환 연출을 재생한다.
        /// </summary>
        /// <param name="onCovered"> 화면이 완전히 가려진 순간 호출 </param>
        /// <param name="onComplete"> 연출 완전 종료 후 호출 </param>
        public void Play(Action onCovered = null, Action onComplete = null)
        {
            if (_seq != null) StopCoroutine(_seq);
            _seq = StartCoroutine(Sequence(onCovered, onComplete));
        }

        // ── 내부 ─────────────────────────────────────────────────────
        IEnumerator Sequence(Action onCovered, Action onComplete)
        {
            // 초기화
            SetProgress(0f);

            // ── 1단계: 덮기 (Progress 0→1) ──────────────────────────
            yield return Tween(0f, 1f, coverDuration, EaseOutExpo);

            // ── 2단계: 홀드 ──────────────────────────────────────────
            onCovered?.Invoke();
            yield return Wait(holdDuration);

            // ── 3단계: 걷히기 (Progress 1→0) ─────────────────────────
            yield return Tween(1f, 0f, clearDuration, EaseInExpo);

            // 종료
            SetProgress(0f);  // Progress=0 = 완전 투명, 오브젝트는 항상 활성
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
        static float EaseOutExpo(float t) =>
            t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);

        static float EaseInExpo(float t) =>
            t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);

#if UNITY_EDITOR
        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
#endif
    }
}
