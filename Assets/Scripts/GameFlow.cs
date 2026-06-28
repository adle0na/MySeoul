using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        [SerializeField] LocationData[] locations; // 장소(Location 시트, 지도 선택지)
        [SerializeField] TMP_FontAsset uiFont;     // 런타임 생성 텍스트용 한글 폰트(UIFactory 주입)

        const float W = 1080f, H = 1920f;
        const int GRID_W = 6, GRID_H = 6;   // 그리드 최대 크기(6x6). 시작 활성영역은 중앙 2x2(Stage)
        const float CELL = 148f;

        readonly System.Random rng = new System.Random();

        // ---- 상태 (4종: 허기/수분/건강/기운, 0~100 높을수록 나쁨) ----
        static readonly string[] StatNormal = { "허기", "수분", "건강", "기운" };
        static readonly string[] StatCaution = { "배고픔", "목마름", "아픔", "피곤함" };
        static readonly string[] StatDanger = { "굶주림", "갈증", "질병", "비몽사몽" };
        static readonly string[] StatRail = { "배고픔", "갈증", "아픔", "정신병" };  // 좌측 상태레일 표시명
        static readonly int[] DailyInc = { 12, 14, 4, 10 };
        readonly int[] status = new int[4];

        int day = 1;
        bool cutsceneSeen = false;
        string lastResult = "";

        // ---- 스테이지 진행 ----
        const int STAGE_LEN = 5;        // 1스테이지 = 5 이벤트
        int stageNo = 0;                // 0 = 온보딩, 1+ = 실전 스테이지
        int eventsThisStage = 0;        // 이번 스테이지에서 진행한 이벤트 수(실전만 카운트)
        string stageRegion = "";        // 현재 스테이지 매칭 키 = LocationID (이벤트 EventRegion과 매칭)
        string stageLocationName = "";  // 상단바 표시용 장소 이름

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
        TextMeshProUGUI bagBody, eventTitle, eventBody, endingBody, goBody, restBody, bagBtnLabel;
        RectTransform choiceArea, mapArea;
        TextMeshProUGUI mapInfoName, mapInfoDesc;   // 지도 하단 선택지역 정보(DiscriptionArea)
        GameObject mapGoBtn;                          // 지도 하단 '출발' 버튼(런타임)
        string selLocId, selLocName;                 // 지도에서 현재 선택한 지역
        GameObject choiceButtonPrefab;
        RectTransform gridRect, slotsRect, trayRect, trashZone, dragLayer;
        Button useBtn; TextMeshProUGUI useBtnLabel; PlacedItem selectedItem;
        GameObject dayStartGO;          // 가방 '계속/닫기' 버튼(씬에서 비활성일 수 있어 ShowBag에서 활성화)
        System.Action bagOnDone;        // 가방 정비 완료 시 동작(맥락별)
        GameObject nextEventBtn;        // 이벤트 화면 하단 '다음' 버튼(런타임 생성, 가방 닫은 뒤 진행)
        string pendingNextId;           // 다음 버튼이 진행할 대화/이벤트 id
        RectTransform bagPanelRT;
        float bagHiddenY;
        float bagShownY;
        bool  isSlidingBag;
        bool  bagOpenedByItem;  // true=아이템 획득으로 열림, false=버튼으로 열림
        BagSkinManager bagSkinManager;  // 가방 이미지 교체 관리자
        SeoulLast.ScreenSpeedTransition transition;

        // ---- Explore 화면 (사이드스크롤 연출) ----
        TextMeshProUGUI dayText;                   // 상단바 "DAY n · 장소"
        MY_UIIcon_Script[] statusIcon = new MY_UIIcon_Script[4];   // 좌측 상태레일 아이콘
        int[] statusLevel = new int[4];   // 이전 상태 레벨 추적
        TextMeshProUGUI[] statusLabel = new TextMeshProUGUI[4];    // 상태 이름
        RawImage bgRaw;                 // 인피니티 스크롤 배경
        Image charImg;                  // 메인 캐릭터(스프라이트, 추후 Spine 교체)
        Image itemImg;                  // 다가오는 아이템
        RectTransform eventCard;        // 하단 이벤트 카드
        GameObject cardBGChoice;          // 선택지 버튼 배경
        GameObject cardBGResult;          // 결과 텍스트 배경
        GameObject cardBGBase;            // 기본 배경 (Choice/Result 꺼졌을 때)
        Button bagToggleBtn;
        Button nextBtn;                  // 결과 확인 후 다음 단계 버튼            // 하단 가방 토글
        bool walking;                   // 걷는 중(배경 스크롤 + Spine 재생)
        float itemMeetX;                // 아이템이 멈출 x(캐릭터 위치)
        const float SCROLL_SPEED = 0.06f;
        Component charSpine;            // Spine 걷기 SkeletonGraphic (리플렉션 제어)
        System.Reflection.FieldInfo spineTimeScale;
        GameObject charSpineIdleGO;     // Spine 정면 idle SkeletonGraphic (O001-01~08용, 토글)
        // idle 스켈레톤 데이터(NPYGchan SkeletonDataAsset). 씬 GameObject는 FlowCanvas 재빌드 시
        // 유실되므로, 이 참조로 런타임에 CharSpineIdle을 생성한다. (Spine 의존 회피 위해 Object로 보관)
        [SerializeField] UnityEngine.Object idleSkeletonData;

        // ---- 이벤트 연출 FX (머리 위 말풍선/느낌표, 우측 연기) ----
        [Header("이벤트 연출 스프라이트(프레임)")]
        [SerializeField] Sprite[] speechFrames;   // 말풍선 (bahbah_1/2/3, speech)
        [SerializeField] Sprite[] exclamFrames;   // 느낌표 (exclamation_1/2) — 아이템 이벤트
        [SerializeField] Sprite[] pungFrames;     // 연기 (pung_1/2) — 아이템 등장
        Image overheadFx;               // 캐릭터 머리 위 (런타임 생성)
        Image pungFx;                   // 우측 아이템 연기 (런타임 생성)

        void Awake()
        {
            if (uiFont != null) UIFactory.Override = uiFont;   // 런타임 텍스트 한글 폰트
            // 6x6 그리드, 중앙 2x2 활성(Stage 1). 나머지 칸은 딤(배치 불가).
            bag.Width = GRID_W; bag.Height = GRID_H; bag.FullGrid = false; bag.Stage = 2;   // 시작부터 2레벨(중앙 3x3)
            // 씬에 미리 배치된 FlowCanvas가 있으면 그걸 바인딩(UI 개발자 작업물), 없으면 코드로 생성(폴백)
            var existing = GameObject.Find("FlowCanvas");
            if (existing != null && existing.transform.Find("StartPanel") != null)
                BindUI(existing.transform);
            else
            {
                if (existing != null) Destroy(existing);
                BuildUI();
            }
            SetupAudio();
            HideAll();
        }

        // ---------- 사운드 ----------
        AudioSource bgmSource, sfxSource, typingSource;
        AudioClip clickClip, typingClip;

        void SetupAudio()
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            sfxSource = gameObject.AddComponent<AudioSource>();
            typingSource = gameObject.AddComponent<AudioSource>();
            var bgm = LoadClip("Bgm");
            if (bgm != null) { bgmSource.clip = bgm; bgmSource.loop = true; bgmSource.volume = 0.5f; bgmSource.Play(); }
            clickClip = LoadClip("button");
            typingClip = LoadClip("keyboard_typing");
            if (typingClip != null) { typingSource.clip = typingClip; typingSource.loop = true; typingSource.volume = 0.6f; }
            UIFactory.Sfx = sfxSource; UIFactory.ClickClip = clickClip;   // 버튼 클릭음 주입
        }

        AudioClip LoadClip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var c = Resources.Load<AudioClip>("Sound/" + name);
            if (c != null) return c;
#if UNITY_EDITOR
            var cx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/art/Sound/" + name + ".mp3");
            if (cx != null) return cx;
