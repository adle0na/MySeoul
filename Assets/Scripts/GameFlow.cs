using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SeoulLast.Data;

namespace SeoulLast
{
    // 선형 흐름: 스타트 → (초회)컷씬 → [일기장 → (10일마다 상점) → 학교도면 → 가방정리 → 탐사(랜덤 이벤트+선택지)] 반복 → 게임오버
    public class GameFlow : MonoBehaviour
    {
        [Header("이벤트 데이터 (EventData 에셋들)")]
        [SerializeField] EventData[] events;

        const float W = 1080f, H = 1920f;

        MainScreen mainScreen;
        GameObject mainCanvas;
        readonly System.Random rng = new System.Random();

        int day = 1;
        bool firstRun = true;
        string selectedRoom = "";
        string lastResult = "";

        // 상태이상 5종 (0~100, 높을수록 나쁨)
        readonly int[] status = new int[5];
        static readonly string[] NormalName = { "허기", "수분", "건강", "기운", "기분" };
        static readonly string[] CautionName = { "배고픔", "목마름", "아픔", "피곤함", "우울함" };
        static readonly string[] DangerName = { "굶주림", "갈증", "질병", "비몽사몽", "미침" };
        static readonly int[] DailyInc = { 12, 15, 2, 10, 4 };

        GameObject startPanel, cutscenePanel, diaryPanel, shopPanel, eventPanel, gameOverPanel;
        Text diaryBody, eventTitle, eventBody, goBody;
        RectTransform choiceArea;

        void Awake()
        {
            mainScreen = FindObjectOfType<MainScreen>();
            mainCanvas = GameObject.Find("MainCanvas");
            ResetStats();
            BuildFlowUI();
            if (mainCanvas) mainCanvas.SetActive(false);
        }

        void Start()
        {
            if (mainScreen != null)
            {
                mainScreen.LocationChosen += OnLocationChosen;
                mainScreen.ExploreRequested += OnExplore;
                mainScreen.RoomSelected += OnRoomSelected;
            }
            ShowStart();
        }

        void ResetStats() { status[0] = 20; status[1] = 20; status[2] = 0; status[3] = 0; status[4] = 0; }

        // ---------- 흐름 ----------
        void ShowStart() { Only(startPanel); }
        void StartGame() { if (firstRun) ShowCutscene(); else BeginDay(); }
        void ShowCutscene() { Only(cutscenePanel); }

        // 턴 시작: 일기장
        void BeginDay()
        {
            firstRun = false;
            string body = string.IsNullOrEmpty(lastResult)
                ? "학교에 홀로 남은 첫날. 어떻게든 살아남아야 한다."
                : lastResult;
            diaryBody.text = $"<b>DAY {day} 일기</b>\n\n{body}\n\n<color=#cfc8a0>{StatusSummary()}</color>";
            Only(diaryPanel);
        }

        void AfterDiary()
        {
            if (day % 10 == 0) ShowShop();
            else GoToMap();
        }

        void ShowShop() { Only(shopPanel); }

        void GoToMap()
        {
            ShowMainCanvas();
            PushStatuses();
            if (mainScreen) { mainScreen.SetDay(day); mainScreen.EnterMapMode(); }
        }

        void OnRoomSelected(string room)
        {
            if (mainScreen) mainScreen.SetDepartInfo(Theme(room));
        }

        // 학교도면에서 [출발] → 가방정리
        void OnLocationChosen(string room)
        {
            selectedRoom = room;
            if (mainScreen) mainScreen.EnterOrganizeMode(Theme(room) + "\n무엇을 챙겨 갈까?");
        }

        // 가방정리에서 [탐사] → 이벤트
        void OnExplore()
        {
            var ev = EventResolver.PickRandomForRoom(events, selectedRoom, rng);
            eventTitle.text = $"{selectedRoom} — DAY {day}";
            ClearChoices();

            if (ev == null)
            {
                eventBody.text = "평범한 하루였다. 별일 없었다.";
                lastResult = $"{selectedRoom}을(를) 둘러봤지만 별일 없었다.";
                AddConfirm(() => { ApplyDay(); });
            }
            else
            {
                eventBody.text = $"<b>{ev.eventName}</b>\n\n{(string.IsNullOrEmpty(ev.situation) ? "" : ev.situation)}";
                var held = mainScreen != null ? mainScreen.HeldItemNames() : new List<string>();
                if (ev.choices != null && ev.choices.Length > 0)
                {
                    for (int i = 0; i < ev.choices.Length; i++)
                    {
                        var c = ev.choices[i];
                        bool enabled = string.IsNullOrEmpty(c.requiredItem) || held.Contains(c.requiredItem);
                        AddChoice(ev, c, i, enabled);
                    }
                }
                else AddConfirm(() => { lastResult = ev.eventName; ApplyDay(); });
            }
            Only(eventPanel);
        }

