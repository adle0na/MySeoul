using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SeoulLast.Data;

namespace SeoulLast
{
    // 자체 완결형 루프 게임 흐름 (재설계 2026-06-27)
    // 스타트 → (초회)컷씬 → [일기 → 가방정비 → 날짜이벤트(선택지/도박/포기) → 결과] 반복
    // 엔딩 A/B/C 시차 + 정답 3연속, 죽으면 리셋(컷씬 스킵).
    public class GameFlow : MonoBehaviour, IBagHost
    {
        [Header("이벤트 / 대화 / 아이템 데이터")]
        [SerializeField] EventData[] events;
        [SerializeField] DialogData[] dialogs;   // 대화 노드(event_dialog 시트)
        [SerializeField] ItemData[] items;       // 시트 아이템 (대화 등장 물건 지급용)

        const float W = 1080f, H = 1920f;
        const int GRID_W = 6, GRID_H = 6;   // 그리드 최대 크기(6x6). 시작 활성영역은 중앙 2x2(Stage)
        const float CELL = 148f;

        readonly System.Random rng = new System.Random();

        // ---- 상태 (4종: 허기/수분/건강/기운, 0~100 높을수록 나쁨) ----
        static readonly string[] StatNormal = { "허기", "수분", "건강", "기운" };
        static readonly string[] StatCaution = { "배고픔", "목마름", "아픔", "피곤함" };
        static readonly string[] StatDanger = { "굶주림", "갈증", "질병", "비몽사몽" };
        static readonly int[] DailyInc = { 12, 14, 4, 10 };
        readonly int[] status = new int[4];

        int day = 1;
        bool cutsceneSeen = false;
        string lastResult = "";

        // ---- 스테이지 진행 ----
        const int STAGE_LEN = 5;        // 1스테이지 = 5 이벤트
        int stageNo = 0;                // 0 = 온보딩, 1+ = 실전 스테이지
        int eventsThisStage = 0;        // 이번 스테이지에서 진행한 이벤트 수(실전만 카운트)
        string stageRegion = "";        // 현재 스테이지 지역(지도에서 선택)

        // ---- 인벤토리 (그리드 가방 only, 창고 없음) ----
        // bag = 그리드에 배치된 것(= 실제 보유). tray = 이번 정비에서 아직 안 넣은 획득품(미배치).
        readonly BagModel bag = new BagModel();
        readonly List<PlacedItem> tray = new List<PlacedItem>();

        // ---- 엔딩 A/B/C ----
        static readonly string[] EndIds = { "A", "B", "C" };
        static readonly int[] EndStartDay = { 3, 5, 7 };
        readonly bool[] endVoided = new bool[3];
        readonly int[] endStreak = new int[3];
        int activeEnding = -1; // 현재 진행 중인 엔딩 인덱스(-1=없음)

        EventData curEvent;
        DialogData curDialog;     // 현재 대화 노드
        string forcedNextId = ""; // 다음 이벤트("random" 또는 EventId)

        // ---- UI ----
        GameObject startPanel, cutscenePanel, bagPanel, eventPanel, endingPanel, gameOverPanel, restPanel, mapPanel;
        Text bagBody, eventTitle, eventBody, endingBody, goBody, restBody, bagBtnLabel;
        RectTransform choiceArea, mapArea;
        RectTransform gridRect, slotsRect, trayRect, trashZone, dragLayer;
        Button useBtn; Text useBtnLabel; PlacedItem selectedItem;
        System.Action bagOnDone;        // 가방 정비 완료 시 동작(맥락별)

        void Awake()
        {
            // 6x6 그리드, 중앙 2x2 활성(Stage 1). 나머지 칸은 딤(배치 불가).
            bag.Width = GRID_W; bag.Height = GRID_H; bag.FullGrid = false; bag.Stage = 1;
            // 씬에 미리 배치된 FlowCanvas가 있으면 그걸 바인딩(UI 개발자 작업물), 없으면 코드로 생성(폴백)
            var existing = GameObject.Find("FlowCanvas");
            if (existing != null && existing.transform.Find("StartPanel") != null)
                BindUI(existing.transform);
            else
            {
                if (existing != null) Destroy(existing);
                BuildUI();
            }
            HideAll();
        }

        // 씬에 배치된 FlowCanvas의 오브젝트를 이름으로 찾아 참조 연결 + 버튼 동작 와이어링
        void BindUI(Transform c)
        {
            startPanel = c.Find("StartPanel").gameObject;
            cutscenePanel = c.Find("CutscenePanel").gameObject;
            bagPanel = c.Find("BagPanel").gameObject;
            eventPanel = c.Find("EventPanel").gameObject;
            restPanel = c.Find("RestPanel").gameObject;
            mapPanel = c.Find("MapPanel").gameObject;
            endingPanel = c.Find("EndingPanel").gameObject;
            gameOverPanel = c.Find("GameOverPanel").gameObject;

            bagBody = c.Find("BagPanel/Info").GetComponent<Text>();
            eventTitle = c.Find("EventPanel/T").GetComponent<Text>();
            eventBody = c.Find("EventPanel/Box/B").GetComponent<Text>();
            endingBody = c.Find("EndingPanel/Box/B").GetComponent<Text>();
            goBody = c.Find("GameOverPanel/Box/B").GetComponent<Text>();
            restBody = c.Find("RestPanel/Box/B").GetComponent<Text>();
            bagBtnLabel = c.Find("BagPanel/DayStart/Label").GetComponent<Text>();

            choiceArea = c.Find("EventPanel/ChoiceArea").GetComponent<RectTransform>();
            mapArea = c.Find("MapPanel/MapArea").GetComponent<RectTransform>();
            gridRect = c.Find("BagPanel/Grid").GetComponent<RectTransform>();
            slotsRect = c.Find("BagPanel/Slots").GetComponent<RectTransform>();
            trayRect = c.Find("BagPanel/Tray").GetComponent<RectTransform>();
            trashZone = c.Find("BagPanel/Trash").GetComponent<RectTransform>();
            dragLayer = c.Find("BagPanel/DragLayer").GetComponent<RectTransform>();
            useBtn = c.Find("BagPanel/UseBtn").GetComponent<Button>();
            useBtnLabel = c.Find("BagPanel/UseBtn/Label").GetComponent<Text>();

            Wire(c, "StartPanel/StartBtn", OnStartBtn);
            Wire(c, "CutscenePanel/CutNext", BeginOnboarding);
            Wire(c, "BagPanel/DayStart", BagDone);
            Wire(c, "BagPanel/UseBtn", OnUseBtn);
            Wire(c, "RestPanel/RestBag", () => ShowBag(ShowRest, "휴식으로 →"));
            Wire(c, "RestPanel/RestMap", ShowMap);
            Wire(c, "EndingPanel/EndingPanelBtn", Restart);
            Wire(c, "GameOverPanel/GameOverPanelBtn", Restart);
        }

