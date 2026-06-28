using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;
using SeoulLast;

namespace SeoulLast.EditorTools
{
    // FlowCanvas를 씬에 실제 GameObject로 생성 (UI 개발자가 직접 편집할 수 있도록).
    // GameFlow.BuildUI(런타임 빌드 코드)를 에디트 모드에서 호출해 동일한 계층을 만든 뒤,
    // 직렬화 가능한 에셋 폰트/빌트인 스프라이트로 교체하고 저장한다.
    // 런타임에는 GameFlow가 이 FlowCanvas를 이름으로 바인딩하고 버튼 동작만 코드로 연결한다.
    public static class FlowCanvasBuilder
    {
        const string FontPath = "Assets/Fonts/malgun.ttf";

        [MenuItem("NoPainYesGame/Build Flow Canvas In Scene")]
        public static void Build()
        {
            var res = BuildCore();
            if (res == null) { EditorUtility.DisplayDialog("오류", "씬에 GameFlow 오브젝트가 없거나 BuildUI 호출에 실패했습니다.", "확인"); return; }
            EditorUtility.DisplayDialog("완료",
                $"FlowCanvas를 씬에 생성했습니다.\nText {res[0]}개 / Image {res[1]}개\n" +
                "런타임엔 GameFlow가 이름으로 바인딩하고 버튼만 연결합니다.", "확인");
        }

        // 다이얼로그 없는 핵심 로직(테스트/자동화용). 반환: {Text수, Image수}, 실패 시 null.
        public static int[] BuildCore()
        {
            var gf = Object.FindObjectOfType<GameFlow>();
            if (gf == null) return null;

            var existing = GameObject.Find("FlowCanvas");
            if (existing != null) Object.DestroyImmediate(existing);

            // 런타임 UI 빌드 코드를 그대로 에디트 모드에서 실행 → 동일 계층 생성
            var m = typeof(GameFlow).GetMethod("BuildUI", BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null) return null;
            m.Invoke(gf, null);

            var canvas = GameObject.Find("FlowCanvas");
            if (canvas == null) return null;

            // 직렬화 가능한 에셋으로 교체 (런타임 동적 폰트/스프라이트는 씬에 저장되지 않음)
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            int texts = 0, imgs = 0;
            foreach (var t in canvas.GetComponentsInChildren<Text>(true)) { if (font != null) t.font = font; texts++; }
            foreach (var img in canvas.GetComponentsInChildren<Image>(true)) { if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; } imgs++; }

            // Explore 화면 리소스 할당 (Assets/UI, Assets/art)
            AssignRaw(canvas, "EventPanel/Bg", "Assets/art/Backgrond/Bg_ex1.jpg");
            AssignSprite(canvas, "EventPanel/Char", "Assets/art/Character/character_example.png");
            AssignSprite(canvas, "EventPanel/Item", "Assets/UI/1_1item.png");
            string[] icons = {
                "Assets/UI/icon/sangtea-isan_h.png", "Assets/UI/icon/sangtea-isan_w.png",
                "Assets/UI/icon/sangtea-isan_hs.png", "Assets/UI/icon/sangtea-isan-cra.png" };
            for (int i = 0; i < 4; i++) AssignSprite(canvas, "EventPanel/Status" + i, icons[i]);
            try { SetupSpineCharacter(canvas); } catch (System.Exception e) { Debug.LogWarning("[FlowCanvasBuilder] Spine 캐릭터 설정 실패(플레이스홀더 유지): " + e.Message); }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            return new[] { texts, imgs };
        }

        const string SpineDataPath = "Assets/art/Character/walk/Character_Yeger_walk_SkeletonData.asset";
        const string SpineIdleDataPath = "Assets/art/Character/idle_robi/NPYGchan_SkeletonData.asset";

        // Spine 걷기 캐릭터를 EventPanel에 베이크 (walk 루프). 플레이스홀더 Char 숨김.
        static void SetupSpineCharacter(GameObject canvas)
        {
            var ep = canvas.transform.Find("EventPanel"); if (ep == null) return;
            var old = ep.Find("CharSpine"); if (old != null) Object.DestroyImmediate(old.gameObject);

            var data = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(SpineDataPath);
            if (data == null) { Debug.LogWarning("[FlowCanvasBuilder] SkeletonDataAsset 없음: " + SpineDataPath); return; }

            var sg = SkeletonGraphic.NewSkeletonGraphicGameObject(data, ep, null);
            sg.gameObject.name = "CharSpine";
            sg.startingAnimation = "walk";
            sg.startingLoop = true;
            sg.Initialize(true);

            var rt = sg.rectTransform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0.5f, 0f);
            rt.localScale = Vector3.one * 0.6f;
            rt.anchoredPosition = new Vector2(160, -1100);   // 걷기 캐릭터 자리(에디터에서 미세조정)

            // 정면 idle 스켈레톤 (O001-01~08 등 정지 장면용). 기본 비활성, 위치는 walk와 동일.
            var oldIdle = ep.Find("CharSpineIdle"); if (oldIdle != null) Object.DestroyImmediate(oldIdle.gameObject);
            var idleData = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(SpineIdleDataPath);
            if (idleData != null)
            {
                var sgi = SkeletonGraphic.NewSkeletonGraphicGameObject(idleData, ep, null);
                sgi.gameObject.name = "CharSpineIdle";
                sgi.startingAnimation = "robi_idle";
                sgi.startingLoop = true;
                sgi.Initialize(true);
                var rti = sgi.rectTransform;
                rti.anchorMin = new Vector2(0, 1); rti.anchorMax = new Vector2(0, 1); rti.pivot = new Vector2(0.5f, 0f);
                rti.localScale = Vector3.one * 0.6f;
                rti.anchoredPosition = new Vector2(-120, -700);
                sgi.gameObject.SetActive(false);
            }
            else Debug.LogWarning("[FlowCanvasBuilder] idle SkeletonDataAsset 없음: " + SpineIdleDataPath);

            var ch = ep.Find("Char"); if (ch != null) ch.gameObject.SetActive(false);
        }

        static void AssignRaw(GameObject canvas, string path, string asset)
        {
            var t = canvas.transform.Find(path); if (t == null) return;
            var raw = t.GetComponent<RawImage>(); if (raw == null) return;
            var imp = AssetImporter.GetAtPath(asset) as TextureImporter;
            if (imp != null && imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; imp.SaveAndReimport(); }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(asset);
            if (tex != null) { raw.texture = tex; raw.color = Color.white; }
        }

        static void AssignSprite(GameObject canvas, string path, string asset)
        {
            var t = canvas.transform.Find(path); if (t == null) return;
            var img = t.GetComponent<Image>(); if (img == null) return;
            var imp = AssetImporter.GetAtPath(asset) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; imp.SaveAndReimport(); }
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(asset);
            if (sp != null) { img.sprite = sp; img.type = Image.Type.Simple; img.color = Color.white; }
        }
    }
}