        void OnChoice(EventData ev, EventChoice c)
        {
            status[0] = Clamp(status[0] + c.hunger);
            status[1] = Clamp(status[1] + c.thirst);
            status[2] = Clamp(status[2] + c.pain);
            status[3] = Clamp(status[3] + c.fatigue);
            status[4] = Clamp(status[4] + c.depression);
            if (c.bagUpgrade && mainScreen != null) mainScreen.UpgradeBag();

            lastResult = $"[{ev.eventName}] {c.resultText}";

            ClearChoices();
            string up = c.bagUpgrade ? "\n\n<b>[가방이 커졌다!]</b>" : "";
            eventBody.text = c.resultText + ChangeText(c) + up;
            AddConfirm(() => { ApplyDay(); });
        }

        // 하루 경과: 상태 악화 + 사망 판정 → 다음 일기
        void ApplyDay()
        {
            day++;
            for (int i = 0; i < 5; i++) status[i] = Clamp(status[i] + DailyInc[i]);
            int dead = -1;
            for (int i = 0; i < 5; i++) if (status[i] >= 100) { dead = i; break; }
            if (dead >= 0) ShowGameOver(dead);
            else BeginDay();
        }

        void ShowGameOver(int idx)
        {
            goBody.text = $"DAY {day}\n\n{DangerName[idx]}(으)로 쓰러졌다...";
            Only(gameOverPanel);
        }

        void Restart() { day = 1; lastResult = ""; ResetStats(); BeginDay(); }
        void Continue() { for (int i = 0; i < 5; i++) status[i] = Mathf.Min(status[i], 45); BeginDay(); }

        // ---------- 보조 ----------
        static string Theme(string room)
        {
            switch (room)
            {
                case "음악실": return "음악실 — 어둡고 고요하다. 빛이 있으면 좋을 듯.";
                case "과학실": return "과학실 — 화학약품·가스 위험 지대.";
                case "급식실": return "급식실 — 잠긴 창고가 많다. 부술 도구가 필요할지도.";
                case "체육관": return "체육관 — 높고 험한 구조물.";
                case "도서관": return "도서관 — 정전·어둠. 불빛이 도움될 듯.";
                case "교무실": return "교무실 — 인기척이 난다. 응급 상황 대비.";
                case "1-1반": return "1-1반 — 평범한 교실. 뭔가 남아있을까.";
                default: return room;
            }
        }

        string StatusSummary()
        {
            var bad = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                int lv = Level(status[i]);
                if (lv == 1) bad.Add(CautionName[i]);
                else if (lv == 2) bad.Add(DangerName[i] + "(위험)");
            }
            return bad.Count == 0 ? "아직은 견딜 만하다." : "지금 " + string.Join(", ", bad) + " 상태다.";
        }

        void PushStatuses()
        {
            if (mainScreen == null) return;
            int[] lv = new int[5]; string[] lb = new string[5];
            for (int i = 0; i < 5; i++)
            {
                lv[i] = Level(status[i]);
                lb[i] = lv[i] == 1 ? CautionName[i] : lv[i] == 2 ? DangerName[i] : NormalName[i];
            }
            mainScreen.SetStatuses(lv, lb);
        }

        static int Level(int v) => v >= 70 ? 2 : v >= 40 ? 1 : 0;
        static int Clamp(int v) => Mathf.Clamp(v, 0, 100);

        string ChangeText(EventChoice c)
        {
            string s = "";
            s += Line(0, c.hunger); s += Line(1, c.thirst); s += Line(2, c.pain);
            s += Line(3, c.fatigue); s += Line(4, c.depression);
            return s.Length > 0 ? "\n" + s : "";
        }
        string Line(int i, int d) => d == 0 ? "" : $"\n{NormalName[i]} {(d > 0 ? "악화" : "회복")} ({(d > 0 ? "+" : "") + d})";

        // ---------- 선택지 버튼 ----------
        void ClearChoices() { for (int i = choiceArea.childCount - 1; i >= 0; i--) Destroy(choiceArea.GetChild(i).gameObject); }