        void Wire(Transform root, string path, UnityEngine.Events.UnityAction action)
        {
            var t = root.Find(path);
            if (t == null) { Debug.LogWarning($"[GameFlow] 버튼 경로 없음: {path}"); return; }
            var b = t.GetComponent<Button>();
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(action);
        }

        void Start() { ResetRun(); ShowStart(); }

        void ResetRun()
        {
            day = 1; lastResult = "";
            status[0] = 20; status[1] = 20; status[2] = 0; status[3] = 10;
            bag.Placed.Clear(); tray.Clear();
            for (int i = 0; i < 3; i++) { endVoided[i] = false; endStreak[i] = 0; }
            activeEnding = -1;
            stageNo = 0; eventsThisStage = 0; stageRegion = "";
            // 온보딩 진입점: EVT-O001이 있으면 첫 이벤트로
            forcedNextId = FindEvent("EVT-O001") != null ? "EVT-O001" : "";
        }

        // ---------- 흐름 ----------
        void ShowStart() { Only(startPanel); }
        void OnStartBtn() { if (!cutsceneSeen) { cutsceneSeen = true; Only(cutscenePanel); } else BeginOnboarding(); }

        // 온보딩(스테이지 0) 시작 — EVT-O001부터 그래프대로 진행
        void BeginOnboarding()
        {
            stageNo = 0; eventsThisStage = 0; stageRegion = "";
            forcedNextId = FindEvent("EVT-O001") != null ? "EVT-O001" : "";
            PlayNextEvent();
        }

        // 가방 정비 화면. onDone = 완료 버튼 동작, label = 버튼 문구
        void ShowBag(System.Action onDone, string label)
        {
            bagOnDone = onDone;
            selectedItem = null;
            if (bagBtnLabel != null) bagBtnLabel.text = label;
            BuildBagScreen();
            Only(bagPanel);
        }

        void BagDone() { var a = bagOnDone; bagOnDone = null; if (a != null) a(); }

        // 그리드/트레이의 아이템 뷰를 모델에서 새로 생성
        void BuildBagScreen()
        {
            for (int i = gridRect.childCount - 1; i >= 0; i--) Destroy(gridRect.GetChild(i).gameObject);
            for (int i = trayRect.childCount - 1; i >= 0; i--)
            {
                var ch = trayRect.GetChild(i);
                if (ch.GetComponent<InvItemView>() != null) Destroy(ch.gameObject);
            }
            foreach (var p in bag.Placed)
            {
                var v = NewItemView(p); v.InBag = true;
                v.transform.SetParent(gridRect, false); v.AttachToBag(p.Origin);
            }
            foreach (var p in tray)
            {
                var v = NewItemView(p); v.InBag = false;
                v.transform.SetParent(trayRect, false);
            }
            LayoutTrayOnly();
            RecolorSlots();
            UpdateUseButton();
            UpdateBagInfo();
        }

        // 그리드 활성 영역(중앙 2x2)만 밝게, 나머지는 딤
        static readonly Color cSlotActive = new Color(0.83f, 0.78f, 0.67f, 0.55f);
        static readonly Color cSlotDim = new Color(0.30f, 0.28f, 0.25f, 0.35f);
        void RecolorSlots()
        {
            if (slotsRect == null) return;
            int n = 0;
            for (int i = 0; i < slotsRect.childCount; i++)
            {
                var img = slotsRect.GetChild(i).GetComponent<Image>();
                if (img == null) continue;
                int x = n % GRID_W, y = n / GRID_W; n++;
                img.color = bag.IsActiveCell(new Vector2Int(x, y)) ? cSlotActive : cSlotDim;
            }
        }

        InvItemView NewItemView(PlacedItem p)
        {
            var go = new GameObject("inv_" + p.Def.Id, typeof(RectTransform), typeof(CanvasGroup));
            var v = go.AddComponent<InvItemView>();
            v.Init(this, p, CELL);
            return v;
        }