#endif
            return null;
        }

        public void PlayClick() { if (sfxSource != null && clickClip != null) sfxSource.PlayOneShot(clickClip); }

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

            bagBody = c.Find("BagPanel/Info")?.GetComponent<TextMeshProUGUI>();
            eventTitle = c.Find("EventPanel/Card/TextBox/T").GetComponent<TextMeshProUGUI>();
            eventBody = c.Find("EventPanel/Card/B").GetComponent<TextMeshProUGUI>();
            if (eventBody != null) eventBody.raycastTarget = false;   // 본문 텍스트가 카드 전체를 덮어 클릭 가로채는 것 방지
            endingBody = c.Find("EndingPanel/Box/B").GetComponent<TextMeshProUGUI>();
            goBody = c.Find("GameOverPanel/Box/B").GetComponent<TextMeshProUGUI>();
            restBody = c.Find("RestPanel/Box/B").GetComponent<TextMeshProUGUI>();
            dayStartGO = c.Find("BagPanel/DayStart")?.gameObject;
            var dayStartLabel = c.Find("BagPanel/DayStart/Label"); bagBtnLabel = dayStartLabel != null ? dayStartLabel.GetComponent<TextMeshProUGUI>() : null;

            choiceArea    = c.Find("EventPanel/Card/ChoiceArea").GetComponent<RectTransform>();
            cardBGChoice  = c.Find("EventPanel/Card/CardBG_Choice")?.gameObject;
            cardBGResult  = c.Find("EventPanel/Card/CardBG_Result")?.gameObject;
            cardBGBase    = c.Find("EventPanel/Card/CardBG_Base")?.gameObject;
            choiceButtonPrefab = Resources.Load<GameObject>("Prefabs/ChoiceButton");
            eventCard = c.Find("EventPanel/Card").GetComponent<RectTransform>();
            dayText = c.Find("EventPanel/TopBar/DayText").GetComponent<TextMeshProUGUI>();
            bgRaw = c.Find("EventPanel/Bg").GetComponent<RawImage>();
            charImg = c.Find("EventPanel/Char").GetComponent<Image>();
            itemImg = c.Find("EventPanel/Item").GetComponent<Image>();
            for (int i = 0; i < 4; i++)
            {
                var iconGO = c.Find("EventPanel/Status_GridLayout/Icon_" + i);
                statusIcon[i] = iconGO != null ? iconGO.GetComponent<MY_UIIcon_Script>() : null;
                var lblGO = c.Find("EventPanel/Status_GridLayout/Icon_" + i + "/Icon_Image"); statusLabel[i] = lblGO != null ? lblGO.GetComponent<TextMeshProUGUI>() : null;
            }
            var bagToggleGO = c.Find("BagPanel/BagToggle") ?? c.Find("EventPanel/BagToggle"); bagToggleBtn = bagToggleGO != null ? bagToggleGO.GetComponent<Button>() : null;
            // Spine 캐릭터(있으면) — timeScale 제어용 캐싱(리플렉션, Spine 의존 회피)
            var csT = c.Find("EventPanel/CharSpine");
            if (csT != null)
            {
                var sgType = System.Type.GetType("Spine.Unity.SkeletonGraphic, spine-unity");
                if (sgType != null)
                {
                    charSpine = csT.GetComponent(sgType);
                    if (charSpine != null) spineTimeScale = sgType.GetField("timeScale");
                }
            }
            var ciT = c.Find("EventPanel/CharSpineIdle");
            charSpineIdleGO = ciT != null ? ciT.gameObject : null;
            if (charSpineIdleGO != null) charSpineIdleGO.SetActive(false);
            mapArea = c.Find("MapPanel/MapArea").GetComponent<RectTransform>();
            mapInfoName = c.Find("MapPanel/DiscriptionArea/ItemName")?.GetComponent<TextMeshProUGUI>();
            mapInfoDesc = c.Find("MapPanel/DiscriptionArea/ItemDiscription")?.GetComponent<TextMeshProUGUI>();
            // 가방 요소 — 오브젝트가 삭제/이동돼도 NRE 안 나도록 null-safe 바인딩
            gridRect    = c.Find("BagPanel/Grid")?.GetComponent<RectTransform>();
            slotsRect   = c.Find("BagPanel/Slots")?.GetComponent<RectTransform>();
            // Tray는 EventPanel/ItemTray(신규)를 우선, 없으면 BagPanel/Tray(구)로 폴백
            trayRect    = (c.Find("EventPanel/ItemTray") ?? c.Find("BagPanel/Tray"))?.GetComponent<RectTransform>();
            trashZone   = c.Find("BagPanel/Trash")?.GetComponent<RectTransform>();
            // DragLayer는 FlowCanvas 레벨을 우선, 없으면 BagPanel/DragLayer로 폴백
            dragLayer   = (c.Find("DragLayer") ?? c.Find("BagPanel/DragLayer"))?.GetComponent<RectTransform>();
            useBtn      = c.Find("BagPanel/UseBtn")?.GetComponent<Button>();
            useBtnLabel = c.Find("BagPanel/UseBtn/Label")?.GetComponent<TextMeshProUGUI>();

            Wire(c, "StartPanel/StartBtn", OnStartBtn);
            Wire(c, "CutscenePanel/CutNext", BeginOnboarding);
            if (c.Find("BagPanel/DayStart") != null) Wire(c, "BagPanel/DayStart", BagDone);
            Wire(c, "BagPanel/UseBtn", OnUseBtn);
            // BagToggle이 BagPanel/EventPanel 양쪽에 있을 수 있으므로 둘 다 배선(이벤트 중 보이는 건 EventPanel 쪽)
            if (c.Find("BagPanel/BagToggle") != null) Wire(c, "BagPanel/BagToggle", OnBagToggle);
            if (c.Find("EventPanel/BagToggle") != null) Wire(c, "EventPanel/BagToggle", OnBagToggle);
            Wire(c, "RestPanel/RestBag", () => ShowBag(ShowRest, "휴식으로 →"));
            Wire(c, "RestPanel/RestMap", ShowMap);
            Wire(c, "EndingPanel/EndingPanelBtn", Restart);
            Wire(c, "GameOverPanel/GameOverPanelBtn", Restart);
            transition = c.GetComponentInChildren<SeoulLast.ScreenSpeedTransition>(true);
            bagSkinManager = c.Find("BagPanel")?.GetComponent<BagSkinManager>();
            var nextBtnGO = c.Find("EventPanel/Card/CardBG_Result/NextBtn");
            if (nextBtnGO != null) { nextBtn = nextBtnGO.GetComponent<Button>(); nextBtn?.onClick.AddListener(OnNextBtn); }
        }

        void Wire(Transform root, string path, UnityEngine.Events.UnityAction action)
        {
            var t = root.Find(path);
            if (t == null) { Debug.LogWarning($"[GameFlow] 버튼 경로 없음: {path}"); return; }
            var b = t.GetComponent<Button>();
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(UIFactory.PlayClick);   // 클릭음
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
        void OnStartBtn() { BeginOnboarding(); }   // 컷씬 제거 — 시작 시 바로 온보딩

        // 온보딩(스테이지 0) 시작 — EVT-O001부터 그래프대로 진행
        void BeginOnboarding()
        {
            stageNo = 0; eventsThisStage = 0; stageRegion = "";
            forcedNextId = FindEvent("EVT-O001") != null ? "EVT-O001" : "";
            TransitionThenPlay();
        }

        // 가방 정비 화면. onDone = 완료 버튼 동작, label = 버튼 문구
        void ShowBag(System.Action onDone, string label, bool byItem = false)
        {
            bagOnDone = onDone;
            bagOpenedByItem = byItem;
            selectedItem = null;
            if (bagBtnLabel != null) bagBtnLabel.text = label;
            if (dayStartGO != null) dayStartGO.SetActive(true);   // 진행/닫기 버튼 보장(씬에서 꺼져 있어도)
            EnsureUseButton();   // 회복 아이템 '사용' 버튼(씬에서 삭제됐으면 런타임 생성)
            BuildBagScreen();
            if (bagPanelRT == null) InitBagPanelPos();
            bagPanel.SetActive(true);
            StartCoroutine(SlideBagCo(true));
        }

        void BagDone() { CloseBag(); }

        // 가방 닫기(공통): 미배치 잔여 아이템 폐기 → 슬라이드 다운 → 닫힘 콜백 실행
        void CloseBag()
        {
            if (isSlidingBag || bagPanel == null || !bagPanel.activeSelf) return;
            isSlidingBag = true;
            // 트레이에 남은(가방에 안 넣은) 아이템은 폐기
            if (trayRect != null)
                for (int i = trayRect.childCount - 1; i >= 0; i--)
                {
                    var ch = trayRect.GetChild(i);
                    if (ch.GetComponent<InvItemView>() != null) Destroy(ch.gameObject);
                }
            tray.Clear();
            var onDone = bagOnDone; bagOnDone = null; bagOpenedByItem = false;
            StartCoroutine(SlideBagCo(false, () => { if (onDone != null) onDone(); }));
        }

        // 그리드/트레이의 아이템 뷰를 모델에서 새로 생성
        void BuildBagScreen()
        {
            if (gridRect != null)
                for (int i = gridRect.childCount - 1; i >= 0; i--) Destroy(gridRect.GetChild(i).gameObject);
            if (trayRect != null)
                for (int i = trayRect.childCount - 1; i >= 0; i--)
                {
                    var ch = trayRect.GetChild(i);
                    if (ch.GetComponent<InvItemView>() != null) Destroy(ch.gameObject);
                }
            if (gridRect != null)
                foreach (var p in bag.Placed)
                {
                    var v = NewItemView(p); v.InBag = true;
                    v.transform.SetParent(gridRect, false); v.AttachToBag(p.Origin);
                }
            if (trayRect != null)
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
            if (trayRect == null) return;
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

        // 회복 아이템 '사용' 버튼 런타임 생성(씬에서 삭제된 경우 대비). 가방 하단 중앙.
        void EnsureUseButton()
        {
            if (useBtn != null || bagPanel == null) return;
            TextMeshProUGUI lbl;
            useBtn = UIFactory.Button(bagPanel.transform, "UseBtnRuntime", "사용할 회복 아이템을 선택",
                new Color(0.30f, 0.55f, 0.45f), OnUseBtn, out lbl);
            var rt = useBtn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(760, 110);
            rt.anchoredPosition = new Vector2(0, 30);   // 가방 하단 중앙
            lbl.fontSize = 32;
            useBtnLabel = lbl;
            useBtn.interactable = false;
            useBtn.transform.SetAsLastSibling();
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
            if (bagBody == null) return;
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
            // (가방에 넣어도 자동 닫지 않음 — 유저가 직접 닫기/다음으로 진행)
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

        void InitBagPanelPos()
        {
            bagPanelRT = bagPanel?.GetComponent<RectTransform>();
            if (bagPanelRT == null) return;
            float panelH = bagPanelRT.sizeDelta.y;
            // pivot(0.5,0), anchor(0,0)~(1,0) 기준
            // anchoredPos.y = 0 → 패널 하단이 화면 하단에 딱 맞음 (표시 위치)
            // anchoredPos.y = -(panelH + 50) → 완전히 화면 아래 (숨김 위치)
            bagShownY  = 0f;
            bagHiddenY = -(panelH + 50f);
            bagPanelRT.anchoredPosition = new Vector2(0f, bagHiddenY);
        }

        System.Collections.IEnumerator SlideBagCo(bool show, System.Action onDone = null)
        {
            if (bagPanelRT == null) { isSlidingBag = false; onDone?.Invoke(); yield break; }
            isSlidingBag = true;   // 슬라이드 동안 재진입(중복 토글) 방지
            float duration = show ? 0.30f : 0.25f;
            float startY   = bagPanelRT.anchoredPosition.y;
            float endY     = show ? bagShownY : bagHiddenY;
            float elapsed  = 0f;
            if (show) bagPanel.SetActive(true);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t    = Mathf.Clamp01(elapsed / duration);
                float ease = show ? (1f - Mathf.Pow(1f - t, 3f)) : (t * t * (3f - 2f * t));
                bagPanelRT.anchoredPosition = new Vector2(bagPanelRT.anchoredPosition.x, Mathf.Lerp(startY, endY, ease));
                yield return null;
            }
            bagPanelRT.anchoredPosition = new Vector2(bagPanelRT.anchoredPosition.x, endY);
            if (!show) bagPanel.SetActive(false);
            isSlidingBag = false;   // show/hide 모두 해제 (hide 후에도 풀어 다음 BagDone이 막히지 않도록)
            onDone?.Invoke();
        }

        // 다음 이벤트 재생 (forcedNextId 또는 "random" 해석). 정해진 게 없으면 휴식.
        // 화면 전환 연출 후 다음 이벤트 진행
        void TransitionThenPlay()
        {
            if (transition != null)
                transition.Play(onCovered: PlayNextEvent);
            else
                PlayNextEvent();
        }

        void PlayNextEvent()
        {
            tray.Clear();
            string id = forcedNextId; forcedNextId = "";
            if (id == "random") id = ResolveRandom();
            curEvent = string.IsNullOrEmpty(id) ? null : FindEvent(id);
            if (curEvent == null) { ShowRest(); return; }
            // 가방 업그레이드 이벤트 감지
            if (curEvent.eventId == "EVT-U001" || curEvent.eventId == "EVT-U002" ||
                curEvent.eventId == "EVT-U003" || curEvent.eventId == "EVT-U004")
                bagSkinManager?.LevelUp();
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
            SetCardBG(false, false);
            eventTitle.text = (curDialog != null && !string.IsNullOrEmpty(curDialog.spawnItemId)) ? "무언가를 발견했다!" : "";
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
            if (choiceArea != null) choiceArea.gameObject.SetActive(false);   // 타이핑 끝난 뒤 노출
            Only(eventPanel);
            UpdateTopBar();
            UpdateStatusRail();
            StartApproach();   // 걷기 + 아이템 접근 후 카드 표시 → 텍스트 타이핑
        }

        // ---------- Explore 연출 ----------
        void Update()
        {
            if (walking && bgRaw != null)
            {
                var r = bgRaw.uvRect; r.x += SCROLL_SPEED * Time.deltaTime; bgRaw.uvRect = r;
            }
            // 이벤트 카드(만남) 중엔 walking=false → Spine 정지, 이동 중엔 재생
            if (charSpine != null && spineTimeScale != null)
                spineTimeScale.SetValue(charSpine, walking ? 1f : 0f);
            // 타이핑 중 터치/클릭하면 즉시 전체 표시
            if (revealing && TapDown()) FinishReveal();
        }

        // 클릭/터치 다운 (Input System / 레거시 모두 대응)
        static bool TapDown()
        {
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) return true;
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) return true;
            return false;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        void StartApproach()
        {
            EnsureFxObjects();
            bool stat = IsStaticScene();   // O001-01~08: 정면 idle + 배경 정지
            SetCharMode(stat);
            walking = !stat;               // static이면 걷지 않음(배경도 정지)
            if (eventCard != null) eventCard.gameObject.SetActive(false);
            if (overheadFx != null) overheadFx.gameObject.SetActive(false);
            if (pungFx != null) pungFx.gameObject.SetActive(false);
            if (itemImg != null) { itemImg.gameObject.SetActive(false); itemImg.rectTransform.localScale = Vector3.one; }
            StopAllCoroutines();
            StartCoroutine(SequenceCo(stat));
        }

        // 정면 idle + 배경 정지로 둘 이벤트인가 (온보딩 O001-01 ~ O001-08)
        bool IsStaticScene()
        {
            string id = curDialog != null ? curDialog.dialogId : null;
            if (string.IsNullOrEmpty(id) || !id.StartsWith("O001-")) return false;
            int n;
            return int.TryParse(id.Substring(5), out n) && n >= 1 && n <= 8;
        }

        // idle(정면) 스켈레톤 ↔ walk 스켈레톤 전환
        void SetCharMode(bool idle)
        {
            bool haveIdle = charSpineIdleGO != null;
            // idle을 원하지만 idle 스켈레톤이 없으면, walk를 끄지 않고 그대로 보여준다(캐릭터 사라짐 방지)
            if (charSpine != null) charSpine.gameObject.SetActive(!idle || !haveIdle);
            if (charSpineIdleGO != null) charSpineIdleGO.SetActive(idle);
        }

        // CharSpineIdle이 씬에 없으면 idleSkeletonData로 런타임 생성(리플렉션, Spine 의존 회피)
        void EnsureCharIdle()
        {
            if (charSpineIdleGO != null || idleSkeletonData == null || eventPanel == null) return;
            var sgType = System.Type.GetType("Spine.Unity.SkeletonGraphic, spine-unity");
            if (sgType == null) return;
            var mNew = sgType.GetMethod("NewSkeletonGraphicGameObject",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (mNew == null) return;
            var sgi = mNew.Invoke(null, new object[] { idleSkeletonData, eventPanel.transform, null }) as Component;
            if (sgi == null) return;
            sgi.gameObject.name = "CharSpineIdle";
            sgType.GetField("startingAnimation")?.SetValue(sgi, "robi_idle");
            sgType.GetField("startingLoop")?.SetValue(sgi, true);
            sgType.GetMethod("Initialize", new[] { typeof(bool) })?.Invoke(sgi, new object[] { true });
            var rti = sgType.GetProperty("rectTransform")?.GetValue(sgi) as RectTransform;
            if (rti != null)
            {
                rti.anchorMin = new Vector2(0, 1); rti.anchorMax = new Vector2(0, 1); rti.pivot = new Vector2(0.5f, 0f);
                rti.localScale = Vector3.one * 0.6f; rti.anchoredPosition = new Vector2(-120, -700);
            }
            charSpineIdleGO = sgi.gameObject;
            charSpineIdleGO.SetActive(false);
        }

        // 머리 위 말풍선/느낌표, (아이템 이벤트면) 우측 연기와 함께 아이템 등장 → 카드/텍스트
        IEnumerator SequenceCo(bool stat)
        {
            bool hasItem = curDialog != null && !string.IsNullOrEmpty(curDialog.spawnItemId);

            // 1) 걷기 후 멈춤 (static이면 걷지 않고 짧은 텀만)
            if (!stat) { yield return new WaitForSeconds(0.9f); walking = false; }
            else yield return new WaitForSeconds(0.3f);

            // 2) 머리 위 연출 — 아이템이면 느낌표, 아니면 말풍선.
            //    멈춘 직후 떠서 다음 걷기 시작(StartApproach)까지 계속 표시·순환.
            ShowOverhead(hasItem ? exclamFrames : speechFrames);
            yield return new WaitForSeconds(0.4f);

            // 3) 아이템이 있으면 우측에서 연기(pung)와 함께 뿅 등장
            if (hasItem) yield return StartCoroutine(PopItem());

            // 4) 카드 표시 + 텍스트 타이핑 (말풍선은 그대로 유지)
            if (eventCard != null) eventCard.gameObject.SetActive(true);
            StartReveal();
        }

        Coroutine overheadAnim;

        // 머리 위 말풍선/느낌표를 띄우고 프레임을 계속 순환. 숨김은 다음 걷기 시작 때(StartApproach).
        void ShowOverhead(Sprite[] frames)
        {
            if (overheadFx == null) return;
            if (frames == null || frames.Length == 0) { overheadFx.gameObject.SetActive(false); return; }
            overheadFx.gameObject.SetActive(true);
            if (overheadAnim != null) StopCoroutine(overheadAnim);
            overheadAnim = StartCoroutine(OverheadAnimCo(frames));
        }

        IEnumerator OverheadAnimCo(Sprite[] frames)
        {
            int i = 0; const float ft = 0.12f;
            overheadFx.sprite = frames[0];
            while (overheadFx != null && overheadFx.gameObject.activeSelf)
            {
                yield return new WaitForSeconds(ft);
                i = (i + 1) % frames.Length;
                if (overheadFx != null) overheadFx.sprite = frames[i];
            }
        }

        // 우측에서 아이템이 연기(pung)와 함께 뿅 등장(스케일 팝)
        IEnumerator PopItem()
        {
            if (itemImg != null)
            {
                var d = FindItemData(curDialog.spawnItemId);
                if (d != null && d.icon != null) { itemImg.sprite = d.icon; itemImg.color = Color.white; }
                PlaceAtTopHalfCenter(itemImg.rectTransform);   // 상단 영역 중앙에 등장
                itemImg.rectTransform.localScale = Vector3.zero;
                itemImg.gameObject.SetActive(true);
            }
            bool hasPung = pungFx != null && pungFrames != null && pungFrames.Length > 0;
            if (hasPung) { pungFx.sprite = pungFrames[0]; PlaceAtTopHalfCenter(pungFx.rectTransform); pungFx.gameObject.SetActive(true); }
            var irt = itemImg != null ? itemImg.rectTransform : null;
            float t = 0, dur = 0.45f, acc = 0; const float ft = 0.1f; int pi = 0;
            while (t < dur)
            {
                t += Time.deltaTime; acc += Time.deltaTime;
                if (hasPung && acc >= ft) { acc = 0; pi = Mathf.Min(pi + 1, pungFrames.Length - 1); pungFx.sprite = pungFrames[pi]; }
                if (irt != null) irt.localScale = Vector3.one * Pop(t / dur);
                yield return null;
            }
            if (irt != null) irt.localScale = Vector3.one;
            if (pungFx != null) pungFx.gameObject.SetActive(false);
        }

        // RectTransform을 부모(EventPanel) '상단 영역(상반부) 중앙'에 배치.
        // 1080×2400 기준: 가로 중앙(540), 세로는 상반부 중앙(상단에서 ph/4 = 600).
        void PlaceAtTopHalfCenter(RectTransform rt)
        {
            if (rt == null) return;
            var prt = rt.parent as RectTransform;
            float pw = prt != null ? prt.rect.width : W;
            float ph = prt != null ? prt.rect.height : 2400f;
            // 앵커/피벗을 좌상단(0,1)으로 고정 후, 좌상단 코너 = 목표중심 - 크기/2
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pw * 0.5f - rt.sizeDelta.x * 0.5f, -(ph * 0.25f - rt.sizeDelta.y * 0.5f));
        }

        // 0→1 살짝 튀는 back-out 이징(뿅)
        static float Pop(float p)
        {
            p = Mathf.Clamp01(p); const float s = 1.7f; float q = p - 1f;
            return 1f + (s + 1f) * q * q * q + s * q * q;
        }

        // 머리 위/우측 FX 이미지를 런타임에 1회 생성(씬/코드 빌드 공통)
        void EnsureFxObjects()
        {
            if (eventPanel == null) return;
            EnsureCharIdle();   // idle 스켈레톤이 씬에서 유실됐으면 런타임 생성
            var ep = eventPanel.transform;
            if (overheadFx == null)
            {
                overheadFx = UIFactory.Img(ep, "OverheadFx", Color.white);
                UIFactory.SetRect(overheadFx.rectTransform, 190, 540, 220, 220);
                overheadFx.raycastTarget = false; overheadFx.gameObject.SetActive(false);
            }
            if (pungFx == null)
            {
                pungFx = UIFactory.Img(ep, "PungFx", Color.white);
                UIFactory.SetRect(pungFx.rectTransform, 805, 805, 240, 240);
                pungFx.raycastTarget = false; pungFx.gameObject.SetActive(false);
            }
            EnsureTray();
            EnsureNextButton();
        }

        // 트레이가 씬에 없으면(그리드 전용 새 BagPanel) 런타임 생성. 가방 상단 스트립에 배치.
        void EnsureTray()
        {
            if (trayRect != null || bagPanel == null) return;
            var bg = UIFactory.Img(bagPanel.transform, "ItemTray", new Color(0f, 0f, 0f, 0.30f));
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(900f, 240f);
            rt.anchoredPosition = new Vector2(90f, -20f);
            bg.raycastTarget = false;
            rt.SetAsLastSibling();          // 그리드 위에 보이도록
            trayRect = rt;
        }

        // 이벤트 화면 하단 '다음' 버튼 런타임 생성(숨김). 가방 닫은 뒤 진행용.
        void EnsureNextButton()
        {
            if (nextEventBtn != null || eventPanel == null) return;
            TextMeshProUGUI lbl;
            var btn = UIFactory.Button(eventPanel.transform, "NextEventBtn", "다음 →",
                new Color(0.85f, 0.55f, 0.25f), OnNextEventBtn, out lbl);
            UIFactory.SetRect(btn.GetComponent<RectTransform>(), (W - 520) / 2f, 2120, 520, 150);
            lbl.fontSize = 44;
            nextEventBtn = btn.gameObject;
            nextEventBtn.transform.SetAsLastSibling();
            nextEventBtn.SetActive(false);
        }

        void ShowNextButton(string nextId)
        {
            EnsureNextButton();
            pendingNextId = nextId;
            if (nextEventBtn != null) { nextEventBtn.SetActive(true); nextEventBtn.transform.SetAsLastSibling(); }
        }

        void OnNextEventBtn()
        {
            if (nextEventBtn != null) nextEventBtn.SetActive(false);
            var n = pendingNextId; pendingNextId = null;
            StartDialog(n);
        }

        // ---------- 텍스트 타이핑 연출 (터치 시 스킵) ----------
        bool revealing;
        Coroutine revealRoutine;

        void StartReveal()
        {
            if (eventBody == null) { if (choiceArea != null) choiceArea.gameObject.SetActive(true); return; }
            eventBody.ForceMeshUpdate();
            int total = eventBody.textInfo.characterCount;
            if (total <= 0) { revealing = false; if (choiceArea != null) choiceArea.gameObject.SetActive(true); return; }
            eventBody.maxVisibleCharacters = 0;
            revealing = true;
            if (typingSource != null && typingClip != null && !typingSource.isPlaying) typingSource.Play();   // 타이핑 사운드
            revealRoutine = StartCoroutine(RevealCo(total));
        }

        IEnumerator RevealCo(int total)
        {
            int vis = 0;
            while (vis < total)
            {
                vis += 2;                       // 디지털 타이핑(2글자씩 빠르게)
                eventBody.maxVisibleCharacters = vis;
                yield return new WaitForSeconds(0.02f);
            }
            FinishReveal();
        }

        void FinishReveal()
        {
            revealing = false;
            if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
            if (typingSource != null && typingSource.isPlaying) typingSource.Stop();   // 타이핑 사운드 정지
            if (eventBody != null) eventBody.maxVisibleCharacters = 99999;
            if (choiceArea != null)
            {
                choiceArea.gameObject.SetActive(true);   // 선택지는 타이핑 끝나고 노출
                StartCoroutine(PopChoices());            // 버튼 하나씩 뿅 생성
            }
        }

        // 선택지 버튼을 하나씩 스케일 팝으로 등장
        IEnumerator PopChoices()
        {
            if (choiceArea == null) yield break;
            int n = choiceArea.childCount;
            var rts = new RectTransform[n];
            for (int i = 0; i < n; i++) { rts[i] = choiceArea.GetChild(i) as RectTransform; if (rts[i] != null) rts[i].localScale = Vector3.zero; }
            for (int i = 0; i < n; i++)
            {
                var rt = rts[i]; if (rt == null) continue;
                float t = 0; const float dur = 0.16f;
                while (t < dur) { t += Time.deltaTime; rt.localScale = Vector3.one * Pop(t / dur); yield return null; }
                rt.localScale = Vector3.one;
            }
        }

        IEnumerator FlyItemToBag(System.Action done)
        {
            var rt = itemImg != null ? itemImg.rectTransform : null;
            Vector2 start = rt != null ? rt.anchoredPosition : Vector2.zero;
            Vector2 end = new Vector2((W - 170) / 2f, -1700);
            float t = 0, dur = 0.4f;
            while (t < dur)
            {
                t += Time.deltaTime;
                if (rt != null) rt.anchoredPosition = Vector2.Lerp(start, end, t / dur);
                yield return null;
            }
            if (rt != null) rt.gameObject.SetActive(false);
            if (done != null) done();
        }

        void UpdateTopBar()
        {
            if (dayText == null) return;
            string place = stageNo == 0 ? "체육창고" : (string.IsNullOrEmpty(stageLocationName) ? "어딘가" : stageLocationName);
            dayText.text = stageNo == 0 ? place : $"DAY {stageNo}     {place}";   // 온보딩/진행도 표기 제거
        }

        void UpdateStatusRail()
        {
            for (int i = 0; i < 4; i++)
            {
                if (statusIcon[i] == null) continue;
                int lv = Level(status[i]);
                if (lv == 0) { statusIcon[i].Hide_Icon(); statusLevel[i] = 0; }
                else { bool upgraded = (lv == 2); if (statusLevel[i] == 0 || statusLevel[i] < lv) statusIcon[i].Show_Icon(upgraded); if (statusLabel[i] != null) statusLabel[i].text = StatRail[i] + (lv == 2 ? " !!" : " !"); statusLevel[i] = lv; }
            }
        }

        void OnNextBtn()
        {
            if (nextBtn != null) nextBtn.gameObject.SetActive(false);
            TransitionThenPlay();
        }

        void OnBagToggle()
        {
            if (isSlidingBag) return;   // 슬라이드 중 중복 입력 무시
            // 열려 있으면 닫기(닫힘 콜백 실행 → 아이템 컨텍스트면 다음 버튼 표시), 닫혀 있으면 열기
            if (bagPanel != null && bagPanel.activeSelf) { CloseBag(); return; }
            ShowBag(() => { Only(eventPanel); if (eventCard != null) eventCard.gameObject.SetActive(true); }, "닫기 ▼");
        }

        // NextEventId == "random": 온보딩 제외, (현재 지역의 특정지역 + 일반) 중 랜덤
        string ResolveRandom()
        {
            var pool = new List<EventData>();
            foreach (var ev in events)
            {
                if (ev == null || ev.eventType == "온보딩" || ev.eventType == "시나리오") continue; // 시나리오는 첫 이벤트 전용
                if (string.IsNullOrEmpty(ev.startDialogId)) continue;   // 대화 없는(빈) 이벤트는 제외
                bool general = ev.eventType == "일반";
                bool regionMatch = ev.eventType == "특정지역" && ev.region == stageRegion;
                if (general || regionMatch) pool.Add(ev);
            }
            return pool.Count == 0 ? "" : pool[rng.Next(pool.Count)].eventId;
        }

        // 해당 장소의 시나리오 이벤트(스테이지 첫 이벤트). 없으면 "".
        string FindScenarioEvent(string locId)
        {
            foreach (var ev in events)
                if (ev != null && ev.eventType == "시나리오" && ev.region == locId && !string.IsNullOrEmpty(ev.startDialogId))
                    return ev.eventId;
            return "";
        }

        // 이벤트(대화 그래프) 종료 → 스테이지 진행(다음 이벤트/휴식)
        void EndEvent() { AfterEventResolve(""); }

        // 대화 분기 선택
        void OnDialogBranch(EventChoice c)
        {
            SetCardBG(false, true);
            if (!string.IsNullOrEmpty(c.requiredItem)) ConsumeUse(c.requiredItem);
            ApplyNewState(c.newState);

            string next = c.nextEventId;   // 다음 대화 id ("Done"/빈값 = 이벤트 종료)
            if (c.opensInventory)
            {
                // 아이템이 하단으로 날아간 뒤 → 가방이 올라오며 트레이에 부착(유저가 넣을지 선택)
                // → 가방 닫으면 '다음' 버튼으로 진행
                string spawn = curDialog != null ? curDialog.spawnItemId : "";
                if (eventCard != null) eventCard.gameObject.SetActive(false);
                StartCoroutine(FlyItemToBag(() => { GrantObjectItem(spawn); ShowBag(() => ShowNextButton(next), "닫기 ▼", byItem: true); }));
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
            def.Icon = d.icon;

            // 회복 매핑 (이름/타입 기반) — 회복류는 1회 소모
            string n = d.itemName ?? "";
            if (n.Contains("빵") || n.Contains("통조림") || n.Contains("라면")) { def.RecoverStat = 0; def.RecoverAmt = 35; def.MaxUses = 1; def.Consumable = true; }
            else if (n.Contains("음료") || n.Contains("생수") || n.Contains("물")) { def.RecoverStat = 1; def.RecoverAmt = 35; def.MaxUses = 1; def.Consumable = true; }
            else if (d.itemType == "회복" || n.Contains("구급") || n.Contains("붕대")) { def.RecoverStat = 2; def.RecoverAmt = 45; def.MaxUses = 1; def.Consumable = true; }
            else if (n.Contains("각성")) { def.RecoverStat = 3; def.RecoverAmt = 40; def.MaxUses = 1; def.Consumable = true; }
            else
            {
                // 도구: durability>0이면 그만큼 소모(내구도), <=0(손상없음)이면 무한
                bool wears = d.durability > 0;
                def.Consumable = wears;
                def.MaxUses = wears ? d.durability : 9999;
            }
            return def;
        }

        // 이벤트 등장 물건을 트레이로 지급
        void GrantObjectItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            var d = FindItemData(itemId);
            if (d == null) { Debug.LogWarning($"[GameFlow] 아이템 데이터 없음: {itemId} (Items 배열/시트 확인)"); return; }
            // 획득 아이템은 항상 트레이로(자동 장착 X). 유저가 가방에 넣을지 선택.
            tray.Add(new PlacedItem(DefFromItemData(d)));
            // 가방 업그레이드 아이템 감지
            if (itemId == "BAG001" || itemId == "BAG002" ||
                itemId == "BAG003" || itemId == "BAG004")
                bagSkinManager?.LevelUp();
        }

        void AfterEventResolve(string nextId)
        {
            // 실전 스테이지만: 사망 판정(선택 결과/유발 상태이상) + 5이벤트 카운트
            // 시간 경과 악화는 매 이벤트가 아니라 1 Day(스테이지) 단위(StartStage)에서 처리
            if (stageNo >= 1)
            {
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
            TransitionThenPlay();
        }

        // 한 스테이지(5이벤트) 완료 → 휴식
        // 휴식 화면 제거 — 이벤트 종료 후 곧바로 지도(MapPanel)로 이동
        void ShowRest() { ShowMap(); }

        LocationData FindLocation(string id)
        {
            if (locations == null || string.IsNullOrEmpty(id)) return null;
            foreach (var l in locations) if (l != null && l.locationId == id) return l;
            return null;
        }

        // 선택 지역의 LocationAssetName으로 이벤트 배경(bgRaw) 교체
        void ApplyAreaBackground(string locId)
        {
            if (bgRaw == null) return;
            var loc = FindLocation(locId);
            var tex = loc != null ? LoadAreaBg(loc.assetName) : null;
            if (tex != null) { bgRaw.texture = tex; bgRaw.color = Color.white; }
        }

        // art/Backgrond 에서 배경 텍스처 로드. Resources 우선(빌드 대응), 에디터는 직접 경로.
        Texture2D LoadAreaBg(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            var t = Resources.Load<Texture2D>("Backgrond/" + assetName);
            if (t != null) return t;
#if UNITY_EDITOR
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                var tx = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/art/Backgrond/" + assetName + ext);
                if (tx != null) return tx;
            }
#endif
            return null;
        }

        // 지도에서 장소 선택 → 다음 스테이지 시작 (첫 이벤트 = 그 장소 시나리오)
        void StartStage(string locId, string locName)
        {
            stageNo++;
            stageRegion = locId;          // 매칭 키 = LocationID (이벤트 EventRegion과 일치)
            stageLocationName = locName;  // 표시명
            eventsThisStage = 0;
            ApplyAreaBackground(locId);   // 선택 지역의 배경 이미지로 교체
            // 1 Day(스테이지)마다 허기·갈증 악화(+5). 100 도달 시 사망.
            status[0] = Clamp(status[0] + 5);
            status[1] = Clamp(status[1] + 5);
            for (int i = 0; i < 4; i++) if (status[i] >= 100) { ShowGameOver(i); return; }
            // 스테이지 첫 이벤트 = 그 장소 시나리오(없으면 랜덤)
            string sc = FindScenarioEvent(locId);
            forcedNextId = !string.IsNullOrEmpty(sc) ? sc : "random";
            TransitionThenPlay();   // 화면 전환 후 첫 이벤트(팀원)
        }

        void ShowMap()
        {
            EnsureMapGoButton();
            CleanMapInfo();
            selLocId = null; selLocName = null;
            if (mapGoBtn != null) mapGoBtn.SetActive(false);
            // 맵 이미지가 버튼을 가리지 않도록 맨 뒤로 + 클릭 통과
            var miT = mapPanel.transform.Find("MapImg");
            if (miT != null) { miT.SetAsFirstSibling(); var mig = miT.GetComponent<UnityEngine.UI.Graphic>(); if (mig != null) mig.raycastTarget = false; }
            for (int i = mapArea.childCount - 1; i >= 0; i--) Destroy(mapArea.GetChild(i).gameObject);

            // 존재하는 층 목록(오름차순). 4·5층은 없으므로 1~3층만.
            var floors = new SortedSet<int>();
            if (locations != null) foreach (var l in locations) if (l != null) { int fl = FloorOf(l); if (fl >= 1 && fl <= 3) floors.Add(fl); }
            if (floors.Count == 0) { Only(mapPanel); return; }

            // 상단: 층 탭 버튼 한 줄 (1층/2층/3층 …)
            const float areaW = W - 160f, tabGap = 12f, tabH = 110f;
            int nf = floors.Count;
            float tabW = (areaW - tabGap * (nf - 1)) / nf;
            int idx = 0;
            foreach (var f in floors)
            {
                int ff = f;
                TextMeshProUGUI tl; var tb = UIFactory.Button(mapArea, "floorTab", FloorName(f), new Color(0.25f, 0.40f, 0.55f), () => RebuildRegions(ff, true), out tl);
                UIFactory.SetRect(tb.GetComponent<RectTransform>(), idx * (tabW + tabGap), 0, tabW, tabH);
                idx++;
            }

            int first = int.MaxValue; foreach (var f in floors) if (f < first) first = f;
            RebuildRegions(first, false);   // 첫 층 버튼은 보이되, 하단은 '장소를 선택해주세요'
            Only(mapPanel);
        }

        int curMapFloor;

        // 선택한 층의 지역 버튼만 다시 그림. floorInfo=true면 하단에 층 정보,
        // false(처음 진입)면 '장소를 선택해주세요' 안내.
        void RebuildRegions(int floor, bool floorInfo)
        {
            curMapFloor = floor;
            selLocId = null; selLocName = null;
            if (mapGoBtn != null) mapGoBtn.SetActive(false);
            // 기존 지역 버튼만 제거(층 탭은 유지)
            for (int i = mapArea.childCount - 1; i >= 0; i--)
            { var ch = mapArea.GetChild(i); if (ch.name == "loc") Destroy(ch.gameObject); }

            var list = new List<LocationData>();
            if (locations != null) foreach (var l in locations) if (l != null && FloorOf(l) == floor) list.Add(l);

            const float areaW = W - 160f, btnW = 290f, btnH = 110f, gapX = 15f, gapY = 14f, top = 140f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((areaW + gapX) / (btnW + gapX)));
            for (int i = 0; i < list.Count; i++)
            {
                int col = i % perRow, row = i / perRow; var loc = list[i];
                var c0 = loc.isLock ? new Color(0.28f, 0.30f, 0.34f) : new Color(0.32f, 0.45f, 0.6f);
                TextMeshProUGUI tl; var b = UIFactory.Button(mapArea, "loc", loc.locationName, c0, () => SelectRegion(loc), out tl);
                UIFactory.SetRect(b.GetComponent<RectTransform>(), col * (btnW + gapX), top + row * (btnH + gapY), btnW, btnH);
            }

            // 하단: 층 정보(탭 선택 시) 또는 안내 멘트(처음 진입/선택 없음)
            if (floorInfo)
            {
                if (mapInfoName != null) mapInfoName.text = FloorName(floor);
                if (mapInfoDesc != null)
                {
                    var names = new List<string>(); foreach (var l in list) names.Add(l.locationName);
                    mapInfoDesc.text = $"지역 {list.Count}곳: " + string.Join(", ", names);
                }
            }
            else
            {
                if (mapInfoName != null) mapInfoName.text = "";
                if (mapInfoDesc != null) mapInfoDesc.text = "장소를 선택해주세요";
            }
        }

        // floor 데이터가 없으면(0) LocationID(LocNNN)에서 층 추정: 1~10=1층, 11~20=2층, …
        int FloorOf(LocationData l)
        {
            if (l == null) return 1;
            if (l.floor > 0) return l.floor;
            var id = l.locationId ?? "";
            int n;
            if (id.StartsWith("Loc") && int.TryParse(id.Substring(3), out n) && n > 0) return (n - 1) / 10 + 1;
            return 1;
        }

        // 층 번호 → 표시 이름. 특수층은 여기서 매핑(필요 시 확장).
        string FloorName(int f)
        {
            if (f <= 0) return "지역";
            return f + "층";
        }

        // 하단 정보영역: 흰 빈 이미지/장식 숨기고 이름·설명 중앙 정렬
        void CleanMapInfo()
        {
            if (mapPanel == null) return;
            var da = mapPanel.transform.Find("DiscriptionArea");
            if (da == null) return;
            foreach (Transform ch in da)
                if (ch.name == "AreaImg" || ch.name == "Divider" || ch.name == "ItemText") ch.gameObject.SetActive(false);
            CenterText(mapInfoName, 140, 80);
            CenterText(mapInfoDesc, -30, 360);
        }

        void CenterText(TextMeshProUGUI t, float y, float h)
        {
            if (t == null) return;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(940, h);
            rt.anchoredPosition = new Vector2(0, y);
            t.alignment = TextAlignmentOptions.Center;
        }

        // 지역 버튼 선택 → 하단 정보 표시 + 출발 버튼 노출
        void SelectRegion(LocationData l)
        {
            if (l == null) return;
            selLocId = l.locationId; selLocName = l.locationName;
            string lockTag = l.isLock ? "  <color=#cc5555>(잠김)</color>" : "";
            if (mapInfoName != null) mapInfoName.text = $"{l.locationName}{lockTag}";
            if (mapInfoDesc != null) mapInfoDesc.text = string.IsNullOrEmpty(l.description) ? l.locationName : l.description;
            // 잠긴 지역은 출발 불가(정보만 표시)
            if (mapGoBtn != null) { mapGoBtn.SetActive(!l.isLock); if (!l.isLock) mapGoBtn.transform.SetAsLastSibling(); }
        }

        // 지도 하단 '출발' 버튼 런타임 생성(숨김). 선택 지역으로 스테이지 시작.
        void EnsureMapGoButton()
        {
            if (mapGoBtn != null || mapPanel == null) return;
            TextMeshProUGUI lbl;
            var btn = UIFactory.Button(mapPanel.transform, "MapGoBtn", "출발 →",
                new Color(0.85f, 0.55f, 0.25f), OnMapGo, out lbl);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(460, 140);
            rt.anchoredPosition = new Vector2(0, 60);   // 화면 하단 중앙
            lbl.fontSize = 44;
            mapGoBtn = btn.gameObject;
            mapGoBtn.SetActive(false);
        }

        void OnMapGo()
        {
            if (string.IsNullOrEmpty(selLocId) && string.IsNullOrEmpty(selLocName)) return;
            if (mapGoBtn != null) mapGoBtn.SetActive(false);
            StartStage(selLocId, selLocName);
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

        // 상태 임계치(Level과 일치): 주의 30, 위험 60
        const int T_CAUTION = 30, T_DANGER = 60;

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
                if (Match(p, key) && p.Uses > 0) { if (!p.Def.Consumable) return; p.Uses--; if (p.Uses <= 0) bag.RemoveFromBag(p); return; }
            foreach (var p in tray)
                if (Match(p, key) && p.Uses > 0) { if (!p.Def.Consumable) return; p.Uses--; if (p.Uses <= 0) tray.Remove(p); return; }
        }

        // ---------- 상태/텍스트 ----------
        static int Clamp(int v) => Mathf.Clamp(v, 0, 100);
        static int Level(int v) => v >= 60 ? 2 : v >= 30 ? 1 : 0;   // 위험 60 / 주의 30
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
        // CardBG 상태 일괄 제어
        void SetCardBG(bool choice, bool result)
        {
            if (cardBGChoice != null) cardBGChoice.SetActive(choice);
            if (cardBGResult != null) cardBGResult.SetActive(result);
            // Base: 둘 다 꺼졌을 때만 ON
            if (cardBGBase   != null) cardBGBase.SetActive(!choice && !result);
        }

        void ClearChoices() { choiceCount = 0; for (int i = choiceArea.childCount - 1; i >= 0; i--) Destroy(choiceArea.GetChild(i).gameObject); }

        void AddChoice(EventChoice c, int index, bool enabled, bool gatedMissing)
        {
            SetCardBG(true, false);
            string lbl = gatedMissing ? $"{c.label}   (필요: {DisplayReq(c.requiredItem)})" : c.label;
            var go = (GameObject)UnityEngine.Object.Instantiate(choiceButtonPrefab, choiceArea);
            go.name = "choice" + index;
            UIFactory.SetRect(go.GetComponent<RectTransform>(), 0, ChoiceY(), 920, 110);
            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = lbl;
            var btn = go.GetComponent<UnityEngine.UI.Button>();
            // 프리팹 BG의 raycastTarget이 꺼져 있으면 클릭이 안 잡힘 → 켜서 클릭 영역 확보
            var bgImg = go.GetComponentInChildren<UnityEngine.UI.Image>();
            if (bgImg != null) { bgImg.raycastTarget = true; btn.targetGraphic = bgImg; }
            btn.interactable = enabled;
            if (enabled) { var cc = c; btn.onClick.AddListener(UIFactory.PlayClick); btn.onClick.AddListener(() => OnDialogBranch(cc)); }
        }

        int choiceCount;
        int ChoiceY() { int y = choiceCount * 122; choiceCount++; return y; }

        void AddConfirm(UnityEngine.Events.UnityAction onConfirm)
        {
            choiceCount = 0;
            SetCardBG(true, false);
            var go = (GameObject)UnityEngine.Object.Instantiate(choiceButtonPrefab, choiceArea);
            go.name = "Confirm";
            UIFactory.SetRect(go.GetComponent<RectTransform>(), (920 - 460) / 2, 0, 460, 120);
            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = "확인";
            var btn = go.GetComponent<UnityEngine.UI.Button>();
            var bgImg = go.GetComponentInChildren<UnityEngine.UI.Image>();
            if (bgImg != null) { bgImg.raycastTarget = true; btn.targetGraphic = bgImg; }
            if (onConfirm != null) btn.onClick.AddListener(onConfirm);
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
            var h = UIFactory.Label(p.transform, "H", "휴식", 46, TextAlignmentOptions.Top, new Color(0.8f, 0.92f, 0.95f));
            UIFactory.SetRect(h.rectTransform, 60, 130, W - 120, 80);
            var box = UIFactory.Panel(p.transform, "Box", new Color(1, 1, 1, 0.08f));
            UIFactory.SetRect(box.rectTransform, 90, 250, W - 180, 700);
            restBody = UIFactory.Label(box.transform, "B", "", 33, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.92f, 0.95f));
            UIFactory.SetRect(restBody.rectTransform, 40, 40, W - 260, 620);
            TextMeshProUGUI bl; var b1 = UIFactory.Button(p.transform, "RestBag", "가방 정비", new Color(0.3f, 0.55f, 0.45f), () => ShowBag(ShowRest, "휴식으로 →"), out bl);
            UIFactory.SetRect(b1.GetComponent<RectTransform>(), (W - 460) / 2f, 1300, 460, 140);
            TextMeshProUGUI bl2; var b2 = UIFactory.Button(p.transform, "RestMap", "지도 → 다음 장소", new Color(0.85f, 0.55f, 0.3f), ShowMap, out bl2);
            UIFactory.SetRect(b2.GetComponent<RectTransform>(), (W - 460) / 2f, 1470, 460, 140);
            return p.gameObject;
        }

        GameObject BuildMapPanel(Transform root)
        {
            var p = UIFactory.Panel(root, "MapPanel", new Color(0.09f, 0.11f, 0.14f)); UIFactory.Fill(p.rectTransform);
            var h = UIFactory.Label(p.transform, "H", "지도 — 다음 장소 선택", 42, TextAlignmentOptions.Top, new Color(0.85f, 0.9f, 0.95f));
            UIFactory.SetRect(h.rectTransform, 60, 150, W - 120, 80);
            var areaGO = new GameObject("MapArea", typeof(RectTransform));
            mapArea = areaGO.GetComponent<RectTransform>(); mapArea.SetParent(p.transform, false);
            UIFactory.SetRect(mapArea, 80, 320, W - 160, 1400);
            return p.gameObject;
        }

        GameObject Simple(Transform root, string name, Color bg, string text, string btn, UnityEngine.Events.UnityAction onClick)
        {
            var p = UIFactory.Panel(root, name, bg); UIFactory.Fill(p.rectTransform);
            var t = UIFactory.Label(p.transform, "T", text, 40, TextAlignmentOptions.Center, Color.white);
            UIFactory.SetRect(t.rectTransform, 80, 500, W - 160, 520);
            TextMeshProUGUI bl; var b = UIFactory.Button(p.transform, name + "Btn", btn, new Color(0.3f, 0.55f, 0.45f), onClick, out bl);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (W - 460) / 2, 1250, 460, 150);
            // 스타트 버튼 이름 고정 (테스트용)
            if (name == "StartPanel") b.gameObject.name = "StartBtn";
            if (name == "CutscenePanel") b.gameObject.name = "CutNext";
            return p.gameObject;
        }

        GameObject BuildText(Transform root, string name, Color bg, string head, Color headCol, out TextMeshProUGUI body, string btn, UnityEngine.Events.UnityAction onClick)
        {
            var p = UIFactory.Panel(root, name, bg); UIFactory.Fill(p.rectTransform);
            var h = UIFactory.Label(p.transform, "H", head, 46, TextAlignmentOptions.Top, headCol);
            UIFactory.SetRect(h.rectTransform, 60, 130, W - 120, 80);
            var box = UIFactory.Panel(p.transform, "Box", new Color(1, 1, 1, 0.08f));
            UIFactory.SetRect(box.rectTransform, 90, 250, W - 180, 820);
            body = UIFactory.Label(box.transform, "B", "", 33, TextAlignmentOptions.TopLeft, headCol);
            UIFactory.SetRect(body.rectTransform, 40, 40, W - 260, 740);
            TextMeshProUGUI bl; var b = UIFactory.Button(p.transform, name + "Btn", btn, new Color(0.45f, 0.42f, 0.3f), onClick, out bl);
            UIFactory.SetRect(b.GetComponent<RectTransform>(), (W - 460) / 2, 1180, 460, 150);
            return p.gameObject;
        }

        GameObject BuildBagPanel(Transform root)
        {
            var p = UIFactory.Panel(root, "BagPanel", new Color(0.14f, 0.13f, 0.12f)); UIFactory.Fill(p.rectTransform);
            var h = UIFactory.Label(p.transform, "H", "가방 정비", 44, TextAlignmentOptions.Top, Color.white);
            UIFactory.SetRect(h.rectTransform, 60, 60, W - 120, 70);
            bagBody = UIFactory.Label(p.transform, "Info", "", 25, TextAlignmentOptions.Top, new Color(0.85f, 0.85f, 0.8f));
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
            var tlb = UIFactory.Label(tbg.transform, "l", "획득 대기 (그리드로 끌어 보관)", 22, TextAlignmentOptions.TopLeft, new Color(0.8f, 0.8f, 0.7f));
            tlb.raycastTarget = false; UIFactory.SetRect(tlb.rectTransform, 14, 8, TRAY_W - 28, 32);
            var trayGO = new GameObject("Tray", typeof(RectTransform));
            trayRect = trayGO.GetComponent<RectTransform>(); trayRect.SetParent(p.transform, false);
            UIFactory.SetRect(trayRect, trayX, ty + 44, TRAY_W, 252);

            var tz = UIFactory.Img(p.transform, "Trash", new Color(0.40f, 0.18f, 0.18f));
            trashZone = tz.rectTransform;
            UIFactory.SetRect(trashZone, trashX, ty, TRASH_W, 300);
            var tzl = UIFactory.Label(tz.transform, "l", "여기로 끌어\n버리기", 26, TextAlignmentOptions.Center, new Color(0.95f, 0.7f, 0.7f));
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

            // 배경(인피니티 스크롤) — 씬 영역
            var bgGO = new GameObject("Bg", typeof(RectTransform), typeof(RawImage));
            bgGO.GetComponent<RectTransform>().SetParent(p.transform, false);
            bgRaw = bgGO.GetComponent<RawImage>();
            UIFactory.SetRect(bgGO.GetComponent<RectTransform>(), 0, 110, W, 1010);
            bgRaw.color = new Color(0.55f, 0.6f, 0.66f); bgRaw.raycastTarget = false;

            // 캐릭터(좌측) / 아이템(우측 접근)
            charImg = UIFactory.Img(p.transform, "Char", Color.white);
            UIFactory.SetRect(charImg.rectTransform, 90, 720, 340, 400); charImg.raycastTarget = false;
            itemImg = UIFactory.Img(p.transform, "Item", Color.white);
            UIFactory.SetRect(itemImg.rectTransform, W - 240, 840, 170, 170); itemImg.raycastTarget = false;
            itemImg.gameObject.SetActive(false);

            // 상단바 (DAY · 장소)
            var bar = UIFactory.Img(p.transform, "TopBar", new Color(0.10f, 0.09f, 0.07f, 0.9f)); bar.raycastTarget = false;
            UIFactory.SetRect(bar.rectTransform, 0, 0, W, 110);
            dayText = UIFactory.Label(p.transform, "DayText", "DAY 1", 36, TextAlignmentOptions.Left, new Color(0.96f, 0.9f, 0.78f));
            UIFactory.SetRect(dayText.rectTransform, 44, 18, W - 88, 74); dayText.raycastTarget = false;

            // 좌측 상태 레일 (평소 숨김)
            for (int i = 0; i < 4; i++)
            {
                var ic = UIFactory.Img(p.transform, "Icon_" + i, new Color(1, 1, 1, 0.95f)); ic.raycastTarget = false;
                UIFactory.SetRect(ic.rectTransform, 18, 150 + i * 116, 96, 96);
                statusIcon[i] = ic.gameObject.AddComponent<MY_UIIcon_Script>();
                var l = UIFactory.Label(ic.transform, "L", StatRail[i], 18, TextAlignmentOptions.Bottom, Color.white);
                UIFactory.Fill(l.rectTransform); l.raycastTarget = false;
                statusLabel[i] = l;
                ic.gameObject.SetActive(false);
            }

            // 이벤트 카드 (하단)
            var card = UIFactory.Panel(p.transform, "Card", new Color(0.10f, 0.09f, 0.08f, 0.93f));
            eventCard = card.rectTransform;
            UIFactory.SetRect(eventCard, 50, 1130, W - 100, 560);
            eventTitle = UIFactory.Label(eventCard, "T", "", 32, TextAlignmentOptions.TopLeft, new Color(0.95f, 0.85f, 0.5f));
            UIFactory.SetRect(eventTitle.rectTransform, 36, 22, W - 180, 56);
            eventBody = UIFactory.Label(eventCard, "B", "", 30, TextAlignmentOptions.TopLeft, new Color(0.93f, 0.94f, 0.97f));
            UIFactory.SetRect(eventBody.rectTransform, 36, 84, W - 180, 150);
            var caGO = new GameObject("ChoiceArea", typeof(RectTransform));
            choiceArea = caGO.GetComponent<RectTransform>(); choiceArea.SetParent(eventCard, false);
            UIFactory.SetRect(choiceArea, 36, 244, W - 180, 300);

            // 하단 가방 토글
            TextMeshProUGUI bt; bagToggleBtn = UIFactory.Button(p.transform, "BagToggle", "가방 ▲", new Color(0.5f, 0.4f, 0.25f), OnBagToggle, out bt);
            UIFactory.SetRect(bagToggleBtn.GetComponent<RectTransform>(), (W - 300) / 2f, 1700, 300, 110);

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

