using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SeoulLast;
using SeoulLast.Data;

namespace SeoulLast.EditorTools
{
    // 메뉴: NoPainYesGame > Build Main Screen UI
    // 씬에 메인화면 UI를 실제 오브젝트로 생성하고, MainScreen 참조와 버튼 OnClick(persistent)을 자동 연결.
    public static class MainScreenBuilder
    {
        static Sprite white;
        static Font font;
        static List<Button> roomBtns;
        static List<string> roomNames;
        static Button departBtn;
        static Text departInfo;
        static Button bagUpBtn;
        static Button tamsaBtn;

        static readonly Color cBg = new Color(0.93f, 0.90f, 0.83f);
        static readonly Color cPanel = new Color(0.88f, 0.84f, 0.74f);
        static readonly Color cSlot = new Color(0.83f, 0.78f, 0.67f);
        static readonly Color cInk = new Color(0.27f, 0.23f, 0.18f);
        static readonly Color cBrown = new Color(0.55f, 0.40f, 0.28f);
        static readonly Color cRoom = new Color(0.60f, 0.70f, 0.55f);
        static readonly Color cEye = new Color(0.25f, 0.85f, 0.85f);

        static readonly string[] Rooms =
        {
            "음악실", "과학실", "1-1반", "1-2반", "2-1반",
            "2-2반", "급식실", "체육관", "도서관", "교무실"
        };

        [MenuItem("NoPainYesGame/Build Main Screen UI")]
        public static void Build()
        {
            white = EnsureWhiteSprite();
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/malgun.ttf");

            var old = GameObject.Find("MainCanvas");
            if (old != null) Object.DestroyImmediate(old);

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                var mod = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (mod != null) es.AddComponent(mod);
                else es.AddComponent<StandaloneInputModule>();
            }