        // 트레이 아이템(미배치) 좌→우, 위→아래 배치
        void LayoutTrayOnly()
        {
            float pad = 12f, x = pad, y = pad, rowH = 0f, areaW = trayRect.rect.width;
            for (int i = 0; i < trayRect.childCount; i++)
            {
                var v = trayRect.GetChild(i).GetComponent<InvItemView>();
                if (v == null) continue;
                var irt = v.GetComponent<RectTransform>();
                float iw = v.Model.Def.Width * CELL, ih = v.Model.Def.Height * CELL;
                if (x + iw > areaW - pad && x > pad) { x = pad; y += rowH + pad; rowH = 0f; }
                irt.anchorMin = new Vector2(0, 1); irt.anchorMax = new Vector2(0, 1); irt.pivot = new Vector2(0, 1);
                irt.anchoredPosition = new Vector2(x, -y);
                x += iw + pad; if (ih > rowH) rowH = ih;
            }
        }

        // 아이템 클릭 → 선택 (InvItemView가 호출)
        public void SelectItem(InvItemView item)
        {
            selectedItem = item != null ? item.Model : null;
            UpdateUseButton();
        }

        // 단일 '사용' 버튼: 회복 아이템이 선택됐을 때만 활성화
        void UpdateUseButton()
        {
            if (selectedItem != null && !bag.Placed.Contains(selectedItem) && !tray.Contains(selectedItem))
                selectedItem = null;
            bool usable = selectedItem != null && selectedItem.Def.IsRecovery && selectedItem.Uses > 0;
            if (useBtn != null) useBtn.interactable = usable;
            if (useBtnLabel != null)
            {
                if (usable) { var d = selectedItem.Def; useBtnLabel.text = $"{d.Name} 사용  ({StatNormal[d.RecoverStat]} -{d.RecoverAmt})"; }
                else if (selectedItem != null) useBtnLabel.text = $"{selectedItem.Def.Name} — 사용할 수 없음";
                else useBtnLabel.text = "사용할 회복 아이템을 선택";
            }
        }

        void OnUseBtn()
        {
            if (selectedItem == null || !selectedItem.Def.IsRecovery) return;
            UseRecovery(selectedItem);   // 내부에서 BuildBagScreen 호출
            selectedItem = null;
        }

        void UseRecovery(PlacedItem p)
        {
            if (p == null || !p.Def.IsRecovery) return;
            status[p.Def.RecoverStat] = Clamp(status[p.Def.RecoverStat] - p.Def.RecoverAmt);
            p.Uses--;
            if (p.Uses <= 0) { bag.RemoveFromBag(p); tray.Remove(p); }
            BuildBagScreen();
        }

        void UpdateBagInfo()
        {
            string warn = tray.Count > 0
                ? $"    <color=#e0a060>미배치 {tray.Count}개 — 그리드에 넣지 않으면 버려집니다</color>"
                : "";
            bagBody.text = $"가방 그리드 {bag.Width}×{bag.Height}    |    {StatusSummary()}{warn}";
        }

        // ---------- IBagHost (그리드 드래그 콜백) ----------
        public RectTransform BagGridRect => gridRect;
        public RectTransform BagDragLayer => dragLayer;
        public RectTransform StorageRect => trayRect;
        public RectTransform TrashRect => trashZone;
        public BagModel Bag => bag;
        public bool LockerOpen => true;
        public float Cell => CELL;

        public void PlaceInBag(InvItemView item, Vector2Int origin)
        {
            bag.PlaceAt(item.Model, origin);
            tray.Remove(item.Model);
            item.InBag = true;
            item.AttachToBag(origin);
            LayoutTrayOnly(); UpdateUseButton(); UpdateBagInfo();
        }

        public void MoveToStorage(InvItemView item)
        {
            if (item.InBag) bag.RemoveFromBag(item.Model);
            item.InBag = false;
            if (!tray.Contains(item.Model)) tray.Add(item.Model);
            item.transform.SetParent(trayRect, false);
            LayoutTrayOnly(); UpdateUseButton(); UpdateBagInfo();
        }

        public void ReturnToStorage(InvItemView item) => MoveToStorage(item);

        public void Discard(InvItemView item)
        {
            if (item.InBag) bag.RemoveFromBag(item.Model);
            tray.Remove(item.Model);
            Destroy(item.gameObject);
            LayoutTrayOnly(); UpdateUseButton(); UpdateBagInfo();
        }

        // 다음 이벤트 재생 (forcedNextId 또는 "random" 해석). 정해진 게 없으면 휴식.
        void PlayNextEvent()
        {
            tray.Clear();
            string id = forcedNextId; forcedNextId = "";
            if (id == "random") id = ResolveRandom();
            curEvent = string.IsNullOrEmpty(id) ? null : FindEvent(id);
            if (curEvent == null) { ShowRest(); return; }
            StartDialog(curEvent.startDialogId);   // 이벤트의 시작 대화부터
        }

        // 대화 노드 시작 ("Done"/없음/빈 노드면 이벤트 종료)
        void StartDialog(string dialogId)
        {
            if (dialogId == "Win") { ShowWin(); return; }
            if (dialogId == "Dead") { ShowGameOver(0); return; }
            if (string.IsNullOrEmpty(dialogId) || dialogId == "Done") { EndEvent(); return; }
            curDialog = FindDialog(dialogId);
            if (curDialog == null || IsEmptyDialog(curDialog)) { EndEvent(); return; }
            RenderDialog();
        }

        bool IsEmptyDialog(DialogData d)
            => string.IsNullOrWhiteSpace(d.description) && (d.choices == null || d.choices.Length == 0);

