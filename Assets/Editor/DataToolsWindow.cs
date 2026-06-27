using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SeoulLast.Data;

namespace SeoulLast.EditorTools
{
    // 메뉴: NoPainYesGame > Data Tools
    // ① 구글시트(게시 CSV URL) -> 로컬 CSV 다운로드
    // ② 로컬 Item.csv -> ItemData ScriptableObject 에셋 생성/갱신
    public class DataToolsWindow : EditorWindow
    {
        string itemCsvUrl;
        string eventCsvUrl;
        string dialogCsvUrl;
        string locationCsvUrl;
        string csvFolder = "Assets/Data/CSV";
        string itemAssetFolder = "Assets/Data/Items";
        string eventAssetFolder = "Assets/Data/Events";
        string dialogAssetFolder = "Assets/Data/Dialogs";
        string locationAssetFolder = "Assets/Data/Locations";

        const string KeyItemUrl = "NPYG_ItemCsvUrl";
        const string KeyEventUrl = "NPYG_EventCsvUrl";
        const string KeyDialogUrl = "NPYG_DialogCsvUrl";
        const string KeyLocationUrl = "NPYG_LocationCsvUrl";

        // static 임포트(윈도우 인스턴스 없이 호출 가능)용 기본 폴더
        const string CsvFolder = "Assets/Data/CSV";
        const string EventFolder = "Assets/Data/Events";
        const string DialogFolder = "Assets/Data/Dialogs";
        const string LocationFolder = "Assets/Data/Locations";

        [MenuItem("NoPainYesGame/Data Tools")]
        static void Open() => GetWindow<DataToolsWindow>("Data Tools");

        void OnEnable()
        {
            itemCsvUrl = EditorPrefs.GetString(KeyItemUrl, "");
            eventCsvUrl = EditorPrefs.GetString(KeyEventUrl, "");
            dialogCsvUrl = EditorPrefs.GetString(KeyDialogUrl, "");
            locationCsvUrl = EditorPrefs.GetString(KeyLocationUrl, "");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("구글시트 CSV URL", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1) 시트 공유를 '링크가 있는 모든 사용자 - 뷰어'로 설정.\n" +
                "2) 각 탭(Item/Event)을 연 상태의 일반 주소(.../edit#gid=123)를 그대로 붙여넣으면\n" +
                "   자동으로 CSV 주소로 변환됩니다. (CSV 게시 주소나 export 주소도 그대로 사용 가능)\n" +
                "※ /pubhtml(HTML) 주소만 단독으로 쓰면 안 됩니다 — 탭별 gid가 필요해요.",
                MessageType.Info);

