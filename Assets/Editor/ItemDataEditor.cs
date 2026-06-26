using UnityEditor;
using UnityEngine;
using SeoulLast.Data;

namespace SeoulLast.EditorTools
{
    [CustomEditor(typeof(ItemData))]
    public class ItemDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var item = (ItemData)target;

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "shape", "m_Script");
            serializedObject.ApplyModifiedProperties();

            item.EnsureSize();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"모양 (클릭해서 칠하기) — {ItemData.GridW} x {ItemData.GridH}", EditorStyles.boldLabel);

            int count = 0;
            for (int y = 0; y < ItemData.GridH; y++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < ItemData.GridW; x++)
                {
                    bool v = item.GetCell(x, y);
                    if (v) count++;
                    Color prev = GUI.backgroundColor;
                    GUI.backgroundColor = v ? new Color(0.30f, 0.70f, 1.0f) : new Color(0.28f, 0.28f, 0.30f);
                    if (GUILayout.Button(v ? "■" : "", GUILayout.Width(34), GUILayout.Height(34)))
                    {
                        Undo.RecordObject(item, "Toggle Shape Cell");
                        item.SetCell(x, y, !v);
                        EditorUtility.SetDirty(item);
                    }
                    GUI.backgroundColor = prev;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("모두 지우기", GUILayout.Width(110), GUILayout.Height(24)))
            {
                Undo.RecordObject(item, "Clear Shape");
                for (int i = 0; i < item.shape.Length; i++) item.shape[i] = false;
                EditorUtility.SetDirty(item);
            }
            if (GUILayout.Button("좌상단으로 정렬", GUILayout.Width(130), GUILayout.Height(24)))
            {
                Undo.RecordObject(item, "Normalize Shape");
                item.Normalize();
                EditorUtility.SetDirty(item);
            }
            EditorGUILayout.LabelField($"점유 {count}칸   |   마스크: {item.GetShapeMask()}");
            EditorGUILayout.EndHorizontal();

            // 분리된 모양 경고 (아이템은 한 덩어리여야 함)
            if (!item.IsConnected())
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox("모양이 분리되어 있습니다. 아이템은 상하좌우로 이어진 한 덩어리여야 합니다.", MessageType.Warning);
            }
        }

        // 다른 오브젝트로 선택을 옮기면(편집 종료) 자동으로 좌상단 정렬하여 저장.
        // 칠하는 도중엔 튀지 않도록, 편집이 끝난 이 시점에만 정규화한다.
        void OnDisable()
        {
            var item = target as ItemData;
            if (item == null) return;
            int before = ShapeHash(item);
            item.Normalize();
            if (ShapeHash(item) != before)
                EditorUtility.SetDirty(item);
        }

        static int ShapeHash(ItemData item)
        {
            if (item.shape == null) return 0;
            int h = 17;
            for (int i = 0; i < item.shape.Length; i++) h = h * 31 + (item.shape[i] ? 1 : 0);
            return h;
        }
    }
}