        void RenderDialog()
        {
            ClearChoices();
            eventTitle.text = stageNo == 0
                ? "온보딩"
                : $"스테이지 {stageNo}" + (string.IsNullOrEmpty(stageRegion) ? "" : " · " + stageRegion)
                  + $" · {Mathf.Min(eventsThisStage + 1, STAGE_LEN)}/{STAGE_LEN}";
            string body = string.IsNullOrWhiteSpace(curDialog.description)
                ? $"<color=#888888>(임시) {curEvent.eventType}{(string.IsNullOrEmpty(curEvent.region) ? "" : " · " + curEvent.region)} — 대화 준비 중</color>"
                : curDialog.description;
            eventBody.text = body;

            // label 있는 분기 = 버튼 / label 빈 분기(Empty) = 자동 진행
            int shown = 0; EventChoice auto = null;
            if (curDialog.choices != null)
                for (int i = 0; i < curDialog.choices.Length; i++)
                {
                    var c = curDialog.choices[i];
                    if (string.IsNullOrEmpty(c.label)) { if (auto == null) auto = c; continue; }
                    bool needItem = !string.IsNullOrEmpty(c.requiredItem);
                    bool has = !needItem || HasItem(c.requiredItem);
                    AddChoice(c, i, has, needItem && !has);
                    shown++;
                }

            if (shown == 0)
            {
                // 선택 버튼 없음 → 터치(확인) 시 자동 분기(Empty)의 다음 대화로, 없으면 이벤트 종료
                var a = auto;
                AddConfirm(() => { if (a != null) OnDialogBranch(a); else EndEvent(); });
            }
            Only(eventPanel);
        }

        // NextEventId == "random": 온보딩 제외, (현재 지역의 특정지역 + 일반) 중 랜덤
        string ResolveRandom()
        {
            var pool = new List<EventData>();
            foreach (var ev in events)
            {
                if (ev == null || ev.eventType == "온보딩") continue;
                if (string.IsNullOrEmpty(ev.startDialogId)) continue;   // 대화 없는(빈) 이벤트는 제외
                bool general = ev.eventType == "일반";
                bool regionMatch = ev.eventType == "특정지역" && ev.region == stageRegion;
                if (general || regionMatch) pool.Add(ev);
            }
            return pool.Count == 0 ? "" : pool[rng.Next(pool.Count)].eventId;
        }

        // 이벤트(대화 그래프) 종료 → 스테이지 진행(다음 이벤트/휴식)
        void EndEvent() { AfterEventResolve(""); }

        // 대화 분기 선택
        void OnDialogBranch(EventChoice c)
        {
            if (!string.IsNullOrEmpty(c.requiredItem)) ConsumeUse(c.requiredItem);
            ApplyNewState(c.newState);

            string next = c.nextEventId;   // 다음 대화 id ("Done"/빈값 = 이벤트 종료)
            if (c.opensInventory)
            {
                // 현재 대화의 등장 물건(DialogItemId) 지급 → 트레이(드래그로 그리드에 배치)
                GrantObjectItem(curDialog != null ? curDialog.spawnItemId : "");
                ShowBag(() => StartDialog(next), "계속 →");
            }
            else
                StartDialog(next);
        }

        // requiredItem(이름 또는 ID)을 표시용 아이템 이름으로
        string DisplayReq(string key)
        {
            var d = FindItemData(key);
            return d != null && !string.IsNullOrEmpty(d.itemName) ? d.itemName : key;
        }

        DialogData FindDialog(string id)
        {
            if (dialogs == null || string.IsNullOrEmpty(id)) return null;
            foreach (var d in dialogs) if (d != null && d.dialogId == id) return d;
            return null;
        }

        // ---------- 시트 아이템 지급 ----------
        ItemData FindItemData(string id)
        {
            if (items == null || string.IsNullOrEmpty(id)) return null;
            foreach (var it in items) if (it != null && it.itemId == id) return it;
            return null;
        }

        // ItemData(시트) → 그리드용 ItemDef 변환 (모양 미설정 시 1x1)
        static ItemDef DefFromItemData(ItemData d)
        {
            var cells = d.GetOccupiedCells();
            if (cells.Count == 0) cells.Add(Vector2Int.zero);
            var def = new ItemDef(d.itemId, string.IsNullOrEmpty(d.itemName) ? d.itemId : d.itemName,
                new Color(0.62f, 0.6f, 0.5f), cells);
            def.MaxUses = d.durability > 0 ? d.durability : 1;

            // 회복 매핑 (이름/타입 기반) — 지급된 자원/회복 아이템을 가방에서 사용 가능
            string n = d.itemName ?? "";
            if (n.Contains("빵") || n.Contains("통조림") || n.Contains("라면")) { def.RecoverStat = 0; def.RecoverAmt = 35; def.MaxUses = 1; }
            else if (n.Contains("음료") || n.Contains("생수") || n.Contains("물")) { def.RecoverStat = 1; def.RecoverAmt = 35; def.MaxUses = 1; }
            else if (d.itemType == "회복" || n.Contains("구급") || n.Contains("붕대")) { def.RecoverStat = 2; def.RecoverAmt = 45; def.MaxUses = 1; }
            else if (n.Contains("각성")) { def.RecoverStat = 3; def.RecoverAmt = 40; def.MaxUses = 1; }
            return def;
        }

        // 이벤트 등장 물건을 트레이로 지급
        void GrantObjectItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            var d = FindItemData(itemId);
            if (d == null) { Debug.LogWarning($"[GameFlow] 아이템 데이터 없음: {itemId} (Items 배열/시트 확인)"); return; }
            tray.Add(new PlacedItem(DefFromItemData(d)));
        }

