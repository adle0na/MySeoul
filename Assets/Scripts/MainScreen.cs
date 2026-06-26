using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SeoulLast
{
    // 새 메인화면 셸: 상단(DAY/돌아가기/상태보기) + 중앙 스왑영역 + 하단 가방(6x5) + 최하단 4버튼.
    // 중앙 영역은 캐릭터(기본) ↔ 사물함/지도/일기/상점/상태 로 교체된다.
    public class MainScreen : MonoBehaviour
    {
        const float W = 1080f, H = 1920f;

        // 임시 학교 도면 (실제 데이터 없음 → 더미)
        static readonly string[] Rooms =
        {
            "음악실", "과학실", "1-1반", "1-2반", "2-1반",
            "2-2반", "급식실", "체육관", "도서관", "교무실"
        };

        readonly Color cBg = new Color(0.93f, 0.90f, 0.83f);     // 종이톤 배경
        readonly Color cPanel = new Color(0.88f, 0.84f, 0.74f);
        readonly Color cSlot = new Color(0.83f, 0.78f, 0.67f);
        readonly Color cInk = new Color(0.27f, 0.23f, 0.18f);
        readonly Color cBrown = new Color(0.55f, 0.40f, 0.28f);
        readonly Color cRoom = new Color(0.60f, 0.70f, 0.55f);

        int day = 1;
        const float BAG_CELL = 135f;

        RectTransform root, centerArea;
        GameObject characterView, lockerView, mapView, diaryView, shopView, statusView;
        Button backBtn;
        Text dayText;

        // 인벤토리 (창고 <-> 가방)
        readonly BagModel bag = new BagModel();
        readonly List<InvItemView> storageItems = new List<InvItemView>();
        public RectTransform BagGridRect;
        public RectTransform BagDragLayer;
        public RectTransform StorageRect;
        public BagModel Bag => bag;
        public bool LockerOpen { get; private set; }

        void Start()
        {
            EnsureEventSystem();
            BuildUI();
            PopulateStartingItems();
            ShowCenter(characterView, false);
        }

        void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            var mod = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (mod != null) es.AddComponent(mod);
            else es.AddComponent<StandaloneInputModule>();
        }

        void BuildUI()
        {
            var canvasGO = new GameObject("MainCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0.5f;
            root = canvas.GetComponent<RectTransform>();

            UIFactory.Panel(root, "Background", cBg);

            BuildTopBar();
            BuildCenterArea();
            BuildBag();
            BuildNav();

            // 드래그 레이어 (최상단)
            var dl = new GameObject("DragLayer", typeof(RectTransform));
            BagDragLayer = dl.GetComponent<RectTransform>();
            BagDragLayer.SetParent(root, false);
            UIFactory.Fill(BagDragLayer);
            BagDragLayer.SetAsLastSibling();
        }

        // ---------- 상단 ----------
        void BuildTopBar()
        {
            var bar = UIFactory.Panel(root, "TopBar", new Color(0.85f, 0.81f, 0.71f));
            UIFactory.SetRect(bar.rectTransform, 0, 0, W, 110);

            dayText = UIFactory.Label(bar.transform, "Day", "DAY 1", 46, TextAnchor.MiddleCenter, cInk);
            UIFactory.SetRect(dayText.rectTransform, 340, 25, 400, 60);

            Text statusLbl;
            var status = UIFactory.Button(bar.transform, "StatusBtn", "상태보기", cBrown, () => ShowCenter(statusView, true), out statusLbl);
            statusLbl.fontSize = 28;
            UIFactory.SetRect(status.GetComponent<RectTransform>(), W - 210, 25, 185, 62);

            Text backLbl;
            backBtn = UIFactory.Button(bar.transform, "BackBtn", "← 돌아가기", new Color(0.5f, 0.45f, 0.38f), () => ShowCenter(characterView, false), out backLbl);
            backLbl.fontSize = 26;
            UIFactory.SetRect(backBtn.GetComponent<RectTransform>(), 20, 25, 175, 62);
        }

        // ---------- 중앙 스왑 영역 ----------
        void BuildCenterArea()
        {
            var areaGO = new GameObject("CenterArea", typeof(RectTransform));
            centerArea = areaGO.GetComponent<RectTransform>();
            centerArea.SetParent(root, false);
            UIFactory.SetRect(centerArea, 40, 130, W - 80, 740);

            characterView = BuildCharacterView();
            lockerView = BuildLockerView();
            mapView = BuildMapView();
            diaryView = BuildDiaryView();
            shopView = BuildShopView();
            statusView = BuildStatusView();
        }

        RectTransform CenterPanel(string name, Color bg)
        {
            var p = UIFactory.Panel(centerArea, name, bg);
            UIFactory.Fill(p.rectTransform);
            p.gameObject.SetActive(false);
            return p.rectTransform;
        }

        GameObject BuildCharacterView()
        {
            var v = CenterPanel("CharacterView", new Color(0.80f, 0.83f, 0.80f)); // 방 배경 톤

            // 간이 캐릭터(로봇) 플레이스홀더
            var body = UIFactory.Img(v, "Body", new Color(0.92f, 0.93f, 0.95f));
            UIFactory.SetRect(body.rectTransform, (W - 80) / 2 - 150, 230, 300, 320);
            var head = UIFactory.Img(v, "Head", new Color(0.95f, 0.96f, 0.98f));
            UIFactory.SetRect(head.rectTransform, (W - 80) / 2 - 110, 120, 220, 180);
            var eyeL = UIFactory.Img(head.transform, "EyeL", new Color(0.25f, 0.85f, 0.85f));
            UIFactory.SetRect(eyeL.rectTransform, 45, 70, 40, 40);
            var eyeR = UIFactory.Img(head.transform, "EyeR", new Color(0.25f, 0.85f, 0.85f));
            UIFactory.SetRect(eyeR.rectTransform, 135, 70, 40, 40);

            var lbl = UIFactory.Label(v, "Lbl", "캐릭터 이미지 위치", 28, TextAnchor.LowerCenter, new Color(0.3f, 0.3f, 0.3f, 0.7f));
            UIFactory.SetRect(lbl.rectTransform, 0, 680, W - 80, 50);
            return v.gameObject;
        }

        GameObject BuildLockerView()
        {
            var v = CenterPanel("LockerView", cPanel);
            var title = UIFactory.Label(v, "T", "사물함 — 창고", 34, TextAnchor.UpperCenter, cInk);
            UIFactory.SetRect(title.rectTransform, 0, 24, W - 80, 50);
            var hint = UIFactory.Label(v, "H", "아래 가방으로 끌어 담거나, 가방에서 여기로 끌어 보관하세요.", 22, TextAnchor.UpperCenter, new Color(0.4f, 0.36f, 0.3f));
            UIFactory.SetRect(hint.rectTransform, 0, 82, W - 80, 40);

            // 창고 아이템 컨테이너 (top-left 기준)
            var sgo = new GameObject("Storage", typeof(RectTransform), typeof(Image));
            var sImg = sgo.GetComponent<Image>();
            sImg.sprite = UIFactory.White();
            sImg.color = new Color(0.83f, 0.78f, 0.67f, 0.5f);
            StorageRect = sgo.GetComponent<RectTransform>();
            StorageRect.SetParent(v, false);
            UIFactory.SetRect(StorageRect, 20, 130, W - 120, 560);
            return v.gameObject;
        }

        GameObject BuildMapView()
        {
            var v = CenterPanel("MapView", cPanel);
            var title = UIFactory.Label(v, "T", "지도 — 학교 (임시 도면)", 34, TextAnchor.UpperCenter, cInk);
            UIFactory.SetRect(title.rectTransform, 0, 24, W - 80, 50);

            // 10개 방을 2열로
            float bw = 430, bh = 96, gap = 18, x0 = 60, y0 = 100;
            for (int i = 0; i < Rooms.Length; i++)
            {
                int cx = i % 2, cy = i / 2;
                string room = Rooms[i];
                Text rl;
                var b = UIFactory.Button(v, "room_" + room, room, cRoom, () => Debug.Log("[Map] 선택: " + room), out rl);
                rl.fontSize = 30;
                UIFactory.SetRect(b.GetComponent<RectTransform>(), x0 + cx * (bw + gap), y0 + cy * (bh + gap), bw, bh);
            }
            return v.gameObject;
        }

        GameObject BuildDiaryView()
        {
            var v = CenterPanel("DiaryView", new Color(0.95f, 0.93f, 0.85f));
            var title = UIFactory.Label(v, "T", "일기장", 38, TextAnchor.UpperCenter, cInk);
            UIFactory.SetRect(title.rectTransform, 0, 30, W - 80, 60);
            var body = UIFactory.Label(v, "B", "(일기 내용은 아직 준비 중입니다)\n\n오늘의 기록...", 28, TextAnchor.UpperLeft, new Color(0.35f, 0.30f, 0.25f));
            UIFactory.SetRect(body.rectTransform, 60, 130, W - 200, 520);
            return v.gameObject;
        }

        GameObject BuildShopView()
        {
            var v = CenterPanel("ShopView", cPanel);
            var title = UIFactory.Label(v, "T", "상점 (준비 중)", 36, TextAnchor.UpperCenter, cInk);
            UIFactory.SetRect(title.rectTransform, 0, 30, W - 80, 56);
            float cell = 200, gap = 24, gx = 70, gy = 120;
            for (int i = 0; i < 6; i++)
            {
                int cx = i % 3, cy = i / 3;
                var slot = UIFactory.Img(v, "buy", cSlot);
                UIFactory.SetRect(slot.rectTransform, gx + cx * (cell + gap), gy + cy * (cell + gap), cell, cell);
                var pl = UIFactory.Label(slot.transform, "p", "??? G", 24, TextAnchor.LowerCenter, cInk);
                UIFactory.Fill(pl.rectTransform);
            }
            return v.gameObject;
        }

        GameObject BuildStatusView()
        {
            var v = CenterPanel("StatusView", cPanel);
            var title = UIFactory.Label(v, "T", "캐릭터 상태", 38, TextAnchor.UpperCenter, cInk);
            UIFactory.SetRect(title.rectTransform, 0, 30, W - 80, 60);

            string[] stats = { "체력", "배고픔", "정신력", "체온", "청결" };
            int[] vals = { 80, 55, 70, 90, 40 };
            float y = 130;
            for (int i = 0; i < stats.Length; i++)
            {
                var nl = UIFactory.Label(v, "s", stats[i], 28, TextAnchor.MiddleLeft, cInk);
                UIFactory.SetRect(nl.rectTransform, 70, y, 180, 60);
                var barBg = UIFactory.Img(v, "bg", cSlot);
                UIFactory.SetRect(barBg.rectTransform, 260, y + 8, 620, 44);
                var fill = UIFactory.Img(barBg.transform, "fill", cRoom);
                UIFactory.SetRect(fill.rectTransform, 0, 0, 620 * vals[i] / 100f, 44);
                y += 96;
            }
            return v.gameObject;
        }

        // ---------- 하단 가방 (6x5, 인터랙티브) ----------
        void BuildBag()
        {
            var lbl = UIFactory.Label(root, "BagLabel", "가방", 26, TextAnchor.UpperLeft, cInk);
            UIFactory.SetRect(lbl.rectTransform, 135, 885, 200, 40);

            float gw = bag.Width * BAG_CELL, gh = bag.Height * BAG_CELL;
            float gx = (W - gw) / 2f, gy = 925f;

            var frame = UIFactory.Panel(root, "BagFrame", new Color(0.78f, 0.72f, 0.60f));
            UIFactory.SetRect(frame.rectTransform, gx - 8, gy - 8, gw + 16, gh + 16);

            var gridGO = new GameObject("BagGrid", typeof(RectTransform));
            BagGridRect = gridGO.GetComponent<RectTransform>();
            BagGridRect.SetParent(root, false);
            UIFactory.SetRect(BagGridRect, gx, gy, gw, gh);

            for (int yy = 0; yy < bag.Height; yy++)
                for (int xx = 0; xx < bag.Width; xx++)
                {
                    var slot = UIFactory.Img(BagGridRect, "bagslot", cSlot);
                    slot.raycastTarget = false;
                    var srt = slot.rectTransform;
                    srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(0, 1); srt.pivot = new Vector2(0, 1);
                    srt.sizeDelta = new Vector2(BAG_CELL - 8, BAG_CELL - 8);
                    srt.anchoredPosition = new Vector2(xx * BAG_CELL + 4, -(yy * BAG_CELL + 4));
                }
        }

        // ---------- 최하단 네비 ----------
        void BuildNav()
        {
            string[] names = { "사물함", "지도", "일기", "상점" };
            System.Action[] acts =
            {
                () => ShowCenter(lockerView, true),
                () => ShowCenter(mapView, true),
                () => ShowCenter(diaryView, true),
                () => ShowCenter(shopView, true),
            };
            float bw = 245, gap = 20, y = 1640, h = 200;
            float x = 20;
            for (int i = 0; i < 4; i++)
            {
                var act = acts[i];
                Text nl;
                var b = UIFactory.Button(root, "Nav_" + names[i], names[i], cBrown, () => act(), out nl);
                nl.fontSize = 34;
                UIFactory.SetRect(b.GetComponent<RectTransform>(), x, y, bw, h);
                x += bw + gap;
            }
        }

        // ---------- 화면 전환 ----------
        void ShowCenter(GameObject view, bool isSub)
        {
            characterView.SetActive(view == characterView);
            lockerView.SetActive(view == lockerView);
            mapView.SetActive(view == mapView);
            diaryView.SetActive(view == diaryView);
            shopView.SetActive(view == shopView);
            statusView.SetActive(view == statusView);
            backBtn.gameObject.SetActive(isSub);

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
            var go = new GameObject("inv_" + def.Id, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(StorageRect, false);
            var view = go.AddComponent<InvItemView>();
            view.Init(this, new PlacedItem(def), BAG_CELL);
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
            item.transform.SetParent(StorageRect, false);
            LayoutStorage();
        }

        public void ReturnToStorage(InvItemView item)
        {
            if (!storageItems.Contains(item)) storageItems.Add(item);
            item.transform.SetParent(StorageRect, false);
            LayoutStorage();
        }

        // 창고 아이템을 좌->우, 위->아래로 줄바꿈 배치
        void LayoutStorage()
        {
            if (StorageRect == null) return;
            float areaW = StorageRect.rect.width;
            float pad = 14f, x = pad, y = pad, rowH = 0f;
            foreach (var it in storageItems)
            {
                if (it == null) continue;
                var irt = it.GetComponent<RectTransform>();
                float iw = it.Model.Def.Width * BAG_CELL;
                float ih = it.Model.Def.Height * BAG_CELL;
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
