using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeoulLast
{
    // 메인화면 인벤토리 아이템. 창고(StorageRect) ↔ 가방(BagGridRect) 드래그.
    public class InvItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public PlacedItem Model;
        public bool InBag;

        MainScreen host;
        float cell;
        RectTransform rt;
        CanvasGroup cg;
        Vector2Int grabOffset;

        public void Init(MainScreen h, PlacedItem m, float cellSize)
        {
            host = h; Model = m; cell = cellSize;
            rt = GetComponent<RectTransform>();
            cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(m.Def.Width * cell, m.Def.Height * cell);
            BuildVisual();
        }

        void BuildVisual()
        {
            float gap = cell * 0.06f;
            foreach (var c in Model.Def.Cells)
            {
                var img = UIFactory.Img(transform, "cell", Model.Def.Color);
                var crt = img.rectTransform;
                crt.anchorMin = new Vector2(0, 1);
                crt.anchorMax = new Vector2(0, 1);
                crt.pivot = new Vector2(0, 1);
                crt.sizeDelta = new Vector2(cell - gap * 2, cell - gap * 2);
                crt.anchoredPosition = new Vector2(c.x * cell + gap, -(c.y * cell + gap));
            }
            var label = UIFactory.Label(transform, "n", Model.Def.Name, 20, TextAnchor.MiddleCenter, new Color(0.12f, 0.12f, 0.12f));
            label.raycastTarget = false;
            UIFactory.Fill(label.rectTransform);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out local);
            int gx = Mathf.Clamp(Mathf.FloorToInt(local.x / cell), 0, Model.Def.Width - 1);
            int gy = Mathf.Clamp(Mathf.FloorToInt(-local.y / cell), 0, Model.Def.Height - 1);
            grabOffset = new Vector2Int(gx, gy);

            transform.SetParent(host.BagDragLayer, true);
            transform.SetAsLastSibling();
            cg.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData e)
        {
            transform.position += (Vector3)e.delta;
        }

        public void OnEndDrag(PointerEventData e)
        {
            cg.blocksRaycasts = true;

            // 1) 가방 격자 위 → 배치 시도
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(host.BagGridRect, e.position, e.pressEventCamera, out local))
            {
                float gw = host.Bag.Width * cell, gh = host.Bag.Height * cell;
                if (local.x >= 0 && local.x <= gw && -local.y >= 0 && -local.y <= gh)
                {
                    int px = Mathf.FloorToInt(local.x / cell);
                    int py = Mathf.FloorToInt(-local.y / cell);
                    var origin = new Vector2Int(px - grabOffset.x, py - grabOffset.y);
                    if (host.Bag.CanPlace(Model.Def, origin, Model))
                    {
                        host.PlaceInBag(this, origin);
                        return;
                    }
                }
            }

            // 2) 창고 영역 위 (사물함 열려 있을 때만) → 보관
            if (host.LockerOpen && host.StorageRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(host.StorageRect, e.position, e.pressEventCamera))
            {
                host.MoveToStorage(this);
                return;
            }

            // 3) 그 외 → 원위치
            if (InBag) AttachToBag(Model.Origin);
            else host.ReturnToStorage(this);
        }

        public void AttachToBag(Vector2Int origin)
        {
            transform.SetParent(host.BagGridRect, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(origin.x * cell, -origin.y * cell);
        }
    }
}