        void AfterEventResolve(string nextId)
        {
            // 실전 스테이지만: 시간 경과(상태 악화) + 사망 판정 + 5이벤트 카운트
            if (stageNo >= 1)
            {
                for (int i = 0; i < 4; i++) status[i] = Clamp(status[i] + DailyInc[i]);
                for (int i = 0; i < 4; i++) if (status[i] >= 100) { ShowGameOver(i); return; }
                eventsThisStage++;
                if (eventsThisStage >= STAGE_LEN) { ShowRest(); return; }
            }

            if (nextId == "random") nextId = ResolveRandom();
            if (string.IsNullOrEmpty(nextId))
            {
                if (stageNo == 0) { ShowRest(); return; }   // 온보딩 종료(다음 미지정)
                nextId = ResolveRandom();
                if (string.IsNullOrEmpty(nextId)) { ShowRest(); return; }
            }
            forcedNextId = nextId;
            PlayNextEvent();
        }

        // 한 스테이지(5이벤트) 완료 → 휴식
        void ShowRest()
        {
            string head = stageNo == 0 ? "온보딩 완료" : $"스테이지 {stageNo} 완료";
            restBody.text = $"<b>{head}</b>\n\n{StatusSummary()}\n\n휴식하며 가방을 정비하고, 지도에서 다음 장소를 고르세요.";
            Only(restPanel);
        }

        // 지도에서 지역 선택 → 다음 스테이지 시작
        void StartStage(string region)
        {
            stageNo++;
            stageRegion = region;
            eventsThisStage = 0;
            forcedNextId = "random";   // 첫 이벤트는 지역+일반 풀에서 랜덤
            PlayNextEvent();
        }

        void ShowMap()
        {
            for (int i = mapArea.childCount - 1; i >= 0; i--) Destroy(mapArea.GetChild(i).gameObject);
            var regions = new List<string>();
            foreach (var ev in events)
                if (ev != null && ev.eventType == "특정지역" && !string.IsNullOrEmpty(ev.region) && !regions.Contains(ev.region))
                    regions.Add(ev.region);
            float y = 0;
            foreach (var r in regions)
            {
                var rr = r;
                Text tl; var b = UIFactory.Button(mapArea, "loc", r, new Color(0.32f, 0.45f, 0.6f), () => StartStage(rr), out tl);
                UIFactory.SetRect(b.GetComponent<RectTransform>(), 0, y, 920, 130); y += 150;
            }
            if (regions.Count == 0)
            {
                Text tl; var b = UIFactory.Button(mapArea, "loc", "어디든 (일반)", new Color(0.32f, 0.45f, 0.6f), () => StartStage(""), out tl);
                UIFactory.SetRect(b.GetComponent<RectTransform>(), 0, 0, 920, 130);
            }
            Only(mapPanel);
        }

        EventData FindEvent(string id)
        {
            foreach (var ev in events) if (ev != null && ev.eventId == id) return ev;
            return null;
        }

        bool HasEndingEvents(int e)
        {
            foreach (var ev in events) if (ev != null && ev.endingId == EndIds[e]) return true;
            return false;
        }

        EventData NextEndingEvent(int e)
        {
            var list = new List<EventData>();
            foreach (var ev in events) if (ev != null && ev.endingId == EndIds[e]) list.Add(ev);
            if (list.Count == 0) return null;
            return list[Mathf.Min(endStreak[e], list.Count - 1)];
        }

        // 상태 임계치(Level과 일치): 주의 40, 위험 70
        const int T_CAUTION = 40, T_DANGER = 70;

