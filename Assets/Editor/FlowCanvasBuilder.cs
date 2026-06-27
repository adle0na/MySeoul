using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
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

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            return new[] { texts, imgs };
        }
    }
}
