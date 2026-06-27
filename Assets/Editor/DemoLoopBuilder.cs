using UnityEditor;
using UnityEngine;
using SeoulLast.Data;

namespace SeoulLast.EditorTools
{
    // 시트와 무관한 DEMO 콘텐츠 (DEMO- id) — 코어 한 바퀴를 내용 있게 돌리기 위한 데모.
    // 데모 전용 아이템 + 일반/특정지역/옥상(엔딩) 이벤트 + 대화(지급·게이트·회복·상태변화·Win).
    // 기획 시트 임포트(②/③/④)는 DEMO- id를 건드리지 않으므로 덮어써지지 않음.
    public static class DemoLoopBuilder
    {
        const string EvDir = "Assets/Data/Events";
        const string DlgDir = "Assets/Data/Dialogs";
        const string ItemDir = "Assets/Data/Items";

        [MenuItem("NoPainYesGame/Build Demo Loop Content")]
        public static void Build()
        {
            var r = BuildCore();
            EditorUtility.DisplayDialog("완료", $"데모 아이템 {r[2]} / 이벤트 {r[0]} / 대화 {r[1]} 생성·갱신", "확인");
        }

        public static int[] BuildCore()
        {
            System.IO.Directory.CreateDirectory(EvDir);
            System.IO.Directory.CreateDirectory(DlgDir);
            System.IO.Directory.CreateDirectory(ItemDir);
            _ev = 0; _dlg = 0; _item = 0;

            // ---- 데모 전용 아이템 (이름 기반 회복 매핑: 빵→허기, 음료→수분, 구급→건강) ----
            Item("DEMO-BREAD", "빵", "자원", -1, 1, 1);
            Item("DEMO-DRINK", "캔음료", "자원", -1, 1, 1);
            Item("DEMO-MED", "구급상자", "회복", -1, 1, 2);

            // ---- 일반: 빈 교실 (빵 지급 → 허기 회복) ----
            Ev("DEMO-G1", "일반", "", "DEMO-G1-01");
            Dlg("DEMO-G1-01", "DEMO-BREAD", "빈 교실이다. 책상 위에 빵이 덩그러니 놓여 있다.",
                B("빵을 가방에 챙긴다", "", true, "", "DEMO-G1-02"),
                B("그냥 둔다", "", false, "", "Done"));
            Dlg("DEMO-G1-02", "", "빵을 챙겼다. 허기지면 가방에서 빵을 사용하자.",
                B("", "", false, "", "Done"));

            // ---- 일반: 잠긴 캐비닛 (빠루 게이트 → 드라이버 지급 / 무모하면 부상) ----
            Ev("DEMO-G2", "일반", "", "DEMO-G2-01");
            Dlg("DEMO-G2-01", "TOO002", "잠긴 철제 캐비닛. 안에서 달그락거리는 소리가 난다.",
                B("빠루로 비틀어 연다", "빠루", true, "", "DEMO-G2-02"),
                B("발로 차본다", "", false, "부상", "DEMO-G2-03"),
                B("포기한다", "", false, "피곤함", "Done"));
            Dlg("DEMO-G2-02", "", "철컹! 캐비닛이 열리고 드라이버를 손에 넣었다.",
                B("", "", false, "", "Done"));
            Dlg("DEMO-G2-03", "", "꿈쩍도 안 한다. 발만 아프다.",
                B("", "", false, "", "Done"));

            // ---- 일반: 복도 자판기 (음료 지급 → 수분 회복) ----
            Ev("DEMO-G3", "일반", "", "DEMO-G3-01");
            Dlg("DEMO-G3-01", "DEMO-DRINK", "복도 끝 자판기에 아직 불이 켜져 있다. 캔음료가 보인다.",
                B("드라이버로 분해한다", "드라이버", true, "", "DEMO-G3-02"),
                B("발로 차서 떨어뜨린다", "", true, "피곤함", "DEMO-G3-02"),
                B("지나친다", "", false, "", "Done"));
            Dlg("DEMO-G3-02", "", "캔음료를 손에 넣었다. 수분 보충에 좋겠다.",
                B("", "", false, "", "Done"));

            // ---- 특정지역(음악실): 피아노 ----
            Ev("DEMO-L1", "특정지역", "음악실", "DEMO-L1-01");
            Dlg("DEMO-L1-01", "", "음악실. 먼지 쌓인 피아노에서 가끔 낮은 소리가 울린다.",
                B("건반을 눌러본다", "", false, "불안", "DEMO-L1-02"),
                B("드라이버로 안을 뜯어본다", "드라이버", false, "피곤함", "DEMO-L1-03"),
                B("조용히 나간다", "", false, "", "Done"));
            Dlg("DEMO-L1-02", "", "낡은 화음이 복도에 울려 퍼진다. 등골이 서늘하다.",
                B("", "", false, "", "Done"));
            Dlg("DEMO-L1-03", "", "피아노 안은 텅 비어 있었다. 헛수고였다.",
                B("", "", false, "", "Done"));

            // ---- 특정지역(과학실): 약품 선반 (구급상자 지급 → 건강 회복) ----
            Ev("DEMO-L2", "특정지역", "과학실", "DEMO-L2-01");
            Dlg("DEMO-L2-01", "DEMO-MED", "과학실. 선반 한쪽에 구급상자가 놓여 있고, 유리병들이 늘어서 있다.",
                B("구급상자를 챙긴다", "", true, "", "DEMO-L2-03"),
                B("약품을 이것저것 살펴본다", "", false, "부상", "DEMO-L2-02"),
                B("조심히 지나간다", "", false, "피곤함", "Done"));
            Dlg("DEMO-L2-02", "", "병이 깨지며 약품이 튀었다! 손등이 따끔거린다.",
                B("", "", false, "", "Done"));
            Dlg("DEMO-L2-03", "", "구급상자를 챙겼다. 다치면 가방에서 사용하자.",
                B("", "", false, "", "Done"));

            // ---- 특정지역(옥상): 탈출 엔딩 ----
            Ev("DEMO-WIN", "특정지역", "옥상", "DEMO-WIN-01");
            Dlg("DEMO-WIN-01", "", "옥상에 올라왔다. 멀리서 헬기 소리가 점점 커진다!",
                B("있는 힘껏 신호를 보낸다", "", false, "", "Win"),
                B("들킬까 무서워 숨는다", "", false, "불안", "Done"));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return new[] { _ev, _dlg, _item };
        }

