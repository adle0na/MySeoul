using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulLast
{
    // 메인화면 로직(참조 기반). UI는 씬에 미리 배치하고, 아래 필드에 연결한다.
    // UI 생성은 더 이상 코드가 하지 않음 — 씬의 오브젝트를 참조만 한다.
    public class MainScreen : MonoBehaviour
    {
        [Header("상단")]
        [SerializeField] Text dayText;              // "DAY ??" 텍스트
        [SerializeField] GameObject backButton;     // 좌상단 '돌아가기' 버튼 (자동 show/hide)

        [Header("중앙 화면들 (서로 교체됨)")]
        [SerializeField] GameObject characterView;  // 기본(캐릭터)
        [SerializeField] GameObject lockerView;     // 사물함(창고)
        [SerializeField] GameObject mapView;        // 지도(학교)
        [SerializeField] GameObject diaryView;      // 일기
        [SerializeField] GameObject shopView;       // 상점
        [SerializeField] GameObject statusView;     // 캐릭터 상태

        [Header("인벤토리")]
        [SerializeField] RectTransform bagGridRect; // 하단 가방 격자 컨테이너 (pivot 좌상단)
        [SerializeField] RectTransform storageRect; // 창고 아이템 컨테이너 (사물함 화면 안)
        [SerializeField] RectTransform dragLayer;   // 드래그 중 아이템이 올라갈 최상단 레이어

        [Header("가방 설정")]
        [SerializeField] int bagWidth = 6;
        [SerializeField] int bagHeight = 5;
        [SerializeField] float bagCell = 135f;

        int day = 1;
        readonly BagModel bag = new BagModel();
        readonly List<InvItemView> storageItems = new List<InvItemView>();

        // InvItemView가 참조하는 접근자
        public RectTransform BagGridRect => bagGridRect;
        public RectTransform BagDragLayer => dragLayer;
        public RectTransform StorageRect => storageRect;
        public BagModel Bag => bag;
        public bool LockerOpen { get; private set; }

        void Start()
        {
            bag.Width = bagWidth;
            bag.Height = bagHeight;
            PopulateStartingItems();
            GoHome();
        }

        // ---------- 버튼 매핑용 public 메서드 (인스펙터 OnClick에서 선택) ----------
        public void OpenLocker() => ShowCenter(lockerView, true);   // 사물함(창고)
        public void OpenMap() => ShowCenter(mapView, true);         // 지도(학교 도면)
        public void OpenDiary() => ShowCenter(diaryView, true);     // 일기장
        public void OpenShop() => ShowCenter(shopView, true);       // 상점
        public void OpenStatus() => ShowCenter(statusView, true);   // 캐릭터 상태 (우상단)
        public void GoHome() => ShowCenter(characterView, false);   // 돌아가기 (캐릭터)

        // ---------- 화면 전환 ----------
        void ShowCenter(GameObject view, bool isSub)
        {
            if (characterView) characterView.SetActive(view == characterView);
            if (lockerView) lockerView.SetActive(view == lockerView);
            if (mapView) mapView.SetActive(view == mapView);
            if (diaryView) diaryView.SetActive(view == diaryView);
            if (shopView) shopView.SetActive(view == shopView);
            if (statusView) statusView.SetActive(view == statusView);
            if (backButton) backButton.SetActive(isSub);

            LockerOpen = (view == lockerView);
            if (LockerOpen) LayoutStorage();
        }

        // ---------- 인벤토리 ----------
        void PopulateStartingItems()
        {
            // 샘플 보유 아이템 (실제 ItemData 연동은 추후)
            string[] ids = { "flashlight", "lighter", "rope", "axe", "food", "medkit", "mask", "radio" };
            foreach (var id in ids)
            {
                var def = ItemDatabase.Get(id);
                if (def != null) CreateStorageItem(def);
            }
            LayoutStorage();
        }

        void CreateStorageItem(ItemDef def)
        {
            if (storageRect == null) return;
            var go = new GameObject("inv_" + def.Id, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(storageRect, false);
            var view = go.AddComponent<InvItemView>();
            view.Init(this, new PlacedItem(def), bagCell);
            storageItems.Add(view);
        }

        public void PlaceInBag(InvItemView item, Vector2Int origin)
        {
            bag.PlaceAt(item.Model, origin);
            item.InBag = true;
            storageItems.Remove(item);
            item.AttachToBag(origin);
            LayoutStorage();
        }

        public void MoveToStorage(InvItemView item)
        {
            if (item.InBag) { bag.RemoveFromBag(item.Model); item.InBag = false; }
            if (!storageItems.Contains(item)) storageItems.Add(item);
            item.transform.SetParent(storageRect, false);
            LayoutStorage();
        }

        public void ReturnToStorage(InvItemView item)
        {
            if (!storageItems.Contains(item)) storageItems.Add(item);
            item.transform.SetParent(storageRect, false);
            LayoutStorage();
        }

        // 창고 아이템을 좌->우, 위->아래로 줄바꿈 배치
        void LayoutStorage()
        {
            if (storageRect == null) return;
            float areaW = storageRect.rect.width;
            float pad = 14f, x = pad, y = pad, rowH = 0f;
            foreach (var it in storageItems)
            {
                if (it == null) continue;
                var irt = it.GetComponent<RectTransform>();
                float iw = it.Model.Def.Width * bagCell;
                float ih = it.Model.Def.Height * bagCell;
                if (x + iw > areaW - pad && x > pad)
                {
                    x = pad; y += rowH + pad; rowH = 0f;
                }
                irt.anchorMin = new Vector2(0, 1);
                irt.anchorMax = new Vector2(0, 1);
                irt.pivot = new Vector2(0, 1);
                irt.anchoredPosition = new Vector2(x, -y);
                x += iw + pad;
                if (ih > rowH) rowH = ih;
            }
        }

        public void SetDay(int d)
        {
            day = d;
            if (dayText != null) dayText.text = "DAY " + day;
        }
    }
}
