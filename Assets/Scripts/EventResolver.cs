using System.Collections.Generic;
using SeoulLast.Data;

namespace SeoulLast
{
    // 보유 아이템 + 지역 조건으로 후보 풀을 만들고, probability를 가중치로 비례 추첨(A안).
    public static class EventResolver
    {
        // nothingWeight = "평범한 하루(무이벤트)" 가중치
        public static EventData Pick(IList<EventData> all, ICollection<string> heldItemNames, string region,
                                     System.Random rng, int nothingWeight = 30)
        {
            var pool = new List<EventData>();
            var weights = new List<float>();

            foreach (var e in all)
            {
                if (e == null || e.probability <= 0) continue;

                bool include;
                switch (e.eventType)
                {
                    case "일반": include = true; break;
                    case "아이템": include = HeldMatches(e.relatedItemId, heldItemNames); break;
                    case "특정지역": include = !string.IsNullOrEmpty(region) && e.region == region; break;
                    default: include = true; break;
                }
                if (!include) continue;

                pool.Add(e);
                weights.Add(e.probability);
            }

            float total = nothingWeight;
            for (int i = 0; i < weights.Count; i++) total += weights[i];
            if (total <= 0) return null;

            float r = (float)(rng.NextDouble() * total);
            for (int i = 0; i < pool.Count; i++)
            {
                if (r < weights[i]) return pool[i];
                r -= weights[i];
            }
            return null; // 무이벤트
        }

        // relatedItemId가 자연어("방독면 / 마스크")라 보유 아이템 '이름'이 포함되는지로 매칭(임시).
        // 추후 아이템 ID 기반으로 교체 권장.
        static bool HeldMatches(string relatedItemId, ICollection<string> held)
        {
            if (string.IsNullOrEmpty(relatedItemId) || held == null) return false;
            foreach (var h in held)
                if (!string.IsNullOrEmpty(h) && relatedItemId.Contains(h)) return true;
            return false;
        }
    }
}