        static int _ev, _dlg, _item;

        static void Ev(string id, string type, string region, string startDialog)
        {
            string path = $"{EvDir}/{id}.asset";
            var ev = AssetDatabase.LoadAssetAtPath<EventData>(path);
            bool isNew = ev == null;
            if (isNew) ev = ScriptableObject.CreateInstance<EventData>();
            ev.eventId = id; ev.eventName = id; ev.eventType = type;
            ev.region = region; ev.startDialogId = startDialog; ev.minLevel = 0;
            if (isNew) AssetDatabase.CreateAsset(ev, path); else EditorUtility.SetDirty(ev);
            _ev++;
        }

        static void Dlg(string id, string spawn, string desc, params EventChoice[] branches)
        {
            string path = $"{DlgDir}/{id}.asset";
            var d = AssetDatabase.LoadAssetAtPath<DialogData>(path);
            bool isNew = d == null;
            if (isNew) d = ScriptableObject.CreateInstance<DialogData>();
            d.dialogId = id; d.spawnItemId = spawn; d.description = desc; d.choices = branches;
            if (isNew) AssetDatabase.CreateAsset(d, path); else EditorUtility.SetDirty(d);
            _dlg++;
        }

        static void Item(string id, string name, string type, int dur, int w, int h)
        {
            string path = $"{ItemDir}/{id}.asset";
            var it = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            bool isNew = it == null;
            if (isNew) it = ScriptableObject.CreateInstance<ItemData>();
            it.itemId = id; it.itemName = name; it.itemType = type; it.durability = dur;
            it.EnsureSize();
            for (int i = 0; i < it.shape.Length; i++) it.shape[i] = false;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    it.shape[y * ItemData.GridW + x] = true;
            if (isNew) AssetDatabase.CreateAsset(it, path); else EditorUtility.SetDirty(it);
            _item++;
        }

        static EventChoice B(string label, string req, bool opensInv, string newState, string next)
            => new EventChoice { label = label, requiredItem = req, opensInventory = opensInv, newState = newState, nextEventId = next };
    }
}
