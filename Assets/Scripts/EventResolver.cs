using System.Collections.Generic;
using SeoulLast.Data;

namespace SeoulLast
{
    // 확정 분기: 선택한 방의 이벤트를 찾고, 보유 아이템 유무로 결과가 '확정'된다(RNG 아님).
    public static class EventResolver
    {
        // 방에 해당하는 이벤트를 찾는다. 없으면 null(=평범한 하루).
        public static EventData FindForRoom(IList<EventData> events, string room)
        {
            if (events == null || string.IsNullOrEmpty(room)) return null;
            foreach (var e in events)
                if (e != null && e.region == room) return e;
            return null;
        }

        // 방의 이벤트 중 하나를 랜덤으로 (어떤 이벤트가 뜰지만 랜덤, 결과는 선택지/아이템으로 확정).
        public static EventData PickRandomForRoom(IList<EventData> events, string room, System.Random rng)
        {
            var matches = new List<EventData>();
            if (events != null)
                foreach (var e in events)
                    if (e != null && e.region == room) matches.Add(e);
            if (matches.Count == 0) return null;
            return matches[rng.Next(matches.Count)];
        }

        // 보유 아이템 이름이 relatedItemId에 들어 있으면 보유로 판정.
        public static bool HasRequiredItem(EventData ev, ICollection<string> heldNames)
        {
            if (ev == null || string.IsNullOrEmpty(ev.relatedItemId) || heldNames == null) return false;
            foreach (var h in heldNames)
                if (!string.IsNullOrEmpty(h) && ev.relatedItemId.Contains(h)) return true;
            return false;
        }
    }
}
