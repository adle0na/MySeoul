using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeoulLast
{
    // 가방/트레이의 한 아이템. 드래그로 이동, 격자 스냅, 충돌 시 원위치.
    public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public PlacedItem Model;
        public bool InBag;

        GameManager gm;
        float cell;
        RectTransform rt;
        CanvasGroup cg;
        Vector2Int grabOffset;

        public void Init(GameManager manager, PlacedItem model, float cellSize)
        {
            gm = manager; Model = model; cell = cellSize;
            rt = GetComponent<RectTransform>();
            cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(model.Def.Width * cell, model.Def.Height * cell);
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
            var label = UIFactory.Label(transform, "name", Model.Def.Name, 20, TextAnchor.MiddleCenter, new Color(0.1f, 0.1f, 0.1f));
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

            transform.SetParent(gm.DragLayer, true);
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

            // 버리기 영역?
            if (gm.TrashRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(gm.TrashRect, e.position, e.pressEventCamera))
            {
                if (InBag) gm.Bag.RemoveFromBag(Model);
                gm.RemoveTrayItem(this);
                Destroy(gameObject);
                return;
            }

            // 격자 위?
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(gm.GridRect, e.position, e.pressEventCamera, out local))
            {
                float gw = gm.Bag.Width * cell, gh = gm.Bag.Height * cell;
                if (local.x >= 0 && local.x <= gw && -local.y >= 0 && -local.y <= gh)
                {
                    int px = Mathf.FloorToInt(local.x / cell);
                    int py = Mathf.FloorToInt(-local.y / cell);
                    var origin = new Vector2Int(px - grabOffset.x, py - grabOffset.y);
                    if (gm.Bag.CanPlace(Model.Def, origin, Model))
                    {
                        gm.Bag.PlaceAt(Model, origin);
                        InBag = true;
                        gm.RemoveTrayItem(this);
                        AttachToGrid(origin);
                        return;
                    }
                }
            }

            Revert();
        }

        void AttachToGrid(Vector2Int origin)
        {
            transform.SetParent(gm.GridRect, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(origin.x * cell, -origin.y * cell);
        }

        void Revert()
        {
            if (InBag)
                AttachToGrid(Model.Origin);
            else
            {
                transform.SetParent(gm.TrayRect, false);
                gm.LayoutTray();
            }
        }
    }
}
