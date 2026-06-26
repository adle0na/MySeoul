using UnityEngine;

namespace SeoulLast.Data
{
    // 기획 Event 테이블 컬럼과 1:1 매핑.
    [CreateAssetMenu(fileName = "Event_", menuName = "NoPainYesGame/Event Data")]
    public class EventData : ScriptableObject
    {
        [Header("기본 정보")]
        public string eventId;            // EventId
        public string eventName;          // EventName
        public string eventType;          // EventType (일반/아이템/특정지역)
        public string eventCategory;      // EventCategory
        public int probability;           // EventProbability(%)
        public string hasBranch;          // EventHasBranch (단순/분기형)

        [Header("지역 정보 (특정지역 전용)")]
        public string region;             // EventRegion
        public string regionEntryCondition;   // EventRegionEntryCondition
        public string regionSpecialEffect;    // EventRegionSpecialEffect

        [Header("아이템 연동")]
        public string relatedItemId;      // EventRelatedItemId
        [TextArea(2, 3)] public string itemOwnedResult;     // EventItemOwnedResult
        [TextArea(2, 3)] public string itemNotOwnedResult;  // EventItemNotOwnedResult

        [Header("분기 시나리오")]
        [TextArea(2, 3)] public string branchAResult;   // EventBranchAResult
        [TextArea(2, 3)] public string branchBResult;   // EventBranchBResult

        [Header("자원 영향")]
        public int foodWaterChange;       // EventFoodWaterChange
        public int weaponAmmoChange;      // EventWeaponAmmoChange
        public int moraleChange;          // EventMoraleChange
        public int medicalSupplyChange;   // EventMedicalSupplyChange

        [Header("상태이상")]
        public string statusEffect;       // 유발 상태이상
        public int statusDuration;        // 지속 턴
        public string statusModifier;     // 상태이상 수식어

        [Header("메타")]
        public string difficulty;         // 난이도
        [TextArea(1, 3)] public string note; // 비고
    }
}