        // 시트 EventBranchXNewState 적용. 두 형식 지원 (구분 ; 또는 ,):
        //  (1) 컨디션 부여  예) "배고픔" → 해당 스탯을 그 컨디션 임계치까지만 악화.
        //      이미 그 컨디션(임계치 이상)이면 변화 없음. (주의어=40, 위험어=70)
        //      배고픔/굶주림→허기, 목마름/갈증→수분, 아픔/부상/질병→건강, 피곤함/비몽사몽→기운
        //  (2) "스탯±N"  예) "건강+20;기운-10"  (내부 스탯은 값이 클수록 나쁨)
        void ApplyNewState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            foreach (var tok in s.Split(';', ','))
            {
                var t = tok.Trim();
                if (t.Length == 0 || t == "없음" || t.Equals("Empty", System.StringComparison.OrdinalIgnoreCase)) continue;

                // (1) 컨디션 부여 (임계치까지만, 이미 그 상태면 변화 없음)
                if (TryApplyCondition(t)) continue;

                // (2) 스탯±N (명시적 증감)
                for (int i = 0; i < StatNormal.Length; i++)
                {
                    if (t.StartsWith(StatNormal[i]))
                    {
                        int v;
                        if (int.TryParse(t.Substring(StatNormal[i].Length).Trim(), out v))
                            status[i] = Clamp(status[i] + v);
                        break;
                    }
                }
                // 그 외(불안/중독/공포/화상 등 별도 상태이상)는 4스탯 모델에 없어 현재 무시
            }
        }

        // 컨디션 단어면 해당 스탯을 임계치까지 올림(이미 그 이상이면 그대로). 처리되면 true.
        bool TryApplyCondition(string w)
        {
            for (int i = 0; i < 4; i++)
            {
                if (w == StatCaution[i] || w == StatNormal[i]) { status[i] = Mathf.Max(status[i], T_CAUTION); return true; }
                if (w == StatDanger[i]) { status[i] = Mathf.Max(status[i], T_DANGER); return true; }
            }
            if (w == "부상") { status[2] = Mathf.Max(status[2], T_CAUTION); return true; } // 건강
            return false;
        }

        void ShowEnding(int e)
        {
            string[] titles = { "구조 — A 엔딩", "탈출 — B 엔딩", "생존 — C 엔딩" };
            endingBody.text = $"<b>{titles[e]}</b>\n\n스테이지 {stageNo}\n\n{lastResult}\n\n살아남았다.";
            Only(endingPanel);
        }

        void ShowGameOver(int idx)
        {
            goBody.text = $"스테이지 {stageNo}\n\n{StatDanger[idx]}(으)로 쓰러졌다...";
            Only(gameOverPanel);
        }

        void ShowWin()
        {
            endingBody.text = "<b>탈출 성공!</b>\n\n헬기가 옥상의 너를 발견했다.\n마침내 학교를 벗어났다.\n\n(데모 엔딩)";
            Only(endingPanel);
        }

        void Restart() { ResetRun(); ShowStart(); } // 시작화면부터(컷씬은 이미 봤으면 스킵)

        // ---------- 인벤토리 보조 (이벤트는 한글 이름으로 아이템 참조) ----------
        // requiredItem은 아이템 이름(데모) 또는 아이템 ID(시트, 예 TOO002) 둘 다 허용
        static bool Match(PlacedItem p, string key) => p.Def.Name == key || p.Def.Id == key;
        bool HasItem(string key)
        {
            foreach (var p in bag.Placed) if (Match(p, key) && p.Uses > 0) return true;
            foreach (var p in tray) if (Match(p, key) && p.Uses > 0) return true;
            return false;
        }
        bool AddItem(string name)
        {
            var def = ItemDatabase.GetByName(name);
            if (def == null) return false;
            var pi = new PlacedItem(def);
            if (!TryAutoPlace(pi)) tray.Add(pi);   // 자동 배치 실패 시 트레이로(정비에서 결정)
            return true;
        }
        bool TryAutoPlace(PlacedItem pi)
        {
            for (int y = 0; y < bag.Height; y++)
                for (int x = 0; x < bag.Width; x++)
                {
                    var o = new Vector2Int(x, y);
                    if (bag.CanPlace(pi.Def, o, null)) { bag.PlaceAt(pi, o); return true; }
                }
            return false;
        }
        void ConsumeUse(string key)
        {
            foreach (var p in bag.Placed)
                if (Match(p, key) && p.Uses > 0) { p.Uses--; if (p.Uses <= 0) bag.RemoveFromBag(p); return; }
            foreach (var p in tray)
                if (Match(p, key) && p.Uses > 0) { p.Uses--; if (p.Uses <= 0) tray.Remove(p); return; }
        }

        // ---------- 상태/텍스트 ----------
        static int Clamp(int v) => Mathf.Clamp(v, 0, 100);
        static int Level(int v) => v >= 70 ? 2 : v >= 40 ? 1 : 0;
        string StatusSummary()
        {
            var bad = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                int lv = Level(status[i]);
                if (lv == 1) bad.Add(StatCaution[i]);
                else if (lv == 2) bad.Add(StatDanger[i] + "(위험)");
            }
            return bad.Count == 0 ? "아직은 견딜 만하다." : "지금 " + string.Join(", ", bad) + " 상태.";
        }
        string ChangeText(EventChoice c)
        {
            string s = "";
            s += L(0, c.hunger); s += L(1, c.thirst); s += L(2, c.pain); s += L(3, c.fatigue);
            return s.Length > 0 ? "\n" + s : "";
        }
        string L(int i, int d) => d == 0 ? "" : $"\n{StatNormal[i]} {(d > 0 ? "악화" : "회복")} ({(d > 0 ? "+" : "") + d})";

        // ---------- 선택지 ----------
        void ClearChoices() { choiceCount = 0; for (int i = choiceArea.childCount - 1; i >= 0; i--) Destroy(choiceArea.GetChild(i).gameObject); }

        void AddChoice(EventChoice c, int index, bool enabled, bool gatedMissing)
        {
            string lbl = gatedMissing ? $"{c.label}   (필요: {DisplayReq(c.requiredItem)})" : c.label;
            Color col = enabled ? new Color(0.28f, 0.50f, 0.70f) : new Color(0.32f, 0.32f, 0.34f);
            Text t; var b = UIFactory.Button(choiceArea, "choice" + index, lbl, col, null, out t);
            t.fontSize = 27;
            b.interactable = enabled;
            if (enabled) { var cc = c; b.onClick.AddListener(() => OnDialogBranch(cc)); }
            UIFactory.SetRect(b.GetComponent<RectTransform>(), 0, ChoiceY(), 920, 110);
        }

        int choiceCount;
        int ChoiceY() { int y = choiceCount * 122; choiceCount++; return y; }

        void AddConfirm(UnityEngine.Events.UnityAction onConfirm)
        {
            choiceCount = 0;
            Text t; var b = UIFactory.Button(choiceArea, "Confirm", "확인", new Color(0.3f, 0.55f, 0.75f), onConfirm, out t);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (920 - 460) / 2, 0, 460, 120);
        }

        // ---------- UI 빌드 ----------
        void BuildUI()
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

            startPanel = Simple(root, "StartPanel", new Color(0.12f, 0.13f, 0.16f), "서울 라스트 (가제)", "시작", OnStartBtn);
            cutscenePanel = Simple(root, "CutscenePanel", new Color(0.06f, 0.06f, 0.08f), "눈을 떠보니, 모두 사라졌다.\n남은 건 너 하나뿐.\n\n(이 하루는 계속 반복된다…)", "계속", BeginOnboarding);

            bagPanel = BuildBagPanel(root);
            eventPanel = BuildEventPanel(root);
            restPanel = BuildRestPanel(root);
            mapPanel = BuildMapPanel(root);
            endingPanel = BuildText(root, "EndingPanel", new Color(0.10f, 0.16f, 0.12f), "엔딩", new Color(0.85f, 0.95f, 0.8f), out endingBody, "다시 시작", Restart);
            gameOverPanel = BuildText(root, "GameOverPanel", new Color(0.06f, 0.04f, 0.05f), "사망", new Color(0.9f, 0.4f, 0.4f), out goBody, "다시 시작", Restart);
        }

        GameObject BuildRestPanel(Transform root)
        {
            var p = UIFactory.Panel(root, "RestPanel", new Color(0.10f, 0.13f, 0.15f)); UIFactory.Fill(p.rectTransform);
            var h = UIFactory.Label(p.transform, "H", "휴식", 46, TextAnchor.UpperCenter, new Color(0.8f, 0.92f, 0.95f));
            UIFactory.SetRect(h.rectTransform, 60, 130, W - 120, 80);
            var box = UIFactory.Panel(p.transform, "Box", new Color(1, 1, 1, 0.08f));
            UIFactory.SetRect(box.rectTransform, 90, 250, W - 180, 700);
            restBody = UIFactory.Label(box.transform, "B", "", 33, TextAnchor.UpperLeft, new Color(0.85f, 0.92f, 0.95f));
            UIFactory.SetRect(restBody.rectTransform, 40, 40, W - 260, 620);
            Text bl; var b1 = UIFactory.Button(p.transform, "RestBag", "가방 정비", new Color(0.3f, 0.55f, 0.45f), () => ShowBag(ShowRest, "휴식으로 →"), out bl);
            UIFactory.SetRect(b1.GetComponent<RectTransform>(), (W - 460) / 2f, 1300, 460, 140);
            Text bl2; var b2 = UIFactory.Button(p.transform, "RestMap", "지도 → 다음 장소", new Color(0.85f, 0.55f, 0.3f), ShowMap, out bl2);
            UIFactory.SetRect(b2.GetComponent<RectTransform>(), (W - 460) / 2f, 1470, 460, 140);
            return p.gameObject;
        }

        GameObject BuildMapPanel(Transform root)
        {
            var p = UIFactory.Panel(root, "MapPanel", new Color(0.09f, 0.11f, 0.14f)); UIFactory.Fill(p.rectTransform);
            var h = UIFactory.Label(p.transform, "H", "지도 — 다음 장소 선택", 42, TextAnchor.UpperCenter, new Color(0.85f, 0.9f, 0.95f));
            UIFactory.SetRect(h.rectTransform, 60, 150, W - 120, 80);
            var areaGO = new GameObject("MapArea", typeof(RectTransform));
            mapArea = areaGO.GetComponent<RectTransform>(); mapArea.SetParent(p.transform, false);
            UIFactory.SetRect(mapArea, 80, 320, W - 160, 1400);
            return p.gameObject;
        }

        GameObject Simple(Transform root, string name, Color bg, string text, string btn, UnityEngine.Events.UnityAction onClick)
        {
            var p = UIFactory.Panel(root, name, bg); UIFactory.Fill(p.rectTransform);
            var t = UIFactory.Label(p.transform, "T", text, 40, TextAnchor.MiddleCenter, Color.white);
            UIFactory.SetRect(t.rectTransform, 80, 500, W - 160, 520);
            Text bl; var b = UIFactory.Button(p.transform, name + "Btn", btn, new Color(0.3f, 0.55f, 0.45f), onClick, out bl);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (W - 460) / 2, 1250, 460, 150);
            // 스타트 버튼 이름 고정 (테스트용)
            if (name == "StartPanel") b.gameObject.name = "StartBtn";
            if (name == "CutscenePanel") b.gameObject.name = "CutNext";
            return p.gameObject;
        }

        GameObject BuildText(Transform root, string name, Color bg, string head, Color headCol, out Text body, string btn, UnityEngine.Events.UnityAction onClick)
        {
            var p = UIFactory.Panel(root, name, bg); UIFactory.Fill(p.rectTransform);
            var h = UIFactory.Label(p.transform, "H", head, 46, TextAnchor.UpperCenter, headCol);
            UIFactory.SetRect(h.rectTransform, 60, 130, W - 120, 80);
            var box = UIFactory.Panel(p.transform, "Box", new Color(1, 1, 1, 0.08f));
            UIFactory.SetRect(box.rectTransform, 90, 250, W - 180, 820);
            body = UIFactory.Label(box.transform, "B", "", 33, TextAnchor.UpperLeft, headCol);
            UIFactory.SetRect(body.rectTransform, 40, 40, W - 260, 740);
            Text bl; var b = UIFactory.Button(p.transform, name + "Btn", btn, new Color(0.45f, 0.42f, 0.3f), onClick, out bl);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (W - 460) / 2, 1180, 460, 150);
            return p.gameObject;
        }

        GameObject BuildBagPanel(Transform root)
        {
            var p = UIFactory.Panel(root, "BagPanel", new Color(0.14f, 0.13f, 0.12f)); UIFactory.Fill(p.rectTransform);
            var h = UIFactory.Label(p.transform, "H", "가방 정비", 44, TextAnchor.UpperCenter, Color.white);
            UIFactory.SetRect(h.rectTransform, 60, 60, W - 120, 70);
            bagBody = UIFactory.Label(p.transform, "Info", "", 25, TextAnchor.UpperCenter, new Color(0.85f, 0.85f, 0.8f));
            UIFactory.SetRect(bagBody.rectTransform, 40, 140, W - 80, 60);

            // 그리드 (배경 + 슬롯 + 아이템 레이어)
            float gw = GRID_W * CELL, gh = GRID_H * CELL, gx = (W - gw) / 2f, gy = 220f;
            var gbg = UIFactory.Img(p.transform, "GridBG", new Color(0.10f, 0.10f, 0.12f));
            UIFactory.SetRect(gbg.rectTransform, gx - 6, gy - 6, gw + 12, gh + 12);
            var slotsGO = new GameObject("Slots", typeof(RectTransform));
            slotsRect = slotsGO.GetComponent<RectTransform>(); slotsRect.SetParent(p.transform, false);
            UIFactory.SetRect(slotsRect, gx, gy, gw, gh);
            for (int yy = 0; yy < GRID_H; yy++)
                for (int xx = 0; xx < GRID_W; xx++)
                {
                    var s = UIFactory.Img(slotsRect, "slot", new Color(0.83f, 0.78f, 0.67f, 0.45f));
                    s.raycastTarget = false;
                    UIFactory.SetRect(s.rectTransform, xx * CELL + 3, yy * CELL + 3, CELL - 6, CELL - 6);
                }
            var gridGO = new GameObject("Grid", typeof(RectTransform));
            gridRect = gridGO.GetComponent<RectTransform>(); gridRect.SetParent(p.transform, false);
            UIFactory.SetRect(gridRect, gx, gy, gw, gh);

            // 트레이(획득 대기) + 버리기 존 — 그리드 크기와 무관한 고정 레이아웃
            float ty = gy + gh + 24f;
            const float TRAY_W = 620f, TRASH_W = 300f, GAP = 20f;
            float trayX = (W - (TRAY_W + GAP + TRASH_W)) / 2f;
            float trashX = trayX + TRAY_W + GAP;
            var tbg = UIFactory.Img(p.transform, "TrayBG", new Color(0.20f, 0.19f, 0.17f));
            UIFactory.SetRect(tbg.rectTransform, trayX, ty, TRAY_W, 300);
            var tlb = UIFactory.Label(tbg.transform, "l", "획득 대기 (그리드로 끌어 보관)", 22, TextAnchor.UpperLeft, new Color(0.8f, 0.8f, 0.7f));
            tlb.raycastTarget = false; UIFactory.SetRect(tlb.rectTransform, 14, 8, TRAY_W - 28, 32);
            var trayGO = new GameObject("Tray", typeof(RectTransform));
            trayRect = trayGO.GetComponent<RectTransform>(); trayRect.SetParent(p.transform, false);
            UIFactory.SetRect(trayRect, trayX, ty + 44, TRAY_W, 252);

            var tz = UIFactory.Img(p.transform, "Trash", new Color(0.40f, 0.18f, 0.18f));
            trashZone = tz.rectTransform;
            UIFactory.SetRect(trashZone, trashX, ty, TRASH_W, 300);
            var tzl = UIFactory.Label(tz.transform, "l", "여기로 끌어\n버리기", 26, TextAnchor.MiddleCenter, new Color(0.95f, 0.7f, 0.7f));
            tzl.raycastTarget = false; UIFactory.Fill(tzl.rectTransform);

            // 회복 아이템 사용 버튼 (아이템 선택 시 활성화)
            useBtn = UIFactory.Button(p.transform, "UseBtn", "사용할 회복 아이템을 선택", new Color(0.3f, 0.6f, 0.45f), OnUseBtn, out useBtnLabel);
            useBtnLabel.fontSize = 30;
            UIFactory.SetRect(useBtn.GetComponent<RectTransform>(), trayX, ty + 320, TRAY_W + 300, 110);
            useBtn.interactable = false;

            var b = UIFactory.Button(p.transform, "DayStart", "계속 →", new Color(0.85f, 0.45f, 0.25f), BagDone, out bagBtnLabel);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (W - 460) / 2f, 1730, 460, 140);

            // 드래그 레이어 (최상단, raycast 없음)
            var dl = new GameObject("DragLayer", typeof(RectTransform));
            dragLayer = dl.GetComponent<RectTransform>(); dragLayer.SetParent(p.transform, false);
            UIFactory.Fill(dragLayer); dragLayer.SetAsLastSibling();
            return p.gameObject;
        }

        GameObject BuildEventPanel(Transform root)
        {
            var p = UIFactory.Panel(root, "EventPanel", new Color(0.10f, 0.11f, 0.14f)); UIFactory.Fill(p.rectTransform);
            eventTitle = UIFactory.Label(p.transform, "T", "", 38, TextAnchor.UpperCenter, new Color(0.95f, 0.85f, 0.5f));
            UIFactory.SetRect(eventTitle.rectTransform, 60, 150, W - 120, 70);
            var box = UIFactory.Panel(p.transform, "Box", new Color(0.16f, 0.17f, 0.21f));
            UIFactory.SetRect(box.rectTransform, 90, 250, W - 180, 420);
            eventBody = UIFactory.Label(box.transform, "B", "", 31, TextAnchor.UpperLeft, new Color(0.93f, 0.94f, 0.97f));
            UIFactory.SetRect(eventBody.rectTransform, 36, 30, W - 252, 360);
            var caGO = new GameObject("ChoiceArea", typeof(RectTransform));
            choiceArea = caGO.GetComponent<RectTransform>(); choiceArea.SetParent(p.transform, false);
            UIFactory.SetRect(choiceArea, 80, 710, W - 160, 600);
            return p.gameObject;
        }

        void HideAll()
        {
            foreach (var g in new[] { startPanel, cutscenePanel, bagPanel, eventPanel, restPanel, mapPanel, endingPanel, gameOverPanel })
                if (g) g.SetActive(false);
        }
        void Only(GameObject panel) { HideAll(); if (panel) panel.SetActive(true); }
    }
}
