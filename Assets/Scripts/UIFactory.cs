using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace SeoulLast
{
    // 코드로 uGUI를 만드는 공통 팩토리. TextMeshPro 기반.
    public static class UIFactory
    {
        static TMP_FontAsset _tmpFont;
        static Sprite _white;

        public static TMP_FontAsset GetTMPFont()
        {
            if (_tmpFont == null)
                _tmpFont = UnityEngine.Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
#if UNITY_EDITOR
            if (_tmpFont == null)
                _tmpFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/Fonts/Paperlogy/TMP/Paperlogy-7Bold SDF.asset");
#endif
            return _tmpFont;
        }

        // sprite 없는 Image는 렌더되지 않으므로 단색 스프라이트를 만들어 사용
        public static Sprite White()
        {
            if (_white == null)
            {
                var tex = new Texture2D(4, 4);
                var px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            }
            return _white;
        }

        public static void SetRect(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        public static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Fill(rt);
            var img = go.GetComponent<Image>();
            img.sprite = White();
            img.color = color;
            return img;
        }

        public static Image Img(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = White();
            img.color = color;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, int size, TextAlignmentOptions anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = GetTMPFont();
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Color bg, UnityAction onClick, out TextMeshProUGUI labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = White();
            img.color = bg;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(onClick);

            labelText = Label(go.transform, "Label", label, 32, TextAlignmentOptions.Center, Color.white);
            Fill(labelText.GetComponent<RectTransform>());
            return btn;
        }
    }
}
