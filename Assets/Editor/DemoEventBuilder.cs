using System.IO;
using UnityEditor;
using UnityEngine;
using SeoulLast.Data;

namespace SeoulLast.EditorTools
{
    // 데모 이벤트: 날짜 기반 일반 이벤트 + 엔딩 체인 A/B/C (각 3개, 정답 3연속).
    public static class DemoEventBuilder
    {
        [MenuItem("NoPainYesGame/Build Demo Events")]
        public static void Build()
        {
            const string dir = "Assets/Data/Events";
            foreach (var g in AssetDatabase.FindAssets("t:EventData", new[] { dir }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));
            Directory.CreateDirectory(dir);

            // ===== 일반(날짜) 이벤트 — 아이템을 벌고 상태를 관리 =====
            Make("N1", "버려진 사물함", 1, 3, "", "복도 사물함이 열려 있다.", new[]
            {
                C("뒤져서 챙긴다", "", "통조림을 찾았다.", 0, 0, 0, 0, "통조림", false, false),
            });
            Make("N2", "쓰러진 자판기", 1, 4, "", "넘어진 자판기 안에 음료가 보인다.", new[]
            {
                C("흔들어 꺼낸다", "", "생수를 챙겼다.", 0, 0, 0, 5, "생수", false, false),
            });
            Make("N3", "어두운 교실", 1, 5, "", "교실이 칠흑같이 어둡다. 안에 뭔가 있다.", new[]
            {
                C("손전등으로 살핀다", "손전등", "구석에서 구급상자를 찾았다.", 0, 0, 0, 0, "구급상자", false, false),
            });
            Make("N4", "잠긴 창고", 2, 6, "", "식자재 창고가 잠겨 있다.", new[]
            {
                C("도끼로 부순다", "도끼", "식량을 잔뜩 챙겼다.", 0, 0, 0, 0, "통조림", false, false),
            });
            Make("N5", "약품 누출", 3, 7, "", "복도에 매캐한 가스가 찬다.", new[]
            {
                C("방독면 쓰고 통과", "방독면", "안전하게 지나갔다.", 0, 0, 0, 0, "", false, false),
            });
            Make("N6", "잠 못 드는 밤", 2, 9, "", "도무지 잠이 오지 않는다.", new[]
            {
                C("각성제로 버틴다", "각성제", "정신이 번쩍 든다.", 0, 0, 0, -30, "", false, false),
                C("쪽잠을 잔다", "", "선잠이라도 잤다.", 5, 0, 0, -10, "", false, false),
            });
            Make("N7", "낯선 인기척", 4, 9, "", "어둠 속에서 누군가의 기척이 느껴진다.", new[]
            {
                C("도끼를 들고 맞선다", "도끼", "위협하자 물러갔다. 떨군 무전기를 주웠다.", 0, 0, 0, 0, "무전기", false, false),
                C("숨죽여 피한다", "", "들키지 않게 숨었다.", 0, 0, 0, 10, "", false, false),
                C("그냥 지나친다", "", "지나치다 뭔가에 부딪혔다.", 0, 0, 8, 0, "", true, false),
            });
            Make("N8", "보급 상자", 1, 6, "", "낙하한 보급 상자를 발견했다.", new[]
            {
                C("밧줄로 끌어온다", "밧줄", "안전하게 끌어와 열었다.", 0, 0, 0, 0, "각성제", false, false),
                C("맨손으로 연다", "", "겨우 열었다.", 0, 0, 6, 0, "생수", false, false),
            });

            // ===== 엔딩 A — 구조 (무전기/손전등) Day3~ =====
            Make("A1", "옥상 안테나", 3, 99, "A", "옥상에 낡은 안테나가 있다. 신호를 보낼 수 있을까?", new[]
            {
                C("무전기로 구조 신호를 보낸다", "무전기", "잡음 사이로 응답이 잡혔다!", 0, 0, 0, 5, "", false, true),
                C("그냥 소리쳐 본다", "", "목만 쉬고 응답은 없다.", 0, 0, 0, 8, "", false, false),
            });
            Make("A2", "응답이 온다", 3, 99, "A", "무전 너머에서 위치를 묻는다.", new[]
            {
                C("학교 좌표를 또박또박 불러준다", "", "구조대가 위치를 확인했다.", 0, 0, 0, 0, "", false, true),
                C("당황해 말을 더듬는다", "", "신호가 끊겼다.", 0, 0, 0, 5, "", false, false),
            });
            Make("A3", "헬기 접근", 3, 99, "A", "멀리서 헬기 소리가 들린다. 알려야 한다.", new[]
            {
                C("손전등을 크게 흔든다", "손전등", "헬기가 너를 발견했다!", 0, 0, 0, 0, "", false, true),
                C("가만히 기다린다", "", "헬기가 그냥 지나친다.", 0, 0, 0, 0, "", false, false),
            });

            // ===== 엔딩 B — 탈출 (도끼/밧줄) Day5~ =====
            Make("B1", "막힌 비상구", 5, 99, "B", "비상구가 잔해로 막혔다.", new[]
            {
                C("도끼로 잔해를 부순다", "도끼", "길을 뚫었다.", 0, 0, 5, 0, "", false, true),
                C("다른 길을 찾는다", "", "헤매다 시간만 버렸다.", 0, 0, 0, 10, "", false, false),
            });
            Make("B2", "끊긴 계단", 5, 99, "B", "계단이 무너져 아래로 못 내려간다.", new[]
            {
                C("밧줄로 내려간다", "밧줄", "안전하게 내려왔다.", 0, 0, 0, 0, "", false, true),
                C("뛰어내린다", "", "착지하다 발을 접질렸다.", 0, 0, 20, 0, "", false, false),
            });
            Make("B3", "교문 자물쇠", 5, 99, "B", "마지막 관문, 교문이 굳게 잠겨 있다.", new[]
            {
                C("도끼로 자물쇠를 내려친다", "도끼", "자물쇠가 깨졌다. 밖이다!", 0, 0, 5, 0, "", false, true),
                C("담을 기어오른다", "", "미끄러져 떨어졌다.", 0, 0, 15, 0, "", false, false),
            });

            // ===== 엔딩 C — 생존 (라이터/버티기) Day7~ =====
            Make("C1", "마지막 밤", 7, 99, "C", "가장 추운 밤이 찾아왔다.", new[]
            {
                C("라이터로 불을 피운다", "라이터", "온기로 밤을 버틴다.", 0, 0, 0, 0, "", false, true),
                C("웅크려 떤다", "", "체온이 떨어진다.", 0, 0, 12, 0, "", false, false),
            });
            Make("C2", "바닥난 물자", 7, 99, "C", "남은 게 거의 없다.", new[]
            {
                C("아껴둔 걸 나눠 먹는다", "", "조금이나마 버틴다.", -10, -10, 0, 0, "", false, true),
                C("전부 먹어치운다", "", "당장은 배부르지만 불안하다.", -20, -20, 0, 0, "", false, false),
            });
            Make("C3", "동틀 무렵", 7, 99, "C", "여명이 밝아온다. 조금만 더.", new[]
            {
                C("이를 악물고 버틴다", "", "마침내 아침이 왔다.", 0, 0, 0, 0, "", false, true),
                C("주저앉는다", "", "마지막 고비를 넘기지 못한다.", 0, 0, 10, 10, "", false, false),
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DemoEventBuilder] 날짜 이벤트 + 엔딩 체인 생성 완료.");
        }

        static EventChoice C(string label, string req, string result, int h, int t, int p, int f, string reward, bool lose, bool correct)
        {
            return new EventChoice
            {
                label = label, requiredItem = req, resultText = result,
                hunger = h, thirst = t, pain = p, fatigue = f,
                rewardItem = reward, loseRandomItem = lose, correct = correct
            };
        }

        static void Make(string id, string name, int dmin, int dmax, string ending, string situation, EventChoice[] choices)
        {
            var ev = ScriptableObject.CreateInstance<EventData>();
            ev.eventId = id; ev.eventName = name; ev.dayMin = dmin; ev.dayMax = dmax;
            ev.endingId = ending; ev.situation = situation; ev.choices = choices; ev.probability = 100;
            AssetDatabase.CreateAsset(ev, "Assets/Data/Events/" + id + ".asset");
        }
    }
}
