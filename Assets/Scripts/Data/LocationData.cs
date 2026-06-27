using UnityEngine;

namespace SeoulLast.Data
{
    // Location 시트 한 행 = 게임 내 장소(지도 선택지).
    [CreateAssetMenu(fileName = "Loc_", menuName = "NoPainYesGame/Location Data")]
    public class LocationData : ScriptableObject
    {
        public string locationId;                    // LocationID
        public string locationName;                  // LocationName (지도 표시명 = EventRegion과 매칭)
        public int floor;                            // LocationFloor (층. 지도에서 층별 배치 기준)
        public bool isLock;                          // LocationIsLock (true=잠김)
        public int visitCount;                       // LocationVisitCount
        [TextArea(2, 4)] public string description;  // LocationDescription
    }
}
