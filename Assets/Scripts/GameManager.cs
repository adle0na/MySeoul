using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SeoulLast
{
    // 서울 라스트 프로토타입: UI를 코드로 생성하고 게임 루프(Pack→Map→Event→Organize→Ending)를 구동.
    public class GameManager : MonoBehaviour
    {
        // 레이아웃 상수 (1080x1920 세로)
        const float W = 1080f, H = 1920f;
        const float CELL = 130f;       // 한 칸 픽셀 (6x5 격자가 화면에 들어가도록)
        const float GridTop = 290f;    // 격자 상단 Y
        const float MapX0 = 50f, MapY0 = 290f, MapW = 980f, MapH = 900f; // 지도 보드 영역
        readonly Color cBg = new Color(0.11f, 0.12f, 0.15f);
        readonly Color cPanel = new Color(0.14f, 0.15f, 0.19f);
        readonly Color cBlue = new Color(0.22f, 0.45f, 0.78f);
        readonly Color cGrey = new Color(0.30f, 0.32f, 0.36f);
        readonly Color cGreen = new Color(0.25f, 0.62f, 0.42f);
        readonly Color cRed = new Color(0.66f, 0.26f, 0.28f);

        // 모델
        GameState state;
        EventDef currentEvent;
        bool initialPack;
        readonly List<DraggableItem> trayItems = new List<DraggableItem>();

        public BagModel Bag => state.Bag;

        // 코드 생성 UI 참조 (DraggableItem이 사용)
        public RectTransform DragLayer;
        public RectTransform GridRect;
        public RectTransform TrayRect;
        public RectTransform TrashRect;

        // 패널
        RectTransform root;
        GameObject mapPanel, eventPanel, organizePanel, endingPanel;
        RectTransform mapList;
        Text dayText, hpText;
        Text eventTitle, eventBody, eventBtnLabel;
        Button eventBtn;
        Text organizeNextLabel;
        Text endingTitle, endingBody;

        void Start()
        {
            EnsureEventSystem();
            state = new GameState();
            BuildUI();
            NewGame();
        }

        // ---------- UI 구성 ----------

        void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            var moduleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null) es.AddComponent(moduleType);
            else es.AddComponent<StandaloneInputModule>();
        }

        void BuildUI()
        {
            var canvasGO = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0.5f;
            root = canvas.GetComponent<RectTransform>();

            UIFactory.Panel(root, "Background", cBg);

            BuildMapPanel();
            BuildEventPanel();
            BuildOrganizePanel();
            BuildEndingPanel();
            BuildHud();

            // 드래그 레이어는 최상단
            var dl = new GameObject("DragLayer", typeof(RectTransform));
            DragLayer = dl.GetComponent<RectTransform>();
            DragLayer.SetParent(root, false);
            UIFactory.Fill(DragLayer);
            DragLayer.SetAsLastSibling();
        }

        void BuildHud()
        {
            var bar = UIFactory.Panel(root, "Hud", new Color(0.07f, 0.08f, 0.10f));
            UIFactory.SetRect(bar.rectTransform, 0, 0, W, 110);
            dayText = UIFactory.Label(bar.transform, "Day", "DAY 1 / 7", 40, TextAnchor.MiddleLeft, Color.white);
            UIFactory.SetRect(dayText.rectTransform, 40, 25, 500, 60);
            hpText = UIFactory.Label(bar.transform, "Hp", "HP 100", 40, TextAnchor.MiddleRight, new Color(0.95f, 0.55f, 0.55f));
            UIFactory.SetRect(hpText.rectTransform, W - 540, 25, 500, 60);
        }

        void BuildMapPanel()
        {
            mapPanel = UIFactory.Panel(root, "MapPanel", cBg).gameObject;
            var title = UIFactory.Label(mapPanel.transform, "Title", "어디로 이동할까?", 46, TextAnchor.MiddleCenter, Color.white);
            UIFactory.SetRect(title.rectTransform, 60, 130, W - 120, 70);
            var sub = UIFactory.Label(mapPanel.transform, "Sub", "파란 지역만 이동할 수 있습니다. (서울 25개 구)", 24, TextAnchor.MiddleCenter, new Color(0.7f, 0.72f, 0.78f));
            UIFactory.SetRect(sub.rectTransform, 60, 205, W - 120, 46);

            // 지도 보드 배경
            var board = UIFactory.Panel(mapPanel.transform, "Board", new Color(0.16f, 0.18f, 0.22f));
            UIFactory.SetRect(board.rectTransform, MapX0 - 24, MapY0 - 24, MapW + 48, MapH + 48);
            board.raycastTarget = false;

            // 한강 띠
            var river = UIFactory.Img(mapPanel.transform, "Han", new Color(0.24f, 0.44f, 0.70f, 0.55f));
            UIFactory.SetRect(river.rectTransform, MapX0, MapY0 + MapH * 0.48f, MapW, 64);
            river.raycastTarget = false;
            var hanLbl = UIFactory.Label(mapPanel.transform, "HanL", "한 강", 24, TextAnchor.MiddleCenter, new Color(0.82f, 0.9f, 1f, 0.85f));
            UIFactory.SetRect(hanLbl.rectTransform, MapX0 + MapW * 0.5f - 80, MapY0 + MapH * 0.48f + 14, 160, 40);
            hanLbl.raycastTarget = false;

            var listGO = new GameObject("List", typeof(RectTransform));
            mapList = listGO.GetComponent<RectTransform>();
            mapList.SetParent(mapPanel.transform, false);
            UIFactory.Fill(mapList);
        }

        void BuildEventPanel()
        {
            eventPanel = UIFactory.Panel(root, "EventPanel", cBg).gameObject;
            eventTitle = UIFactory.Label(eventPanel.transform, "Title", "", 50, TextAnchor.MiddleCenter, new Color(0.95f, 0.85f, 0.5f));
            UIFactory.SetRect(eventTitle.rectTransform, 60, 160, W - 120, 80);

            var box = UIFactory.Panel(eventPanel.transform, "Box", cPanel);
            UIFactory.SetRect(box.rectTransform, 80, 290, W - 160, 760);
            eventBody = UIFactory.Label(box.transform, "Body", "", 34, TextAnchor.UpperLeft, new Color(0.92f, 0.93f, 0.96f));
            UIFactory.SetRect(eventBody.rectTransform, 40, 40, W - 240, 680);

            eventBtn = UIFactory.Button(eventPanel.transform, "EventBtn", "조사한다", cBlue, null, out eventBtnLabel);
            UIFactory.SetRect(eventBtn.GetComponent<RectTransform>(), 240, 1180, W - 480, 150);
        }

        void BuildOrganizePanel()
        {
            organizePanel = UIFactory.Panel(root, "OrganizePanel", cBg).gameObject;
            var title = UIFactory.Label(organizePanel.transform, "Title", "가방을 정리하고 떠나라", 44, TextAnchor.MiddleCenter, Color.white);
            UIFactory.SetRect(title.rectTransform, 60, 140, W - 120, 70);
            var sub = UIFactory.Label(organizePanel.transform, "Sub", "아이템을 격자에 끌어다 놓으세요. 칸에 안 들어가면 두고 가야 합니다.", 24, TextAnchor.MiddleCenter, new Color(0.7f, 0.72f, 0.78f));
            UIFactory.SetRect(sub.rectTransform, 60, 210, W - 120, 60);

            // 격자 배경 + 컨테이너 (가방 크기 기반으로 계산)
            float gw = state.Bag.Width * CELL;
            float gh = state.Bag.Height * CELL;
            float gx = (W - gw) / 2f;

            var gridFrame = UIFactory.Panel(organizePanel.transform, "GridFrame", new Color(0.08f, 0.09f, 0.11f));
            UIFactory.SetRect(gridFrame.rectTransform, gx - 8, GridTop - 8, gw + 16, gh + 16);

            var gridGO = new GameObject("Grid", typeof(RectTransform));
            GridRect = gridGO.GetComponent<RectTransform>();
            GridRect.SetParent(organizePanel.transform, false);
            UIFactory.SetRect(GridRect, gx, GridTop, gw, gh);
            BuildGridBackground();

            float trayTop = GridTop + gh + 50f;   // 격자 아래

            // 트레이
            var trayLabel = UIFactory.Label(organizePanel.transform, "TrayLabel", "획득한 물건", 24, TextAnchor.UpperLeft, new Color(0.7f, 0.72f, 0.78f));
            UIFactory.SetRect(trayLabel.rectTransform, 55, trayTop - 35, 400, 40);
            var trayBg = UIFactory.Panel(organizePanel.transform, "TrayBg", new Color(0.09f, 0.10f, 0.13f));
            UIFactory.SetRect(trayBg.rectTransform, 40, trayTop, W - 80, 290);

            var trayGO = new GameObject("Tray", typeof(RectTransform));
            TrayRect = trayGO.GetComponent<RectTransform>();
            TrayRect.SetParent(organizePanel.transform, false);
            UIFactory.SetRect(TrayRect, 40, trayTop + 10, W - 80, 270);

            float bottomTop = trayTop + 330f;

            // 버리기
            var trash = UIFactory.Panel(organizePanel.transform, "Trash", cRed);
            UIFactory.SetRect(trash.rectTransform, 60, bottomTop, 360, 150);
            TrashRect = trash.rectTransform;
            var tl = UIFactory.Label(trash.transform, "L", "여기로 끌어\n버리기", 28, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Fill(tl.rectTransform);
            tl.raycastTarget = false;

            // 다음 날 / 출발
            var next = UIFactory.Button(organizePanel.transform, "NextBtn", "다음 날", cGreen, OnOrganizeNext, out organizeNextLabel);
            UIFactory.SetRect(next.GetComponent<RectTransform>(), W - 60 - 460, bottomTop, 460, 150);
        }

        void BuildGridBackground()
        {
            for (int y = 0; y < state.Bag.Height; y++)
                for (int x = 0; x < state.Bag.Width; x++)
                {
                    var img = UIFactory.Img(GridRect, "bg", new Color(1, 1, 1, 0.06f));
                    img.raycastTarget = false;
                    var rt = img.rectTransform;
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0, 1);
                    rt.sizeDelta = new Vector2(CELL - 6, CELL - 6);
                    rt.anchoredPosition = new Vector2(x * CELL + 3, -(y * CELL + 3));
                }
        }

        void BuildEndingPanel()
        {
            endingPanel = UIFactory.Panel(root, "EndingPanel", new Color(0.06f, 0.06f, 0.08f)).gameObject;
            endingTitle = UIFactory.Label(endingPanel.transform, "Title", "", 70, TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.6f));
            UIFactory.SetRect(endingTitle.rectTransform, 60, 480, W - 120, 130);
            endingBody = UIFactory.Label(endingPanel.transform, "Body", "", 36, TextAnchor.UpperCenter, new Color(0.9f, 0.91f, 0.94f));
            UIFactory.SetRect(endingBody.rectTransform, 120, 660, W - 240, 480);
            Text restartLabel;
            var restart = UIFactory.Button(endingPanel.transform, "Restart", "다시 시작", cBlue, NewGame, out restartLabel);
            UIFactory.SetRect(restart.GetComponent<RectTransform>(), (W - 480) / 2, 1300, 480, 150);
        }

        // ---------- 게임 루프 ----------

        void NewGame()
        {
            // 정리
            foreach (var di in GridRect.GetComponentsInChildren<DraggableItem>(true)) Destroy(di.gameObject);
            foreach (var it in trayItems) if (it != null) Destroy(it.gameObject);
            trayItems.Clear();

            state = new GameState();
            currentEvent = null;

            AddTrayItem(ItemDatabase.Get("flashlight"));
            AddTrayItem(ItemDatabase.Get("lighter"));
            AddTrayItem(ItemDatabase.Get("rope"));
            LayoutTray();

            ShowOrganize(true);
        }

        void ShowMap()
        {
            SetPhase(mapPanel);
            UpdateHud();
            foreach (Transform c in mapList) Destroy(c.gameObject);

            // 2패스: 비활성 구(아래) 먼저, 활성 구(위)를 나중에 그려 위로 올림
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var d in SeoulMap.Districts)
                {
                    var ev = EventDatabase.Events.FirstOrDefault(e => e.Region == d.Name);
                    bool active = ev != null;
                    if (active != (pass == 1)) continue;

                    bool visited = active && state.Visited.Contains(d.Name);
                    Color col = !active ? new Color(0.20f, 0.21f, 0.25f)
                              : visited ? cGrey : cBlue;
                    string text = visited ? d.Name + "\n(방문)" : d.Name;

                    var captured = ev;
                    Text lbl;
                    var btn = UIFactory.Button(mapList, "tile_" + d.Name, text, col,
                        (active && !visited) ? (UnityEngine.Events.UnityAction)(() => OnRegion(captured)) : null,
                        out lbl);
                    lbl.fontSize = active ? 22 : 17;
                    lbl.color = active ? Color.white : new Color(0.55f, 0.57f, 0.62f);

                    float w = active ? 138f : 118f;
                    float h = active ? 82f : 64f;
                    float cx = MapX0 + d.Nx * MapW;
                    float cy = MapY0 + d.Ny * MapH;
                    UIFactory.SetRect(btn.GetComponent<RectTransform>(), cx - w / 2f, cy - h / 2f, w, h);
                    btn.interactable = active && !visited;
                }
            }
        }

        void OnRegion(EventDef ev)
        {
            currentEvent = ev;
            state.Visited.Add(ev.Region);

            SetPhase(eventPanel);
            eventTitle.text = ev.Region;
            eventBody.text = ev.Intro;
            UpdateHud();
            SetButton(eventBtn, eventBtnLabel, "조사한다", ResolveEvent);
        }

        void ResolveEvent()
        {
            bool success = state.Bag.Contains(currentEvent.RequiredItemId);
            int delta = success ? currentEvent.HpOnSuccess : currentEvent.HpOnFail;
            state.Hp = Mathf.Clamp(state.Hp + delta, 0, 100);

            string body = success ? currentEvent.SuccessText : currentEvent.FailText;
            if (success && !string.IsNullOrEmpty(currentEvent.RewardOnSuccess))
            {
                var rdef = ItemDatabase.Get(currentEvent.RewardOnSuccess);
                if (rdef != null) { AddTrayItem(rdef); LayoutTray(); }
            }

            string hpLine = delta != 0
                ? $"\n\n<color=#ff8888>체력 {(delta > 0 ? "+" : "")}{delta}</color>  (현재 {state.Hp})"
                : $"\n\n(현재 체력 {state.Hp})";
            eventBody.text = body + hpLine;
            UpdateHud();

            if (state.Hp <= 0)
                SetButton(eventBtn, eventBtnLabel, "쓰러진다...", () => ShowEnding(true));
            else
                SetButton(eventBtn, eventBtnLabel, "가방 정리", () => ShowOrganize(false));
        }

        void ShowOrganize(bool initial)
        {
            initialPack = initial;
            SetPhase(organizePanel);
            organizeNextLabel.text = initial ? "탐색 시작" : "다음 날";
            LayoutTray();
            UpdateHud();
        }

        void OnOrganizeNext()
        {
            // 트레이에 남은(못 넣은) 아이템은 두고 간다
            foreach (var it in trayItems.ToList())
                if (it != null) Destroy(it.gameObject);
            trayItems.Clear();

            if (initialPack) { initialPack = false; ShowMap(); return; }

            if (state.Visited.Count >= EventDatabase.Events.Count)
                ShowEnding(false);
            else
                ShowMap();
        }

        void ShowEnding(bool dead)
        {
            SetPhase(endingPanel);
            if (dead)
            {
                endingTitle.text = "사망";
                endingTitle.color = new Color(0.9f, 0.4f, 0.4f);
                endingBody.text = "당신은 서울을 빠져나가지 못했다.\n챙긴 것만으로는... 부족했다.\n\n혼자 남은 도시에 또 하나의 흔적이 사라졌다.";
            }
            else if (state.Bag.Contains("radio"))
            {
                endingTitle.text = "구조";
                endingTitle.color = new Color(0.5f, 0.9f, 0.6f);
                endingBody.text = "무전기로 구조 신호를 보냈다.\n며칠 뒤, 헬기가 폐허가 된 서울 위로 날아와 당신을 데려갔다.\n\n당신은 가져갈 것을 정확히 알았다. — 최고의 결말.";
            }
            else
            {
                endingTitle.text = "생존";
                endingTitle.color = new Color(0.95f, 0.9f, 0.6f);
                endingBody.text = "당신은 마지막 산길을 넘어 서울을 빠져나왔다.\n혼자였지만, 살아남았다.\n\n언젠가 다른 생존자를 만날 수 있을까?";
            }
        }

        // ---------- 헬퍼 ----------

        DraggableItem AddTrayItem(ItemDef def)
        {
            if (def == null) return null;
            var go = new GameObject("item_" + def.Id, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(TrayRect, false);
            var di = go.AddComponent<DraggableItem>();
            di.Init(this, new PlacedItem(def), CELL);
            trayItems.Add(di);
            return di;
        }

        public void RemoveTrayItem(DraggableItem item)
        {
            trayItems.Remove(item);
        }

        public void LayoutTray()
        {
            float x = 20;
            foreach (var it in trayItems)
            {
                if (it == null) continue;
                var rt = it.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(x, -15);
                x += it.Model.Def.Width * CELL + 25;
            }
        }

        void SetButton(Button b, Text label, string text, UnityEngine.Events.UnityAction cb)
        {
            b.onClick.RemoveAllListeners();
            label.text = text;
            if (cb != null) b.onClick.AddListener(cb);
        }

        void SetPhase(GameObject active)
        {
            mapPanel.SetActive(active == mapPanel);
            eventPanel.SetActive(active == eventPanel);
            organizePanel.SetActive(active == organizePanel);
            endingPanel.SetActive(active == endingPanel);
        }

        void UpdateHud()
        {
            state.Day = Mathf.Clamp(Mathf.Max(1, state.Visited.Count), 1, 7);
            dayText.text = $"DAY {state.Day} / 7";
            hpText.text = $"HP {state.Hp}";
        }
    }
}
