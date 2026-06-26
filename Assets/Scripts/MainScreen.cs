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

        [Header("지도뷰")]
        [SerializeField] Text departInfo;           // 출발 버튼 위 지역 정보 텍스트

        [Header("상태이상 (캐릭터 좌측 5개)")]
        [SerializeField] Image[] statusPills;       // 5개 알약 배경
        [SerializeField] Text[] statusTexts;        // 5개 상태 이름

        [Header("중앙 화면들 (서로 교체됨)")]
        [SerializeField] GameObject characterView;  // 기본(캐릭터)
        [SerializeField] GameObject lockerView;     // 사물함(창고)
        [SerializeField] GameObject mapView;        // 지도(학교)
        [SerializeField] GameObject diaryView;      // 일기
        [SerializeField] GameObject shopView;       // 상점
        [SerializeField] GameObject statusView;     // 캐릭터 상태

        [Header("인벤토리")]
        [SerializeField] GameObject bagArea;        // 하단 가방 영역(라벨+격자) 묶음 - 가방정리 때만 표시
        [SerializeField] RectTransform bagGridRect; // 하단 가방 격자 컨테이너 (pivot 좌상단)
        [SerializeField] RectTransform storageRect; // 창고 아이템 컨테이너 (사물함 화면 안)
        [SerializeField] RectTransform dragLayer;   // 드래그 중 아이템이 올라갈 최상단 레이어

        [Header("가방 설정")]
        [SerializeField] int bagWidth = 6;
        [SerializeField] int bagHeight = 6;
        [SerializeField] float bagCell = 110f;
        readonly Color cActiveSlot = new Color(0.83f, 0.78f, 0.67f);
        readonly Color cDimSlot = new Color(0.30f, 0.28f, 0.25f, 0.5f);

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
            RefreshBagDim();
            PopulateStartingItems();
            GoHome();
        }

        // ---------- 가방 단계 / 딤드 ----------
        public void UpgradeBag()
        {
            bag.Stage = Mathf.Min(bag.Stage + 1, 5);
            RefreshBagDim();
        }

        // 활성 영역(중앙 NxN) 밖 칸을 어둡게 표시
        public void RefreshBagDim()
        {
            if (bagGridRect == null) return;
            int n = 0;
            for (int i = 0; i < bagGridRect.childCount; i++)
            {
                var ch = bagGridRect.GetChild(i);
                if (ch.name != "bagslot") continue;
                int x = n % bagWidth, y = n / bagWidth;
                n++;
                var img = ch.GetComponent<Image>();
                if (img != null) img.color = bag.IsActiveCell(new Vector2Int(x, y)) ? cActiveSlot : cDimSlot;
            }
        }

        // ---------- 상태이상 표시 ----------
        // level: 0 정상(숨김), 1 주의, 2 위험. 주의/위험만 위에서부터 모아 표시.
        public void SetStatuses(int[] levels, string[] labels)
        {
            if (statusPills == null) return;
            int slot = 0;
            for (int i = 0; i < statusPills.Length; i++)
            {
                var pill = statusPills[i];
                if (pill == null) continue;

                int lv = (levels != null && i < levels.Length) ? levels[i] : 0;
                if (lv <= 0) { pill.gameObject.SetActive(false); continue; }

                pill.gameObject.SetActive(true);
                pill.color = lv == 1 ? new Color(0.90f, 0.72f, 0.20f) : new Color(0.85f, 0.25f, 0.22f);
                if (statusTexts != null && i < statusTexts.Length && statusTexts[i] != null)
                    statusTexts[i].text = (labels != null && i < labels.Length) ? labels[i] : "";

                var rt = pill.rectTransform;
                rt.anchoredPosition = new Vector2(10, -(150 + slot * 64));
                slot++;
            }
        }

        // ---------- 버튼 매핑용 public 메서드 (인스펙터 OnClick에서 선택) ----------
        public void OpenLocker() => ShowCenter(lockerView, true);   // 사물함(창고)
        public void OpenMap() => ShowCenter(mapView, true);         // 지도(학교 도면)
        public void OpenDiary() => ShowCenter(diaryView, true);     // 일기장
        public void OpenShop() => ShowCenter(shopView, true);       // 상점
        public void OpenStatus() => ShowCenter(statusView, true);   // 캐릭터 상태 (우상단)
        public void GoHome() => ShowCenter(characterView, false);   // 돌아가기 (캐릭터)

        // ---------- 지역 선택 / 출발 (지도뷰 버튼에서 호출) ----------
        public string SelectedRoom { get; private set; }
        public System.Action<string> LocationChosen;   // 학교도면 [출발] → 가방정리로
        public System.Action ExploreRequested;          // 가방정리 [탐사] → 이벤트로
        public System.Action<string> RoomSelected;      // 방 선택 시 (정보 텍스트 갱신)

        public void SelectRoom(string room)
        {
            SelectedRoom = room;
            if (RoomSelected != null) RoomSelected(room);
        }

        public void Explore()
        {
            if (ExploreRequested != null) ExploreRequested();
        }

        // ---------- 흐름 모드 (GameFlow가 호출) ----------
        public void EnterMapMode()
        {
            HideCenterViews();
            if (mapView) mapView.SetActive(true);
            if (bagArea) bagArea.SetActive(false);
        }

        public void EnterOrganizeMode(string hint)
        {
            HideCenterViews();
            if (lockerView) lockerView.SetActive(true);
            if (bagArea) bagArea.SetActive(true);
            LockerOpen = true;
            LayoutStorage();
            SetDepartInfo(hint);
        }

        void HideCenterViews()
        {
            if (characterView) characterView.SetActive(false);
            if (lockerView) lockerView.SetActive(false);
            if (mapView) mapView.SetActive(false);
            if (diaryView) diaryView.SetActive(false);
            if (shopView) shopView.SetActive(false);
            if (statusView) statusView.SetActive(false);
            LockerOpen = false;
        }

        public void SetDepartInfo(string text)
        {
            if (departInfo != null) departInfo.text = text;
        }

        public void Depart()
        {
            if (string.IsNullOrEmpty(SelectedRoom)) return;
            if (LocationChosen != null) LocationChosen(SelectedRoom);
        }

        // 가방에 담은(=가져갈) 아이템 이름 목록
        public List<string> HeldItemNames()
        {
            var list = new List<string>();
            foreach (var p in bag.Placed) list.Add(p.Def.Name);
            return list;
        }

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