        void AddChoice(EventData ev, EventChoice c, int index, bool enabled)
        {
            string lbl = enabled ? c.label : $"{c.label}   (필요: {c.requiredItem})";
            Color col = enabled ? new Color(0.28f, 0.50f, 0.70f) : new Color(0.32f, 0.32f, 0.34f);
            Text t; var b = UIFactory.Button(choiceArea, "choice" + index, lbl, col, null, out t);
            t.fontSize = 28;
            b.interactable = enabled;
            if (enabled) { var ce = ev; var cc = c; b.onClick.AddListener(() => OnChoice(ce, cc)); }
            UIFactory.SetRect(b.GetComponent<RectTransform>(), 0, index * 139f, 920, 125);
        }

        void AddConfirm(UnityEngine.Events.UnityAction onConfirm)
        {
            Text t; var b = UIFactory.Button(choiceArea, "Confirm", "확인", new Color(0.3f, 0.55f, 0.75f), onConfirm, out t);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (920 - 460) / 2, 0, 460, 125);
        }

        // ---------- 더미/오버레이 화면 ----------
        void BuildFlowUI()
        {
            var canvasGO = new GameObject("FlowCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvas.GetComponent<RectTransform>();

            startPanel = BuildStart(root);
            cutscenePanel = BuildCutscene(root);
            diaryPanel = BuildDiary(root);
            shopPanel = BuildShop(root);
            eventPanel = BuildEvent(root);
            gameOverPanel = BuildGameOver(root);
            HideAllFlow();
        }

        GameObject BuildStart(Transform root)
        {
            var p = UIFactory.Panel(root, "StartPanel", new Color(0.12f, 0.13f, 0.16f));
            UIFactory.Fill(p.rectTransform);
            var title = UIFactory.Label(p.transform, "T", "서울 라스트 (가제)", 60, TextAnchor.MiddleCenter, Color.white);
            UIFactory.SetRect(title.rectTransform, 60, 600, W - 120, 120);
            Text bl;
            var b = UIFactory.Button(p.transform, "StartBtn", "시작", new Color(0.25f, 0.6f, 0.45f), StartGame, out bl);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (W - 460) / 2, 1100, 460, 150);
            return p.gameObject;
        }

        GameObject BuildCutscene(Transform root)
        {
            var p = UIFactory.Panel(root, "CutscenePanel", new Color(0.06f, 0.06f, 0.08f));
            UIFactory.Fill(p.rectTransform);
            var body = UIFactory.Label(p.transform, "B", "[컷씬 — 준비 중]\n\n눈을 떠보니, 모두 사라졌다.\n남은 건 너 하나뿐.", 36, TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.92f));
            UIFactory.SetRect(body.rectTransform, 80, 500, W - 160, 500);
            Text bl;
            var b = UIFactory.Button(p.transform, "CutNext", "계속", new Color(0.3f, 0.45f, 0.7f), BeginDay, out bl);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (W - 400) / 2, 1300, 400, 140);
            return p.gameObject;
        }

        GameObject BuildDiary(Transform root)
        {
            var p = UIFactory.Panel(root, "DiaryPanel", new Color(0.93f, 0.90f, 0.82f));
            UIFactory.Fill(p.rectTransform);
            var head = UIFactory.Label(p.transform, "H", "일기장", 40, TextAnchor.UpperCenter, new Color(0.3f, 0.25f, 0.18f));
            UIFactory.SetRect(head.rectTransform, 60, 140, W - 120, 70);
            var box = UIFactory.Panel(p.transform, "Box", new Color(0.97f, 0.95f, 0.88f));
            UIFactory.SetRect(box.rectTransform, 90, 260, W - 180, 820);
            diaryBody = UIFactory.Label(box.transform, "B", "", 34, TextAnchor.UpperLeft, new Color(0.28f, 0.24f, 0.18f));
            UIFactory.SetRect(diaryBody.rectTransform, 40, 40, W - 260, 740);
            Text bl;
            var b = UIFactory.Button(p.transform, "DiaryOk", "확인", new Color(0.55f, 0.45f, 0.3f), AfterDiary, out bl);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (W - 460) / 2, 1180, 460, 150);
            return p.gameObject;
        }

        GameObject BuildShop(Transform root)
        {
            var p = UIFactory.Panel(root, "ShopPanel", new Color(0.14f, 0.15f, 0.18f));
            UIFactory.Fill(p.rectTransform);
            var head = UIFactory.Label(p.transform, "H", "상점 (10일마다 · 준비 중)", 38, TextAnchor.UpperCenter, new Color(0.95f, 0.9f, 0.6f));
            UIFactory.SetRect(head.rectTransform, 60, 160, W - 120, 70);
            for (int i = 0; i < 6; i++)
            {
                int cx = i % 3, cy = i / 3;
                var slot = UIFactory.Img(p.transform, "buy", new Color(0.25f, 0.26f, 0.3f));
                UIFactory.SetRect(slot.rectTransform, 90 + cx * 320, 320 + cy * 320, 280, 280);
                var pl = UIFactory.Label(slot.transform, "p", "??? G", 26, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
                UIFactory.Fill(pl.rectTransform);
            }
            Text bl;
            var b = UIFactory.Button(p.transform, "ShopExit", "나가기", new Color(0.5f, 0.4f, 0.35f), GoToMap, out bl);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (W - 460) / 2, 1180, 460, 150);
            return p.gameObject;
        }

        GameObject BuildEvent(Transform root)
        {
            var p = UIFactory.Panel(root, "EventPanel", new Color(0.10f, 0.11f, 0.14f));
            UIFactory.Fill(p.rectTransform);
            eventTitle = UIFactory.Label(p.transform, "T", "", 40, TextAnchor.UpperCenter, new Color(0.95f, 0.85f, 0.5f));
            UIFactory.SetRect(eventTitle.rectTransform, 60, 170, W - 120, 80);
            var box = UIFactory.Panel(p.transform, "Box", new Color(0.16f, 0.17f, 0.21f));
            UIFactory.SetRect(box.rectTransform, 90, 280, W - 180, 420);
            eventBody = UIFactory.Label(box.transform, "B", "", 32, TextAnchor.UpperLeft, new Color(0.93f, 0.94f, 0.97f));
            UIFactory.SetRect(eventBody.rectTransform, 36, 30, W - 252, 360);
            var caGO = new GameObject("ChoiceArea", typeof(RectTransform));
            choiceArea = caGO.GetComponent<RectTransform>();
            choiceArea.SetParent(p.transform, false);
            UIFactory.SetRect(choiceArea, 80, 740, W - 160, 560);
            return p.gameObject;
        }

        GameObject BuildGameOver(Transform root)
        {
            var p = UIFactory.Panel(root, "GameOverPanel", new Color(0.05f, 0.04f, 0.05f));
            UIFactory.Fill(p.rectTransform);
            var title = UIFactory.Label(p.transform, "T", "사망", 80, TextAnchor.MiddleCenter, new Color(0.85f, 0.25f, 0.25f));
            UIFactory.SetRect(title.rectTransform, 60, 480, W - 120, 130);
            goBody = UIFactory.Label(p.transform, "B", "", 36, TextAnchor.UpperCenter, new Color(0.9f, 0.9f, 0.9f));
            UIFactory.SetRect(goBody.rectTransform, 80, 680, W - 160, 300);
            Text bl;
            var restart = UIFactory.Button(p.transform, "Restart", "처음부터", new Color(0.5f, 0.3f, 0.3f), Restart, out bl);
            UIFactory.SetRect(restart.GetComponent<RectTransform>(), 120, 1250, 400, 150);
            Text cl;
            var cont = UIFactory.Button(p.transform, "Continue", "이어하기", new Color(0.35f, 0.45f, 0.55f), Continue, out cl);
            UIFactory.SetRect(cont.GetComponent<RectTransform>(), W - 120 - 400, 1250, 400, 150);
            return p.gameObject;
        }

        void HideAllFlow()
        {
            if (startPanel) startPanel.SetActive(false);
            if (cutscenePanel) cutscenePanel.SetActive(false);
            if (diaryPanel) diaryPanel.SetActive(false);
            if (shopPanel) shopPanel.SetActive(false);
            if (eventPanel) eventPanel.SetActive(false);
            if (gameOverPanel) gameOverPanel.SetActive(false);
        }

        void ShowMainCanvas()
        {
            HideAllFlow();
            if (mainCanvas) mainCanvas.SetActive(true);
        }

        void Only(GameObject panel)
        {
            if (mainCanvas) mainCanvas.SetActive(false);
            HideAllFlow();
            if (panel) panel.SetActive(true);
        }
    }
}