            // Canvas
            var canvasGO = new GameObject("MainCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvas.GetComponent<RectTransform>();

            var bg = Img(root, "Background", cBg); Fill(bg.rectTransform);

            // ---- TopBar ----
            var bar = Img(root, "TopBar", new Color(0.85f, 0.81f, 0.71f)); SetRect(bar.rectTransform, 0, 0, 1080, 110);
            var dayText = Label(bar.transform, "Day", "DAY 1", 46, TextAnchor.MiddleCenter, cInk); SetRect(dayText.rectTransform, 340, 25, 400, 60);

            // 지역 정보/힌트 (지도·가방정리 모두에서 보임)
            departInfo = Label(root, "DepartInfo", "", 26, TextAnchor.MiddleCenter, new Color(0.35f, 0.30f, 0.22f));
            SetRect(departInfo.rectTransform, 40, 120, 1000, 64);

            // ---- CenterArea ----
            var area = NewRect(root, "CenterArea"); SetRect(area, 40, 130, 1000, 740);
            var characterView = BuildCharacter(area);
            RectTransform storageRect;
            var lockerView = BuildLocker(area, out storageRect);
            var mapView = BuildMap(area);
            var diaryView = BuildDiary(area);
            var shopView = BuildShop(area);
            var statusView = BuildStatus(area);
            characterView.SetActive(true);
            lockerView.SetActive(false); mapView.SetActive(false);
            diaryView.SetActive(false); shopView.SetActive(false); statusView.SetActive(false);

            // ---- Bag (6x6, cell 110) under BagArea — 가방정리 때만 표시 ----
            const int BCOLS = 6, BROWS = 6; const float BCELL = 110f;
            var bagArea = NewRect(root, "BagArea"); Fill(bagArea);
            var bagLbl = Label(bagArea, "BagLabel", "가방 — 챙길 것을 끌어 담으세요", 26, TextAnchor.UpperLeft, cInk);
            float gw = BCOLS * BCELL, gh = BROWS * BCELL, gx = (1080 - gw) / 2f, gy = 900f;
            SetRect(bagLbl.rectTransform, gx, gy - 40, 800, 40);
            var frame = Img(bagArea, "BagFrame", new Color(0.78f, 0.72f, 0.60f)); SetRect(frame.rectTransform, gx - 8, gy - 8, gw + 16, gh + 16);
            var bagGrid = NewRect(bagArea, "BagGrid"); SetRect(bagGrid, gx, gy, gw, gh);
            for (int yy = 0; yy < BROWS; yy++)
                for (int xx = 0; xx < BCOLS; xx++)
                {
                    var slot = Img(bagGrid, "bagslot", cSlot); slot.raycastTarget = false;
                    var srt = slot.rectTransform;
                    srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(0, 1); srt.pivot = new Vector2(0, 1);
                    srt.sizeDelta = new Vector2(BCELL - 8, BCELL - 8);
                    srt.anchoredPosition = new Vector2(xx * BCELL + 4, -(yy * BCELL + 4));
                }
            Text tl; tamsaBtn = Btn(bagArea, "TamsaBtn", "탐사 출발", new Color(0.85f, 0.45f, 0.25f), out tl); tl.fontSize = 36;
            SetRect((RectTransform)tamsaBtn.transform, (1080 - 480) / 2, gy + gh + 14, 480, 92);

            // ---- 상태이상 5개 알약 (캐릭터 좌측) ----
            string[] statusInit = { "허기", "수분", "건강", "기운", "기분" };
            var pills = new Image[5]; var pillTexts = new Text[5];
            for (int i = 0; i < 5; i++)
            {
                var pill = Img(root, "Status" + i, new Color(0.42f, 0.52f, 0.42f, 0.55f));
                SetRect(pill.rectTransform, 10, 150 + i * 64, 104, 54);
                var pt = Label(pill.transform, "L", statusInit[i], 22, TextAnchor.MiddleCenter, Color.white); Fill(pt.rectTransform);
                pt.raycastTarget = false;
                pills[i] = pill; pillTexts[i] = pt;
            }

            // ---- DragLayer (최상단) ----
            var dragLayer = NewRect(root, "DragLayer"); Fill(dragLayer); dragLayer.SetAsLastSibling();

            // ---- MainScreen 참조 연결 ----
            var ms = Object.FindObjectOfType<MainScreen>();
            if (ms == null) ms = new GameObject("MainScreen").AddComponent<MainScreen>();
            var so = new SerializedObject(ms);
            SetRef(so, "dayText", dayText);
            SetRef(so, "characterView", characterView);
            SetRef(so, "lockerView", lockerView);
            SetRef(so, "mapView", mapView);
            SetRef(so, "diaryView", diaryView);
            SetRef(so, "shopView", shopView);
            SetRef(so, "statusView", statusView);
            SetRef(so, "bagArea", bagArea.gameObject);
            SetRef(so, "bagGridRect", bagGrid);
            SetRef(so, "storageRect", storageRect);
            SetRef(so, "dragLayer", dragLayer);
            SetRef(so, "departInfo", departInfo);
            SetRefArray(so, "statusPills", pills);
            SetRefArray(so, "statusTexts", pillTexts);
            so.FindProperty("bagWidth").intValue = BCOLS;
            so.FindProperty("bagHeight").intValue = BROWS;
            so.FindProperty("bagCell").floatValue = BCELL;
            so.ApplyModifiedProperties();

            // ---- 버튼 OnClick(persistent) 연결 ----
            // 방 선택 → 출발(가방정리로) → 탐사(이벤트로)
            for (int i = 0; i < roomBtns.Count; i++)
                WireString(roomBtns[i], ms.SelectRoom, roomNames[i]);
            Wire(departBtn, ms.Depart);
            Wire(tamsaBtn, ms.Explore);
            Wire(bagUpBtn, ms.UpgradeBag);

            // ---- GameFlow: 오브젝트 생성 + EventData 에셋 전부 연결 ----
            var gf = Object.FindObjectOfType<GameFlow>();
            if (gf == null) gf = new GameObject("GameFlow").AddComponent<GameFlow>();
            var evGuids = AssetDatabase.FindAssets("t:EventData");
            var evList = new List<EventData>();
            foreach (var g in evGuids)
            {
                var ed = AssetDatabase.LoadAssetAtPath<EventData>(AssetDatabase.GUIDToAssetPath(g));
                if (ed != null) evList.Add(ed);
            }
            var gso = new SerializedObject(gf);
            var arr = gso.FindProperty("events");
            arr.arraySize = evList.Count;
            for (int i = 0; i < evList.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = evList[i];
            gso.ApplyModifiedProperties();
            EditorUtility.SetDirty(gf);
            Debug.Log("[MainScreenBuilder] GameFlow에 EventData " + evList.Count + "개 연결.");

            EditorUtility.SetDirty(ms);
            EditorSceneManager.MarkSceneDirty(ms.gameObject.scene);
            Debug.Log("[MainScreenBuilder] 메인화면 UI 빌드 완료.");
        }

        // ---------- 중앙 화면 빌더 ----------
        static GameObject BuildCharacter(Transform area)
        {
            var v = CenterPanel(area, "CharacterView", new Color(0.80f, 0.83f, 0.80f));
            var body = Img(v.transform, "Body", new Color(0.92f, 0.93f, 0.95f)); SetRect(body.rectTransform, 350, 230, 300, 320);
            var head = Img(v.transform, "Head", new Color(0.95f, 0.96f, 0.98f)); SetRect(head.rectTransform, 390, 120, 220, 180);
            var eyeL = Img(head.transform, "EyeL", cEye); SetRect(eyeL.rectTransform, 45, 70, 40, 40);
            var eyeR = Img(head.transform, "EyeR", cEye); SetRect(eyeR.rectTransform, 135, 70, 40, 40);
            var lbl = Label(v.transform, "Lbl", "캐릭터 이미지 위치", 28, TextAnchor.LowerCenter, new Color(0.3f, 0.3f, 0.3f, 0.7f)); SetRect(lbl.rectTransform, 0, 680, 1000, 50);
            return v;
        }

        static GameObject BuildLocker(Transform area, out RectTransform storageRect)
        {
            var v = CenterPanel(area, "LockerView", cPanel);
            var title = Label(v.transform, "T", "사물함 — 창고", 34, TextAnchor.UpperCenter, cInk); SetRect(title.rectTransform, 0, 24, 1000, 50);
            var hint = Label(v.transform, "H", "아래 가방으로 끌어 담거나, 가방에서 여기로 끌어 보관하세요.", 22, TextAnchor.UpperCenter, new Color(0.4f, 0.36f, 0.3f)); SetRect(hint.rectTransform, 0, 82, 1000, 40);
            var s = Img(v.transform, "Storage", new Color(0.83f, 0.78f, 0.67f, 0.5f)); SetRect(s.rectTransform, 20, 130, 960, 560);
            storageRect = s.rectTransform;

            // [디버그] 가방 단계 업그레이드 (시연용)
            Text bl; bagUpBtn = Btn(v.transform, "BagUpBtn", "[디버그] 가방 +", new Color(0.4f, 0.5f, 0.35f), out bl); bl.fontSize = 24;
            SetRect((RectTransform)bagUpBtn.transform, 660, 700, 320, 64);
            return v;
        }

        static GameObject BuildMap(Transform area)
        {
            var v = CenterPanel(area, "MapView", cPanel);
            var title = Label(v.transform, "T", "지도 — 학교 (방 선택 후 출발)", 34, TextAnchor.UpperCenter, cInk); SetRect(title.rectTransform, 0, 20, 1000, 50);

            roomBtns = new List<Button>();
            roomNames = new List<string>();
            float bw = 430, bh = 80, gap = 14, x0 = 60, y0 = 84;
            for (int i = 0; i < Rooms.Length; i++)
            {
                int cx = i % 2, cy = i / 2;
                Text rl; var b = Btn(v.transform, "room_" + Rooms[i], Rooms[i], cRoom, out rl); rl.fontSize = 28;
                SetRect((RectTransform)b.transform, x0 + cx * (bw + gap), y0 + cy * (bh + gap), bw, bh);
                roomBtns.Add(b); roomNames.Add(Rooms[i]);
            }

            float roomsBottom = y0 + 5 * (bh + gap); // 5행

            // 출발 버튼 (지역 정보 힌트는 root의 DepartInfo가 담당)
            Text dl; departBtn = Btn(v.transform, "DepartBtn", "출발", new Color(0.85f, 0.45f, 0.25f), out dl); dl.fontSize = 36;
            SetRect((RectTransform)departBtn.transform, (1000 - 480) / 2, roomsBottom + 30, 480, 92);
            return v;
        }

        static GameObject BuildDiary(Transform area)
        {
            var v = CenterPanel(area, "DiaryView", new Color(0.95f, 0.93f, 0.85f));
            var title = Label(v.transform, "T", "일기장", 38, TextAnchor.UpperCenter, cInk); SetRect(title.rectTransform, 0, 30, 1000, 60);
            var body = Label(v.transform, "B", "(일기 내용은 아직 준비 중입니다)\n\n오늘의 기록...", 28, TextAnchor.UpperLeft, new Color(0.35f, 0.30f, 0.25f)); SetRect(body.rectTransform, 60, 130, 880, 520);
            return v;
        }

        static GameObject BuildShop(Transform area)
        {
            var v = CenterPanel(area, "ShopView", cPanel);
            var title = Label(v.transform, "T", "상점 (준비 중)", 36, TextAnchor.UpperCenter, cInk); SetRect(title.rectTransform, 0, 30, 1000, 56);
            float cell = 200, gap = 24, gx = 70, gy = 120;
            for (int i = 0; i < 6; i++)
            {
                int cx = i % 3, cy = i / 3;
                var slot = Img(v.transform, "buy", cSlot); SetRect(slot.rectTransform, gx + cx * (cell + gap), gy + cy * (cell + gap), cell, cell);
                var pl = Label(slot.transform, "p", "??? G", 24, TextAnchor.LowerCenter, cInk); Fill(pl.rectTransform);
            }
            return v;
        }

        static GameObject BuildStatus(Transform area)
        {
            var v = CenterPanel(area, "StatusView", cPanel);
            var title = Label(v.transform, "T", "캐릭터 상태", 38, TextAnchor.UpperCenter, cInk); SetRect(title.rectTransform, 0, 30, 1000, 60);
            string[] stats = { "체력", "배고픔", "정신력", "체온", "청결" };
            int[] vals = { 80, 55, 70, 90, 40 };
            float y = 130;
            for (int i = 0; i < stats.Length; i++)
            {
                var nl = Label(v.transform, "s", stats[i], 28, TextAnchor.MiddleLeft, cInk); SetRect(nl.rectTransform, 70, y, 180, 60);
                var barBg = Img(v.transform, "bg", cSlot); SetRect(barBg.rectTransform, 260, y + 8, 620, 44);
                var fill = Img(barBg.transform, "fill", cRoom); SetRect(fill.rectTransform, 0, 0, 620 * vals[i] / 100f, 44);
                y += 96;
            }
            return v;
        }

        // ---------- 헬퍼 ----------
        static GameObject CenterPanel(Transform parent, string name, Color bg)
        {
            var img = Img(parent, name, bg); Fill(img.rectTransform);
            return img.gameObject;
        }

        static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static Image Img(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = white; img.type = Image.Type.Simple; img.color = color;
            return img;
        }

        static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.text = text; t.fontSize = size; t.alignment = anchor; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Button Btn(Transform parent, string name, string label, Color bg, out Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.sprite = white; img.color = bg;
            var btn = go.GetComponent<Button>(); btn.targetGraphic = img;
            labelText = Label(go.transform, "Label", label, 32, TextAnchor.MiddleCenter, Color.white);
            Fill(labelText.rectTransform);
            return btn;
        }

        static void SetRect(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, -y);
        }

        static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void SetRef(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning("[MainScreenBuilder] 필드 없음: " + prop);
        }

        static void SetRefArray(SerializedObject so, string prop, Object[] values)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning("[MainScreenBuilder] 배열 필드 없음: " + prop); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static void Wire(Button b, UnityAction call)
        {
            for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(b.onClick, i);
            UnityEventTools.AddPersistentListener(b.onClick, call);
        }

        static void WireString(Button b, UnityAction<string> call, string arg)
        {
            for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(b.onClick, i);
            UnityEventTools.AddStringPersistentListener(b.onClick, call, arg);
        }

        static Sprite EnsureWhiteSprite()
        {
            const string path = "Assets/UI/white.png";
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;

            Directory.CreateDirectory("Assets/UI");
            var tex = new Texture2D(8, 8);
            var px = new Color32[64];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
