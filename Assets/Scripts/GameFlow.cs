using UnityEngine;
using UnityEngine.UI;
using SeoulLast.Data;

namespace SeoulLast
{
    // 전체 흐름: 스타트 → (초회)컷씬 → 메인 → 지역선택/출발 → 이벤트 연출 → 결과 → 메인(Day++) → ... → 게임오버
    // 메인화면은 씬의 MainScreen/MainCanvas를 재사용. 나머지(스타트/컷씬/이벤트/게임오버)는 임시 더미 화면.
    public class GameFlow : MonoBehaviour
    {
        [Header("이벤트 데이터 (EventData 에셋들)")]
        [SerializeField] EventData[] events;

        const float W = 1080f, H = 1920f;

        MainScreen mainScreen;
        GameObject mainCanvas;
        readonly System.Random rng = new System.Random();

        // 스탯
        int day = 1, hp = 100, food = 100, morale = 100;
        bool firstRun = true;

        // 더미 화면
        GameObject startPanel, cutscenePanel, eventPanel, gameOverPanel;
        Text eventTitle, eventBody, goBody;

        void Awake()
        {
            mainScreen = FindObjectOfType<MainScreen>();
            mainCanvas = GameObject.Find("MainCanvas");
            BuildFlowUI();
            if (mainCanvas) mainCanvas.SetActive(false);
        }

        void Start()
        {
            if (mainScreen != null) mainScreen.DepartRequested += OnDepart;
            ShowStart();
        }

        // ---------- 흐름 ----------
        void ShowStart() { Only(startPanel); }

        void StartGame()
        {
            if (firstRun) ShowCutscene();
            else ShowMain();
        }

        void ShowCutscene() { Only(cutscenePanel); }

        void ShowMain()
        {
            firstRun = false;
            HideAllFlow();
            if (mainCanvas) mainCanvas.SetActive(true);
            if (mainScreen) { mainScreen.SetDay(day); mainScreen.GoHome(); }
        }

        void OnDepart(string room)
        {
            var held = mainScreen != null ? mainScreen.HeldItemNames() : new System.Collections.Generic.List<string>();
            var ev = EventResolver.Pick(events, held, room, rng);

            string body;
            if (ev == null)
            {
                body = "평범한 하루였다. 별일 없었다.";
            }
            else
            {
                food = Clamp(food + ev.foodWaterChange);
                morale = Clamp(morale + ev.moraleChange);
                hp = Clamp(hp + ev.medicalSupplyChange);
                string res = !string.IsNullOrEmpty(ev.branchAResult) ? ev.branchAResult
                           : (!string.IsNullOrEmpty(ev.itemOwnedResult) ? ev.itemOwnedResult : "");
                body = $"<b>{ev.eventName}</b>\n\n{res}{Deltas(ev)}";
            }

            eventTitle.text = $"{room} — DAY {day}";
            eventBody.text = body;
            Only(eventPanel);
        }

        void NextDay()
        {
            day++;
            food = Clamp(food - 8); // 하루치 허기
            if (mainScreen) mainScreen.SetDay(day);

            if (hp <= 0 || food <= 0) ShowGameOver();
            else ShowMain();
        }

        void ShowGameOver()
        {
            string reason = food <= 0 ? "배가 고파 쓰러졌다..." : "체력이 바닥났다...";
            goBody.text = $"DAY {day}\n\n{reason}";
            Only(gameOverPanel);
        }

        void Restart()
        {
            day = 1; hp = 100; food = 100; morale = 100;
            ShowMain();
        }

        void Continue() // 이어하기 (임시): 약간 회복하고 계속
        {
            hp = Mathf.Max(hp, 30); food = Mathf.Max(food, 30);
            ShowMain();
        }

        string Deltas(EventData ev)
        {
            string s = "";
            if (ev.foodWaterChange != 0) s += $"\n식량 {Sign(ev.foodWaterChange)}";
            if (ev.moraleChange != 0) s += $"\n정신력 {Sign(ev.moraleChange)}";
            if (ev.medicalSupplyChange != 0) s += $"\n체력 {Sign(ev.medicalSupplyChange)}";
            return s.Length > 0 ? "\n" + s : "";
        }

        static string Sign(int v) => (v > 0 ? "+" : "") + v;
        static int Clamp(int v) => Mathf.Clamp(v, 0, 100);

        // ---------- 더미 화면 빌드 ----------
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
            var b = UIFactory.Button(p.transform, "CutNext", "계속", new Color(0.3f, 0.45f, 0.7f), ShowMain, out bl);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (W - 400) / 2, 1300, 400, 140);
            return p.gameObject;
        }

        GameObject BuildEvent(Transform root)
        {
            var p = UIFactory.Panel(root, "EventPanel", new Color(0.10f, 0.11f, 0.14f));
            UIFactory.Fill(p.rectTransform);
            eventTitle = UIFactory.Label(p.transform, "T", "", 40, TextAnchor.UpperCenter, new Color(0.95f, 0.85f, 0.5f));
            UIFactory.SetRect(eventTitle.rectTransform, 60, 200, W - 120, 80);
            var box = UIFactory.Panel(p.transform, "Box", new Color(0.16f, 0.17f, 0.21f));
            UIFactory.SetRect(box.rectTransform, 90, 320, W - 180, 760);
            eventBody = UIFactory.Label(box.transform, "B", "", 34, TextAnchor.UpperLeft, new Color(0.93f, 0.94f, 0.97f));
            UIFactory.SetRect(eventBody.rectTransform, 40, 40, W - 260, 680);
            Text bl;
            var b = UIFactory.Button(p.transform, "EvtOk", "확인", new Color(0.3f, 0.55f, 0.75f), NextDay, out bl);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (W - 460) / 2, 1180, 460, 150);
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
            if (eventPanel) eventPanel.SetActive(false);
            if (gameOverPanel) gameOverPanel.SetActive(false);
        }

        void Only(GameObject panel)
        {
            if (mainCanvas) mainCanvas.SetActive(false);
            HideAllFlow();
            if (panel) panel.SetActive(true);
        }
    }
}