            itemCsvUrl = EditorGUILayout.TextField("Item 시트 URL", itemCsvUrl);
            eventCsvUrl = EditorGUILayout.TextField("Event 시트 URL", eventCsvUrl);
            dialogCsvUrl = EditorGUILayout.TextField("EventDialog 시트 URL", dialogCsvUrl);
            locationCsvUrl = EditorGUILayout.TextField("Location 시트 URL", locationCsvUrl);
            csvFolder = EditorGUILayout.TextField("CSV 저장 폴더", csvFolder);
            itemAssetFolder = EditorGUILayout.TextField("ItemData 에셋 폴더", itemAssetFolder);
            eventAssetFolder = EditorGUILayout.TextField("EventData 에셋 폴더", eventAssetFolder);
            dialogAssetFolder = EditorGUILayout.TextField("DialogData 에셋 폴더", dialogAssetFolder);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("①  구글시트 → 로컬 CSV 최신화", GUILayout.Height(34)))
            {
                EditorPrefs.SetString(KeyItemUrl, itemCsvUrl);
                EditorPrefs.SetString(KeyEventUrl, eventCsvUrl);
                EditorPrefs.SetString(KeyDialogUrl, dialogCsvUrl);
                EditorPrefs.SetString(KeyLocationUrl, locationCsvUrl);
                DownloadCsvs();
            }
            EditorGUILayout.Space(4);
            if (GUILayout.Button("②  Item.csv → ItemData 에셋 생성/갱신", GUILayout.Height(34)))
                ImportItems();
            EditorGUILayout.Space(4);
            if (GUILayout.Button("③  Event.csv → EventData 에셋 생성/갱신", GUILayout.Height(34)))
                ImportEvents();
            EditorGUILayout.Space(4);
            if (GUILayout.Button("④  EventDialog.csv → DialogData 에셋 생성/갱신", GUILayout.Height(34)))
                ImportDialogs();
            EditorGUILayout.Space(4);
            if (GUILayout.Button("⑤  Location.csv → LocationData 에셋 생성/갱신", GUILayout.Height(34)))
                ImportLocations();
        }

        void DownloadCsvs()
        {
            Directory.CreateDirectory(csvFolder);
            int ok = 0;
            if (TryDownload(itemCsvUrl, Path.Combine(csvFolder, "Item.csv"))) ok++;
            if (TryDownload(eventCsvUrl, Path.Combine(csvFolder, "Event.csv"))) ok++;
            if (TryDownload(dialogCsvUrl, Path.Combine(csvFolder, "EventDialog.csv"))) ok++;
            if (TryDownload(locationCsvUrl, Path.Combine(csvFolder, "Location.csv"))) ok++;
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("CSV 최신화", $"{ok}개 파일 다운로드 완료.\n폴더: {csvFolder}", "확인");
        }

        bool TryDownload(string url, string path)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string csvUrl = NormalizeSheetCsvUrl(url);
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                string text;
                using (var wc = new System.Net.WebClient())
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    text = wc.DownloadString(csvUrl);
                }

                // CSV가 아니라 HTML(로그인/미리보기 페이지)이 오면 실패 처리
                if (text.TrimStart().StartsWith("<"))
                {
                    Debug.LogError($"[DataTools] CSV가 아니라 HTML이 반환됨: {csvUrl}\n" +
                        "→ 시트 공유를 '링크가 있는 모든 사용자(뷰어)'로 바꾸거나, CSV 형식으로 게시한 URL을 쓰세요.");
                    return false;
                }

                File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
                Debug.Log($"[DataTools] 다운로드 완료: {path}\n   (요청 URL: {csvUrl})");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataTools] 다운로드 실패: {csvUrl}\n{e.Message}\n" +
                    "→ 시트 공유가 '링크가 있는 모든 사용자(뷰어)'인지, URL이 맞는지 확인하세요.");
                return false;
            }
        }

        // 붙여넣은 구글시트 URL을 CSV 다운로드 URL로 변환.
        //  - .../edit#gid=123        -> .../export?format=csv&gid=123
        //  - .../d/e/2PACX.../pubhtml -> .../d/e/2PACX.../pub?gid=..&single=true&output=csv
        //  - 이미 csv면 그대로
        static string NormalizeSheetCsvUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            url = url.Trim();
            if (url.Contains("output=csv") || url.Contains("format=csv")) return url;

            string gid = "";
            var gm = System.Text.RegularExpressions.Regex.Match(url, @"[?#&]gid=([0-9]+)");
            if (gm.Success) gid = gm.Groups[1].Value;

            // 게시(웹에 게시) URL: /d/e/2PACX.../pubhtml 또는 /pub
            int pubIdx = url.IndexOf("/pub", System.StringComparison.Ordinal);
            if (url.Contains("/d/e/") && pubIdx >= 0)
            {
                string baseUrl = url.Substring(0, pubIdx);
                return string.IsNullOrEmpty(gid)
                    ? baseUrl + "/pub?output=csv"
                    : baseUrl + "/pub?gid=" + gid + "&single=true&output=csv";
            }

            // 일반 편집 URL: /spreadsheets/d/{ID}/edit...
            var m = System.Text.RegularExpressions.Regex.Match(url, @"/spreadsheets/d/([a-zA-Z0-9-_]+)");
            if (m.Success && m.Groups[1].Value != "e")
            {
                string id = m.Groups[1].Value;
                string u = "https://docs.google.com/spreadsheets/d/" + id + "/export?format=csv";
                if (!string.IsNullOrEmpty(gid)) u += "&gid=" + gid;
                return u;
            }

            return url; // 변환 못 하면 원본 그대로 시도
        }

        void ImportItems()
        {
            string csvPath = Path.Combine(csvFolder, "Item.csv");
            if (!File.Exists(csvPath))
            {
                EditorUtility.DisplayDialog("오류", $"{csvPath} 없음.\n먼저 ① 최신화를 실행하세요.", "확인");
                return;
            }
            Directory.CreateDirectory(itemAssetFolder);

            var rows = CsvParser.Parse(File.ReadAllText(csvPath));
            int headerRow = FindHeaderRow(rows, "ItemID");
            if (headerRow < 0) { EditorUtility.DisplayDialog("오류", "Item.csv에서 'ItemID' 헤더를 찾지 못했습니다.", "확인"); return; }
            var col = MapColumns(rows[headerRow]);

            int created = 0, updated = 0;
            for (int r = headerRow + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string id = Get(row, col, "ItemID").Trim();
                if (IsHeaderToken(id)) continue; // 타입/한글 헤더행 스킵

                string assetPath = $"{itemAssetFolder}/{id}.asset";
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                bool isNew = item == null;
                if (isNew) item = ScriptableObject.CreateInstance<ItemData>();

                // 기획 Item 테이블 컬럼 1:1 매핑
                item.itemId = id;
                item.itemName = Get(row, col, "ItemName");
                item.itemType = Get(row, col, "ItemType");
                item.durability = ParseInt(Get(row, col, "ItemDurability"), -1);
                item.description = Get(row, col, "ItemDescription");
                item.sellGold = ParseInt(Get(row, col, "ItemSellGoldPrice"));
                item.buyGold = ParseInt(Get(row, col, "ItemBuyGoldPrice"));
                item.cashPrice = ParseInt(Get(row, col, "ItemCashPrice"), -1);
                item.canBeDamaged = ParseBool(Get(row, col, "ItemCanBeDamaged"));
                item.specialEvent = Get(row, col, "ItemSpecialEvent");
                item.resourcePath = Get(row, col, "ItemResourcePath");
                item.resourceName = Get(row, col, "ItemResourceName");

                // 리소스 경로 → 스프라이트 로드 (절대경로/따옴표/역슬래시 정규화)
                var loaded = LoadIconSprite(item.resourcePath, item.resourceName);
                if (loaded != null) item.icon = loaded;

                // 시트에 모양 컬럼이 없으므로, 인스펙터에서 칠한 모양은 그대로 보존
                item.EnsureSize();

                if (isNew) { AssetDatabase.CreateAsset(item, assetPath); created++; }
                else { EditorUtility.SetDirty(item); updated++; }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("임포트 완료", $"생성 {created}개 / 갱신 {updated}개\n폴더: {itemAssetFolder}", "확인");
        }

        void ImportEvents()
        {
            string csvPath = Path.Combine(csvFolder, "Event.csv");
            if (!File.Exists(csvPath))
            {
                EditorUtility.DisplayDialog("오류", $"{csvPath} 없음.\n먼저 ① 최신화를 실행하세요.", "확인");
                return;
            }
            var res = RunImportEvents();
            if (res == null) { EditorUtility.DisplayDialog("오류", "Event.csv에서 'EventId' 헤더를 찾지 못했습니다.", "확인"); return; }
            EditorUtility.DisplayDialog("임포트 완료", $"이벤트 생성 {res[0]}개 / 갱신 {res[1]}개\n폴더: {eventAssetFolder}", "확인");
        }

        // 다이얼로그 없는 핵심 로직 (테스트/자동화용, static — 윈도우 불필요). 반환: {생성, 갱신}, 헤더 못 찾으면 null.
        public static int[] RunImportEvents()
        {
            string csvPath = Path.Combine(CsvFolder, "Event.csv");
            if (!File.Exists(csvPath)) return null;
            Directory.CreateDirectory(EventFolder);

            var rows = CsvParser.Parse(File.ReadAllText(csvPath));
            int headerRow = FindHeaderRow(rows, "EventId");
            if (headerRow < 0) return null;
            var col = MapColumns(rows[headerRow]);

            int created = 0, updated = 0;
            for (int r = headerRow + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string id = Get(row, col, "EventId").Trim();
                if (!id.StartsWith("EVT")) continue; // 데이터 행만 (타입/한글 헤더 스킵)

                string assetPath = $"{EventFolder}/{id}.asset";
                var ev = AssetDatabase.LoadAssetAtPath<EventData>(assetPath);
                bool isNew = ev == null;
                if (isNew) ev = ScriptableObject.CreateInstance<EventData>();

                // ----- 신 구조: 이벤트는 메타 + 시작 대화 포인터만 -----
                ev.eventId = id;
                ev.eventName = id;                                  // 시트에 이름 컬럼 없음 → Id로
                ev.eventType = Get(row, col, "EventType");
                ev.minLevel = ParseInt(Get(row, col, "EventMinLv"));
                ev.region = Get(row, col, "EventRegion");
                ev.startDialogId = Get(row, col, "EventDialogId").Trim();
                // 메모 (EventDialogId 다음 칸, 헤더 비어 있음)
                int dIdx;
                if (col.TryGetValue("EventDialogId", out dIdx)) ev.note = At(row, dIdx + 1);

                if (isNew) { AssetDatabase.CreateAsset(ev, assetPath); created++; }
                else { EditorUtility.SetDirty(ev); updated++; }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return new[] { created, updated };
        }

        // ---------- EventDialog.csv → DialogData ----------
        void ImportDialogs()
        {
            string csvPath = Path.Combine(csvFolder, "EventDialog.csv");
            if (!File.Exists(csvPath))
            {
                EditorUtility.DisplayDialog("오류", $"{csvPath} 없음.\n먼저 ① 최신화를 실행하세요.", "확인");
                return;
            }
            var res = RunImportDialogs();
            if (res == null) { EditorUtility.DisplayDialog("오류", "EventDialog.csv에서 'DialogId' 헤더를 찾지 못했습니다.", "확인"); return; }
            EditorUtility.DisplayDialog("임포트 완료", $"대화 생성 {res[0]}개 / 갱신 {res[1]}개\n폴더: {dialogAssetFolder}", "확인");
        }

        public static int[] RunImportDialogs()
        {
            string csvPath = Path.Combine(CsvFolder, "EventDialog.csv");
            if (!File.Exists(csvPath)) return null;
            Directory.CreateDirectory(DialogFolder);

            var rows = CsvParser.Parse(File.ReadAllText(csvPath));
            int headerRow = FindHeaderRow(rows, "DialogId");
            if (headerRow < 0) return null;
            var col = MapColumns(rows[headerRow]);

            int created = 0, updated = 0;
            for (int r = headerRow + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string id = Get(row, col, "DialogId").Trim();
                if (string.IsNullOrEmpty(id) || id == "string" || id.StartsWith("대화")) continue;

                string assetPath = $"{DialogFolder}/{id}.asset";
                var d = AssetDatabase.LoadAssetAtPath<DialogData>(assetPath);
                bool isNew = d == null;
                if (isNew) d = ScriptableObject.CreateInstance<DialogData>();

                d.dialogId = id;
                d.spawnItemId = Get(row, col, "DialogItemId").Trim();
                d.description = Get(row, col, "Description");

                var list = new List<EventChoice>();
                AddDialogBranch(list, Get(row, col, "BranchA_ItemID"), GetAny(row, col, "BranchA_name", "BranchA_Name"),
                    Get(row, col, "BranchA_OpensInventory"), Get(row, col, "BranchA_NewState"), Get(row, col, "BranchA_NextEventId"));
                AddDialogBranch(list, Get(row, col, "BranchB_ItemID"), GetAny(row, col, "BranchB_Name", "BranchB_name"),
                    Get(row, col, "BranchB_OpensInventory"), Get(row, col, "BranchB_NewState"), Get(row, col, "BranchB_NextEventId"));
                AddDialogBranch(list, Get(row, col, "BranchC_ItemID"), GetAny(row, col, "BranchC_Name", "BranchC_name"),
                    Get(row, col, "BranchC_OpensInventory"), Get(row, col, "BranchC_NewState"), Get(row, col, "BranchC_NextEventId"));
                d.choices = list.ToArray();

                if (isNew) { AssetDatabase.CreateAsset(d, assetPath); created++; }
                else { EditorUtility.SetDirty(d); updated++; }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return new[] { created, updated };
        }

        // ---------- Location.csv → LocationData ----------
        void ImportLocations()
        {
            string csvPath = Path.Combine(csvFolder, "Location.csv");
            if (!File.Exists(csvPath))
            {
                EditorUtility.DisplayDialog("오류", $"{csvPath} 없음.\n먼저 ① 최신화를 실행하세요.", "확인");
                return;
            }
            var res = RunImportLocations();
            if (res == null) { EditorUtility.DisplayDialog("오류", "Location.csv에서 'LocationID' 헤더를 찾지 못했습니다.", "확인"); return; }
            EditorUtility.DisplayDialog("임포트 완료", $"장소 생성 {res[0]}개 / 갱신 {res[1]}개\n폴더: {locationAssetFolder}", "확인");
        }

        public static int[] RunImportLocations()
        {
            string csvPath = Path.Combine(CsvFolder, "Location.csv");
            if (!File.Exists(csvPath)) return null;
            Directory.CreateDirectory(LocationFolder);

            var rows = CsvParser.Parse(File.ReadAllText(csvPath));
            int headerRow = FindHeaderRow(rows, "LocationID");
            if (headerRow < 0) return null;
            var col = MapColumns(rows[headerRow]);

            int created = 0, updated = 0;
            for (int r = headerRow + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string id = Get(row, col, "LocationID").Trim();
                if (!id.StartsWith("Loc")) continue;

                string assetPath = $"{LocationFolder}/{id}.asset";
                var loc = AssetDatabase.LoadAssetAtPath<LocationData>(assetPath);
                bool isNew = loc == null;
                if (isNew) loc = ScriptableObject.CreateInstance<LocationData>();

                loc.locationId = id;
                loc.locationName = Get(row, col, "LocationName");
                loc.floor = ParseInt(Get(row, col, "LocationFloor"));
                loc.isLock = ParseBool(Get(row, col, "LocationIsLock"));
                loc.visitCount = ParseInt(Get(row, col, "LocationVisitCount"));
                loc.description = Get(row, col, "LocationDescription");

                if (isNew) { AssetDatabase.CreateAsset(loc, assetPath); created++; }
                else { EditorUtility.SetDirty(loc); updated++; }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return new[] { created, updated };
        }

        // 대화 분기 추가. 이름이 "Empty"면 버튼 없이 자동 진행(라벨 빈값). 이름·다음 둘 다 없으면 스킵.
        static void AddDialogBranch(List<EventChoice> list, string itemId, string name, string opensInv, string newState, string nextId)
        {
            string nm = (name ?? "").Trim();
            string next = (nextId ?? "").Trim();
            if (nm.Length == 0 && next.Length == 0) return;
            bool auto = nm.Equals("Empty", System.StringComparison.OrdinalIgnoreCase);
            list.Add(new EventChoice
            {
                label = auto ? "" : nm,
                requiredItem = (itemId ?? "").Trim(),
                opensInventory = ParseBool(opensInv),
                newState = (newState ?? "").Trim(),
                nextEventId = next                 // 다음 대화 id ("Done" = 이벤트 종료)
            });
        }

        static string GetAny(List<string> row, Dictionary<string, int> col, params string[] names)
        {
            foreach (var n in names)
            {
                int i;
                if (col.TryGetValue(n, out i) && i < row.Count) return row[i];
            }
            return "";
        }

        // 'key' 컬럼명을 포함한 첫 행을 헤더로 간주 (Item=0행, Event=1행에 헤더가 있어 유연 탐지)
        static int FindHeaderRow(List<List<string>> rows, string key)
        {
            for (int r = 0; r < rows.Count && r < 6; r++)
                foreach (var cell in rows[r])
                    if (cell.Trim() == key) return r;
            return -1;
        }

        static Dictionary<string, int> MapColumns(List<string> header)
        {
            var col = new Dictionary<string, int>();
            for (int i = 0; i < header.Count; i++)
            {
                string key = header[i].Trim();
                if (key.Length > 0 && !col.ContainsKey(key)) col[key] = i;
            }
            return col;
        }

        // 데이터가 아닌 헤더/타입/한글설명 행을 거르기
        static bool IsHeaderToken(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            switch (id)
            {
                case "ItemID": case "ID": case "EventId":
                case "string": case "int": case "enum": case "bool":
                    return true;
            }
            return false;
        }

        static string Get(List<string> row, Dictionary<string, int> col, string name)
            => col.TryGetValue(name, out int i) && i < row.Count ? row[i] : "";

        static string At(List<string> row, int idx)
            => idx >= 0 && idx < row.Count ? row[idx] : "";

        static int ParseInt(string s, int def = 0)
            => int.TryParse((s ?? "").Trim(), out int v) ? v : def;

        static bool ParseBool(string s)
        {
            s = (s ?? "").Trim().ToUpper();
            return s == "TRUE" || s == "1" || s == "O" || s == "Y" || s == "YES";
        }

        // 리소스 경로(절대/상대/따옴표/역슬래시) → Assets 상대경로의 Sprite 로드
        static Sprite LoadIconSprite(string path, string fileName)
        {
            string p = (path ?? "").Trim().Trim('"').Replace('\\', '/');
            if (string.IsNullOrEmpty(p)) return null;
            int idx = p.IndexOf("Assets/", System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) p = p.Substring(idx);
            if (!System.IO.Path.HasExtension(p) && !string.IsNullOrEmpty(fileName))
                p = p.TrimEnd('/') + "/" + fileName.Trim().Trim('"');
            if (!p.StartsWith("Assets/")) return null;
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; imp.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Sprite>(p);
        }
    }
}
