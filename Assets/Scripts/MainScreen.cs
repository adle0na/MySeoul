using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SeoulLast
{
    // 메인화면 로직(참조 기반). UI는 씬에 미리 배치하고, 아래 필드에 연결한다.
    public class MainScreen : MonoBehaviour, IBagHost
    {
        [Header("상단")]
        [SerializeField] TextMeshProUGUI dayText;
        [SerializeField] GameObject backButton;

        [Header("지도뷰")]
        [SerializeField] TextMeshProUGUI departInfo;

        [Header("상태이상 (캐릭터 좌측 5개)")]
        [SerializeField] Image[] statusPills;
        [SerializeField] TextMeshProUGUI[] statusTexts;

        [Header("중앙 화면들 (서로 교체됨)")]
        [SerializeField] GameObject characterView;
        [SerializeField] GameObject lockerView;
        [SerializeField] GameObject mapView;
        [SerializeField] GameObject diaryView;
        [SerializeField] GameObject shopView;
        [SerializeField] GameObject statusView;

        [Header("인벤토리")]
        [SerializeField] GameObject bagArea;
        [SerializeField] RectTransform bagGridRect;
        [SerializeField] RectTransform storageRect;
        [SerializeField] RectTransform dragLayer;

        [Header("가방 설정")]
        [SerializeField] int bagWidth = 6;
        [SerializeField] int bagHeight = 6;
        [SerializeField] float bagCell = 110f;
        readonly Color cActiveSlot = new Color(0.83f, 0.78f, 0.67f);
        readonly Color cDimSlot = new Color(0.30f, 0.28f, 0.25f, 0.5f);

        int day = 1;
        readonly BagModel bag = new BagModel();
        readonly List<InvItemView> storageItems = new List<InvItemView>();

        public RectTransform BagGridRect => bagGridRect;
        public RectTransform BagDragLayer => dragLayer;
        public RectTransform StorageRect => storageRect;
        public RectTransform TrashRect => null;
        public BagModel Bag => bag;
        public bool LockerOpen { get; private set; }
        public float Cell => bagCell;

        public void Discard(InvItemView item)
        {
            if (item.InBag) bag.RemoveFromBag(item.Model);
            storageItems.Remove(item);
            Destroy(item.gameObject);
            LayoutStorage();
        }

        public void SelectItem(InvItemView item) { }

        void Start()
        {
            bag.Width = bagWidth;
            bag.Height = bagHeight;
            RefreshBagDim();
            PopulateStartingItems();
            GoHome();
        }

        public void UpgradeBag()
        {
            bag.Stage = Mathf.Min(bag.Stage + 1, 5);
            RefreshBagDim();
        }

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

        public void OpenLocker() => ShowCenter(lockerView, true);
        public void OpenMap() => ShowCenter(mapView, true);
        public void OpenDiary() => ShowCenter(diaryView, true);
        public void OpenShop() => ShowCenter(shopView, true);
        public void OpenStatus() => ShowCenter(statusView, true);
        public void GoHome() => ShowCenter(characterView, false);

        public string SelectedRoom { get; private set; }
        public System.Action<string> LocationChosen;
        public System.Action ExploreRequested;
        public System.Action<string> RoomSelected;

        public void SelectRoom(string room)
        {
            SelectedRoom = room;
            if (RoomSelected != null) RoomSelected(room);
        }

        public void Explore()
        {
            if (ExploreRequested != null) ExploreRequested();
        }

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

        public List<string> HeldItemNames()
        {
            var list = new List<string>();
            foreach (var p in bag.Placed) list.Add(p.Def.Name);
            return list;
        }

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

        void PopulateStartingItems()
        {
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
                { x = pad; y += rowH + pad; rowH = 0f; }
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
