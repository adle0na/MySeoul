using System.Collections.Generic;
using UnityEngine;

namespace SeoulLast.Data
{
    // 기획 데이터 아이템 1개 = ScriptableObject 1개. 모양은 7x5 그리드로 인스펙터에서 편집.
    [CreateAssetMenu(fileName = "Item_", menuName = "NoPainYesGame/Item Data")]
    public class ItemData : ScriptableObject
    {
        public const int GridW = 7;
        public const int GridH = 5;

        // 기획 Item 테이블 컬럼과 1:1 매핑 (모양만 인스펙터 전용)
        [Header("기본")]
        public string itemId;            // ItemID
        public string itemName;          // ItemName
        public string itemType;          // ItemType (무기/방어구/회복/자원/특수)
        public int durability = -1;      // ItemDurability (-1 = 손상 없음)
        [TextArea(2, 4)] public string description; // ItemDescription

        [Header("경제")]
        public int sellGold;             // ItemSellGoldPrice
        public int buyGold;              // ItemBuyGoldPrice
        public int cashPrice = -1;       // ItemCashPrice

        [Header("기타")]
        public bool canBeDamaged;        // ItemCanBeDamaged
        public string specialEvent;      // ItemSpecialEvent (연동 이벤트)
        public string resourcePath;      // ItemResourcePath
        public string resourceName;      // ItemResourceName

        [Header("모양 (7x5 — 인스펙터 그리드에서 편집, 시트에 컬럼 없음)")]
        public bool[] shape = new bool[GridW * GridH];

        public bool GetCell(int x, int y) => shape[y * GridW + x];
        public void SetCell(int x, int y, bool v) => shape[y * GridW + x] = v;

        // 점유 셀을 좌상단 기준으로 정규화 (BagModel 연동용)
        public List<Vector2Int> GetOccupiedCells()
        {
            var cells = new List<Vector2Int>();
            int minX = int.MaxValue, minY = int.MaxValue;
            for (int y = 0; y < GridH; y++)
                for (int x = 0; x < GridW; x++)
                    if (shape[y * GridW + x])
                    {
                        cells.Add(new Vector2Int(x, y));
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                    }
            for (int i = 0; i < cells.Count; i++)
                cells[i] -= new Vector2Int(minX, minY);
            return cells;
        }

        // "11/01/01" 마스크 -> shape (좌상단 정렬)
        public void SetShapeFromMask(string mask)
        {
            EnsureSize();
            for (int i = 0; i < shape.Length; i++) shape[i] = false;
            if (string.IsNullOrEmpty(mask) || mask == "-") return;
            var rows = mask.Split('/');
            for (int y = 0; y < rows.Length && y < GridH; y++)
            {
                var row = rows[y];
                for (int x = 0; x < row.Length && x < GridW; x++)
                    if (row[x] == '1') shape[y * GridW + x] = true;
            }
        }

        // shape -> bounding box 마스크 문자열
        public string GetShapeMask()
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            bool any = false;
            for (int y = 0; y < GridH; y++)
                for (int x = 0; x < GridW; x++)
                    if (shape[y * GridW + x])
                    {
                        any = true;
                        if (x < minX) minX = x; if (y < minY) minY = y;
                        if (x > maxX) maxX = x; if (y > maxY) maxY = y;
                    }
            if (!any) return "-";
            var sb = new System.Text.StringBuilder();
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                    sb.Append(shape[y * GridW + x] ? '1' : '0');
                if (y < maxY) sb.Append('/');
            }
            return sb.ToString();
        }

        public void EnsureSize()
        {
            int need = GridW * GridH;
            if (shape == null || shape.Length != need)
            {
                var old = shape;
                shape = new bool[need];
                if (old != null)
                    for (int i = 0; i < Mathf.Min(old.Length, need); i++) shape[i] = old[i];
            }
        }

        // 칠한 칸을 좌상단(0,0)으로 밀어 저장. 빈칸은 자동으로 버려짐.
        public void Normalize()
        {
            EnsureSize();
            int minX = int.MaxValue, minY = int.MaxValue;
            bool any = false;
            for (int y = 0; y < GridH; y++)
                for (int x = 0; x < GridW; x++)
                    if (shape[y * GridW + x]) { any = true; if (x < minX) minX = x; if (y < minY) minY = y; }
            if (!any || (minX == 0 && minY == 0)) return;

            var src = (bool[])shape.Clone();
            for (int i = 0; i < shape.Length; i++) shape[i] = false;
            for (int y = 0; y < GridH; y++)
                for (int x = 0; x < GridW; x++)
                    if (src[y * GridW + x]) shape[(y - minY) * GridW + (x - minX)] = true;
        }

        public int OccupiedCount()
        {
            int c = 0;
            for (int i = 0; i < shape.Length; i++) if (shape[i]) c++;
            return c;
        }

        // 칠한 칸들이 상하좌우로 한 덩어리인지 (아이템은 쪼개질 수 없음)
        public bool IsConnected()
        {
            int total = OccupiedCount();
            if (total <= 1) return true;

            int startX = -1, startY = -1;
            for (int y = 0; y < GridH && startX < 0; y++)
                for (int x = 0; x < GridW; x++)
                    if (shape[y * GridW + x]) { startX = x; startY = y; break; }

            var visited = new bool[GridW * GridH];
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(startX, startY));
            visited[startY * GridW + startX] = true;
            int seen = 1;
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                for (int d = 0; d < 4; d++)
                {
                    int nx = p.x + dx[d], ny = p.y + dy[d];
                    if (nx < 0 || nx >= GridW || ny < 0 || ny >= GridH) continue;
                    int idx = ny * GridW + nx;
                    if (visited[idx] || !shape[idx]) continue;
                    visited[idx] = true;
                    seen++;
                    stack.Push(new Vector2Int(nx, ny));
                }
            }
            return seen == total;
        }
    }
}
