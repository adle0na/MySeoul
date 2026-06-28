using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SeoulLast
{
    // 아이템 정의: 모양(셀 오프셋) + 색 + 게임플레이(내구도/회복)
    public class ItemDef
    {
        public string Id;
        public string Name;
        public Color Color;
        public List<Vector2Int> Cells; // (x=col, y=row), top-left 기준 정규화

        public int MaxUses = 1;        // 내구도(도구) 또는 사용 횟수(회복=1)
        public int RecoverStat = -1;   // -1=도구, 0허기/1수분/2건강/3기운
        public int RecoverAmt = 0;     // 회복량
        public bool Consumable = true; // false면 사용해도 안 닳음(내구도 -1/손상없음)
        public Sprite Icon;            // 아이템 이미지(있으면 색칸 대신 표시)

        public ItemDef(string id, string name, Color color, List<Vector2Int> cells)
        {
            Id = id; Name = name; Color = color; Cells = cells;
        }

        public bool IsRecovery => RecoverStat >= 0;
        public int Width => Cells.Max(c => c.x) + 1;
        public int Height => Cells.Max(c => c.y) + 1;
    }

    public static class ItemDatabase
    {
        public static readonly Dictionary<string, ItemDef> All = new Dictionary<string, ItemDef>();

        static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        // 도구: 내구도 N회
        static void Tool(string id, string name, Color c, int maxUses, params Vector2Int[] cells)
        {
            All[id] = new ItemDef(id, name, c, cells.ToList()) { MaxUses = maxUses };
        }
        // 회복 소모품: stat(0허기/1수분/2건강/3기운) +amt, 1회
        static void Recover(string id, string name, Color c, int stat, int amt, params Vector2Int[] cells)
        {
            All[id] = new ItemDef(id, name, c, cells.ToList()) { MaxUses = 1, RecoverStat = stat, RecoverAmt = amt };
        }

        static ItemDatabase()
        {
            // 도구(내구도)
            Tool("lighter",    "라이터",   new Color(0.92f, 0.62f, 0.22f), 3, V(0, 0));                     // 1x1
            Tool("flashlight", "손전등",   new Color(0.95f, 0.88f, 0.40f), 3, V(0, 0), V(0, 1));            // 1x2
            Tool("rope",       "밧줄",     new Color(0.62f, 0.52f, 0.36f), 2, V(0, 0), V(1, 0));            // 2x1
            Tool("axe",        "도끼",     new Color(0.74f, 0.34f, 0.28f), 4, V(0, 0), V(0, 1), V(1, 1));   // ㄱ자
            Tool("mask",       "방독면",   new Color(0.52f, 0.57f, 0.62f), 2, V(0, 0));                     // 1x1
            Tool("radio",      "무전기",   new Color(0.30f, 0.80f, 0.52f), 5, V(0,0), V(1,0), V(0,1), V(1,1)); // 2x2
            // 회복 소모품
            Recover("food",   "통조림",   new Color(0.85f, 0.76f, 0.32f), 0, 40, V(0, 0));                 // 1x1
            Recover("water",  "생수",     new Color(0.30f, 0.70f, 0.95f), 1, 40, V(0, 0));                 // 1x1
            Recover("medkit", "구급상자", new Color(0.90f, 0.32f, 0.36f), 2, 45, V(0,0), V(1,0), V(0,1), V(1,1)); // 2x2
            Recover("stim",   "각성제",   new Color(0.80f, 0.45f, 0.85f), 3, 40, V(0, 0));                 // 1x1
        }

        public static ItemDef Get(string id)
        {
            return All.TryGetValue(id, out var d) ? d : null;
        }

        public static ItemDef GetByName(string name)
        {
            foreach (var d in All.Values) if (d.Name == name) return d;
            return null;
        }
    }

    // 가방에 배치된 아이템 인스턴스
    public class PlacedItem
    {
        public ItemDef Def;
        public Vector2Int Origin;
        public int Uses;   // 남은 내구도/사용 횟수
        public PlacedItem(ItemDef def) { Def = def; Uses = def.MaxUses; }
        public IEnumerable<Vector2Int> AbsCells() => Def.Cells.Select(c => Origin + c);
    }

    // 4x4 가방 점유 모델
    public class BagModel
    {
        public int Width = 6, Height = 6;
        public List<PlacedItem> Placed = new List<PlacedItem>();

        // FullGrid=true면 전체 칸 사용(고정 그리드). false면 단계별 중앙 활성(구 프로토타입).
        public bool FullGrid = false;

        // 가방 5단계: 1=중앙 2x2, 2=3x3, 3=4x4, 4=5x5, 5=6x6 (나머지는 딤드=배치불가)
        public int Stage = 1;
        public int ActiveSize => Mathf.Clamp(Stage + 1, 2, 6);
        public int ActiveOffset => (6 - ActiveSize) / 2;
        public bool IsActiveCell(Vector2Int c)
        {
            if (FullGrid) return InBounds(c);
            int o = ActiveOffset, s = ActiveSize;
            return c.x >= o && c.x < o + s && c.y >= o && c.y < o + s;
        }

        bool InBounds(Vector2Int c) => c.x >= 0 && c.x < Width && c.y >= 0 && c.y < Height;

        HashSet<Vector2Int> Occupied(PlacedItem ignore)
        {
            var set = new HashSet<Vector2Int>();
            foreach (var p in Placed)
            {
                if (p == ignore) continue;
                foreach (var c in p.AbsCells()) set.Add(c);
            }
            return set;
        }

        public bool CanPlace(ItemDef def, Vector2Int origin, PlacedItem ignore)
        {
            var occ = Occupied(ignore);
            foreach (var cell in def.Cells)
            {
                var c = origin + cell;
                if (!InBounds(c) || !IsActiveCell(c) || occ.Contains(c)) return false;
            }
            return true;
        }

        public void PlaceAt(PlacedItem p, Vector2Int origin)
        {
            p.Origin = origin;
            if (!Placed.Contains(p)) Placed.Add(p);
        }

        // 활성 영역에서 def가 들어갈 첫 빈칸 origin(없으면 null). 드롭 자동 안착용.
        public Vector2Int? FirstFreeActiveOrigin(ItemDef def, PlacedItem ignore)
        {
            int o = ActiveOffset, s = ActiveSize;
            for (int y = o; y < o + s; y++)
                for (int x = o; x < o + s; x++)
                {
                    var origin = new Vector2Int(x, y);
                    if (CanPlace(def, origin, ignore)) return origin;
                }
            return null;
        }

        public void RemoveFromBag(PlacedItem p) => Placed.Remove(p);

        public bool Contains(string itemId) => Placed.Any(p => p.Def.Id == itemId);
    }

    // 지역 이벤트 정의
    public class EventDef
    {
        public string Region;
        public string Intro;
        public string RequiredItemId;
        public string SuccessText;
        public string FailText;
        public string RewardOnSuccess; // null 가능
        public int HpOnSuccess;
        public int HpOnFail;
    }

    public static class EventDatabase
    {
        public static List<EventDef> Events = new List<EventDef>
        {
            new EventDef{ Region="종로구",
                Intro="옛 도심. 어두운 상가 건물 안에서 인기척이 느껴진다. 안을 살펴보려면 빛이 필요하다.",
                RequiredItemId="flashlight",
                SuccessText="손전등으로 내부를 비춰 무사히 보급품을 확보했다. 구석에서 도끼를 발견했다!",
                FailText="어둠 속을 더듬다 유리 파편에 손을 베였다. 빈손으로 물러난다.",
                RewardOnSuccess="axe", HpOnSuccess=0, HpOnFail=-15 },

            new EventDef{ Region="마포구",
                Intro="홍대 거리의 잠긴 마트. 셔터가 단단히 내려져 있다. 부수고 들어가야 한다.",
                RequiredItemId="axe",
                SuccessText="도끼로 셔터를 부수고 들어가 통조림을 잔뜩 챙겼다.",
                FailText="맨손으론 셔터가 꿈쩍도 안 한다. 시간만 허비했다.",
                RewardOnSuccess="food", HpOnSuccess=0, HpOnFail=-10 },

            new EventDef{ Region="영등포구",
                Intro="한강이 범람해 길이 끊겼다. 무너진 난간을 건너려면 몸을 지탱할 도구가 필요하다.",
                RequiredItemId="rope",
                SuccessText="밧줄로 몸을 묶고 무사히 강을 건넜다. 버려진 구급상자를 주웠다.",
                FailText="발을 헛디뎌 물에 빠졌다. 겨우 빠져나왔지만 크게 지쳤다.",
                RewardOnSuccess="medkit", HpOnSuccess=0, HpOnFail=-25 },

            new EventDef{ Region="용산구",
                Intro="용산역 지하 통로는 칠흑같이 어둡다. 불씨가 있어야 길을 찾는다.",
                RequiredItemId="lighter",
                SuccessText="라이터 불빛으로 통로를 빠져나왔다. 방독면을 주웠다.",
                FailText="어둠 속에서 길을 잃고 한참을 헤맸다.",
                RewardOnSuccess="mask", HpOnSuccess=0, HpOnFail=-15 },

            new EventDef{ Region="강남구",
                Intro="번화가 한복판, 가스가 새어나와 공기가 매캐하다. 호흡을 보호하지 않으면 위험하다.",
                RequiredItemId="mask",
                SuccessText="방독면을 쓰고 가스 지대를 통과했다. 작동하는 무전기를 발견했다!",
                FailText="유독가스를 들이마셨다. 정신이 아득해진다.",
                RewardOnSuccess="radio", HpOnSuccess=0, HpOnFail=-40 },

            new EventDef{ Region="송파구",
                Intro="잠실의 탁 트인 벌판. 밤이 오면 추위가 매섭다. 불을 피워야 버틴다.",
                RequiredItemId="lighter",
                SuccessText="라이터로 모닥불을 피워 추운 밤을 견뎠다. 몸이 조금 회복됐다.",
                FailText="불을 피우지 못해 밤새 떨었다. 체온이 크게 떨어졌다.",
                RewardOnSuccess=null, HpOnSuccess=5, HpOnFail=-20 },

            new EventDef{ Region="노원구",
                Intro="북쪽 외곽의 가파른 산길. 도시를 벗어나려면 이 길을 올라야 한다. 다치면 응급처치가 절실하다.",
                RequiredItemId="medkit",
                SuccessText="가파른 길에서 미끄러졌지만 구급상자로 상처를 처치하고 능선을 넘었다.",
                FailText="굴러떨어져 크게 다쳤다. 치료할 것이 없다.",
                RewardOnSuccess=null, HpOnSuccess=0, HpOnFail=-30 },
        };
    }

    public class GameState
    {
        public int Day = 1;
        public int Hp = 100;
        public BagModel Bag = new BagModel();
        public HashSet<string> Visited = new HashSet<string>();
    }
}
