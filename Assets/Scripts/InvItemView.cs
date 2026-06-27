using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeoulLast
{
    // 그리드 가방을 쓰는 화면이 구현하는 호스트 계약.
    // 창고 개념을 없앤 새 흐름에서 StorageRect = "획득 대기 트레이", TrashRect = "버리기 존".
    public interface IBagHost
    {
        RectTransform BagGridRect { get; }   // 그리드(셀 좌표 기준 배치)
        RectTransform BagDragLayer { get; }  // 드래그 중 최상단 레이어
        RectTransform StorageRect { get; }   // 미배치 아이템(트레이). null이면 비활성
        RectTransform TrashRect { get; }     // 버리기 존. null이면 비활성
        BagModel Bag { get; }
        bool LockerOpen { get; }
        float Cell { get; }
        void PlaceInBag(InvItemView item, Vector2Int origin);
        void MoveToStorage(InvItemView item);
        void ReturnToStorage(InvItemView item);
        void Discard(InvItemView item);
        void SelectItem(InvItemView item);
    }

    // 인벤토리 아이템 뷰. 그리드 ↔ 트레이 드래그, 버리기 존에 놓으면 폐기, 클릭하면 선택.
    public class InvItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public PlacedItem Model;
        public bool InBag;

        IBagHost host;
        float cell;
        RectTransform rt;
        CanvasGroup cg;
        Vector2Int grabOffset;

        public void Init(IBagHost h, PlacedItem m, float cellSize)
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
            // 아이템 이미지가 있으면 모양 박스 위에 스프라이트 표시
            if (Model.Def.Icon != null)
            {
                var ico = UIFactory.Img(transform, "icon", Color.white);
                ico.sprite = Model.Def.Icon; ico.type = Image.Type.Simple; ico.preserveAspect = true;
                ico.raycastTarget = false;
                var irt = ico.rectTransform;
                irt.anchorMin = new Vector2(0, 1); irt.anchorMax = new Vector2(0, 1); irt.pivot = new Vector2(0, 1);
                irt.sizeDelta = new Vector2(Model.Def.Width * cell, Model.Def.Height * cell);
                irt.anchoredPosition = Vector2.zero;
            }
            // 이름 + 상태(회복류 / 내구도)
            string sub = Model.Def.IsRecovery ? "" : "  x" + Model.Uses;
            var label = UIFactory.Label(transform, "n", Model.Def.Name + sub, 20, TextAnchor.MiddleCenter, new Color(0.12f, 0.12f, 0.12f));
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

            // 1) 버리기 존 위 → 폐기
            if (host.TrashRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(host.TrashRect, e.position, e.pressEventCamera))
            {
                host.Discard(this);
                return;
            }

            // 2) 그리드 위 → 배치 시도
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

            // 3) 트레이 영역 위 → 미배치 보관
            if (host.LockerOpen && host.StorageRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(host.StorageRect, e.position, e.pressEventCamera))
            {
                host.MoveToStorage(this);
                return;
            }

            // 4) 그 외 → 원위치
            if (InBag) AttachToBag(Model.Origin);
            else host.ReturnToStorage(this);
        }

        // 클릭(드래그 아님)하면 선택. 드래그 시엔 EventSystem이 click을 발생시키지 않음.
        public void OnPointerClick(PointerEventData e)
        {
            host.SelectItem(this);
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
