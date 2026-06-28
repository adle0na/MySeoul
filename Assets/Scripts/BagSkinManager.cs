using UnityEngine;
using UnityEngine.UI;

namespace SeoulLast
{
    /// <summary>
    /// 가방 레벨에 따라 XxBag 오브젝트 활성/비활성 제어.
    /// 슬롯은 레벨 1 이상 시 알파 255, 미해금 시 알파 50.
    ///
    /// 레벨 구조:
    ///   Lv1 (기본) : 22Bag ON
    ///   Lv2        : 33Bag ON  (EVT-U001 / BAG001)
    ///   Lv3        : 34Bag ON  (EVT-U002 / BAG002)
    ///   Lv4        : 54Bag ON  (EVT-U003 / BAG003)
    ///   Lv5 (최대) : 55Bag ON  (EVT-U004 / BAG004)
    /// </summary>
    public class BagSkinManager : MonoBehaviour
    {
        [Header("BagN 오브젝트 (Lv1~5 순서 : 22Bag, 33Bag, 34Bag, 54Bag, 55Bag)")]
        [SerializeField] GameObject[] bagObjects = new GameObject[5];

        [Header("슬롯 Image 배열 (BagPanel/Slots 하위, 자동 탐색)")]
        [SerializeField] Image[] slotImages;

        [Header("알파 설정 (0~255)")]
        [SerializeField] float unlockedAlpha = 255f;
        [SerializeField] float lockedAlpha   = 50f;

        int _currentLevel = 1;
        public int CurrentLevel => _currentLevel;

        void Awake()
        {
            if (bagObjects == null || bagObjects.Length < 5 || bagObjects[0] == null)
                AutoFindBagObjects();

            if (slotImages == null || slotImages.Length == 0)
                AutoFindSlotImages();

            Apply(_currentLevel);
        }

        // ── 공개 API ────────────────────────────────────────────────

        /// <summary>레벨 1 올리기. GameFlow에서 EVT-U / BAG 획득 시 호출.</summary>
        public void LevelUp()
        {
            if (_currentLevel >= 5)
            {
                Debug.Log("[BagSkinManager] 최대 레벨(Lv5) 도달.");
                return;
            }
            _currentLevel++;
            Apply(_currentLevel);
            Debug.Log($"[BagSkinManager] 가방 Lv{_currentLevel} 업그레이드");
        }

        /// <summary>세이브 데이터 복원 시 직접 레벨 지정.</summary>
        public void SetLevel(int level)
        {
            _currentLevel = Mathf.Clamp(level, 1, 5);
            Apply(_currentLevel);
        }

        // ── 내부 ────────────────────────────────────────────────────

        void Apply(int level)
        {
            int idx = Mathf.Clamp(level - 1, 0, 4);

            // XxBag 오브젝트: 현재 레벨만 ON, 나머지 OFF
            for (int i = 0; i < bagObjects.Length; i++)
            {
                if (bagObjects[i] == null) continue;
                bagObjects[i].SetActive(i == idx);
            }

            // 슬롯 알파: 해금(현재 레벨) → 255, 미해금 → 50
            ApplySlotAlpha(level);
        }

        void ApplySlotAlpha(int level)
        {
            if (slotImages == null) return;
            // 현재 구현: 레벨 1 이상이면 전체 슬롯 해금
            // 추후 BagModel.ActiveCells 기반 세분화 가능 (RefreshSlotAlpha 사용)
            float a = level >= 1 ? unlockedAlpha / 255f : lockedAlpha / 255f;
            foreach (var img in slotImages)
            {
                if (img == null) continue;
                var c = img.color;
                c.a = a;
                img.color = c;
            }
        }

        /// <summary>
        /// 슬롯 개별 알파 제어.
        /// GameFlow에서 BagModel의 활성 셀 배열을 넘기면 해금/잠금 세분화 가능.
        /// </summary>
        public void RefreshSlotAlpha(bool[] activeCells)
        {
            if (slotImages == null || activeCells == null) return;
            for (int i = 0; i < slotImages.Length && i < activeCells.Length; i++)
            {
                if (slotImages[i] == null) continue;
                var c = slotImages[i].color;
                c.a = activeCells[i] ? unlockedAlpha / 255f : lockedAlpha / 255f;
                slotImages[i].color = c;
            }
        }

        // ── 자동 탐색 ────────────────────────────────────────────────

        void AutoFindBagObjects()
        {
            string[] names = { "22Bag", "33Bag", "34Bag", "54Bag", "55Bag" };
            bagObjects = new GameObject[5];
            for (int i = 0; i < names.Length; i++)
            {
                var t = transform.Find(names[i]);
                if (t != null)
                    bagObjects[i] = t.gameObject;
                else
                    Debug.LogWarning($"[BagSkinManager] '{names[i]}' 오브젝트를 찾지 못했습니다.");
            }
        }

        void AutoFindSlotImages()
        {
            var slotsT = transform.Find("Slots");
            if (slotsT == null)
            {
                Debug.LogWarning("[BagSkinManager] 'Slots' 오브젝트를 찾지 못했습니다.");
                return;
            }
            slotImages = new Image[slotsT.childCount];
            for (int i = 0; i < slotsT.childCount; i++)
                slotImages[i] = slotsT.GetChild(i).GetComponent<Image>();
        }
    }
}
