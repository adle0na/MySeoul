using UnityEngine;

namespace SeoulLast.Data
{
    // 이벤트 선택지. requiredItem이 있으면 보유 시에만 활성(미보유 시 비활성 버튼으로 노출).
    [System.Serializable]
    public class EventChoice
    {
        public string label;                       // 버튼 텍스트
        public string requiredItem;                // 필요 아이템 이름("" = 조건 없음)
        [TextArea(2, 3)] public string resultText;  // 선택 결과
        public int hunger, thirst, pain, fatigue;  // 상태 변화(양수=악화). 4종: 허기/수분/건강/기운
        public bool bagUpgrade;                    // 가방 단계 상승

        [Header("분기 그래프(시트)")]
        public bool opensInventory;                // 선택 시 가방 열림(아이템 배치) 이벤트
        public string newState;                    // 선택 시 플레이어 상태 변경 토큰 (예: "건강+20;기운-10")
        public string nextEventId;                 // 선택 후 이동할 이벤트 Id("" = 일반 진행)

        [Header("엔딩 체인용")]
        public bool correct;                       // 엔딩 이벤트에서 '정답' 선택지인가
        [Header("보상/회복/분실")]
        public string rewardItem;                  // 획득 아이템 id("" = 없음)
        public bool loseRandomItem;                // 보유 아이템 하나 분실
    }

    // 기획 Event 테이블 컬럼과 1:1 매핑 (+ 선택지).
    [CreateAssetMenu(fileName = "Event_", menuName = "NoPainYesGame/Event Data")]
    public class EventData : ScriptableObject
    {
        [Header("발생 / 대화 (신 구조)")]
        public int minLevel;            // EventMinLv (접근 가능 최소 레벨)
        public string startDialogId;    // EventDialogId (시작 대화 id)

        [Header("발생 시점 / 엔딩 (구 구조, 미사용)")]
        public int dayMin = 1;          // 이 이벤트가 뜰 수 있는 최소 날짜
        public int dayMax = 99;         // 최대 날짜
        public string endingId = "";    // "" = 일반 이벤트, "A"/"B"/"C" = 해당 엔딩 체인

        [Header("선택지 (아이템 게이트)")]
        public string situation;        // 상황 설명 (= description)
        public EventChoice[] choices;

        [Header("시트 기본 정보")]
        public string objectId;         // EventObjectID (등장하는 물건)
        [TextArea(2, 4)] public string description; // EventDescription (시나리오/상황)

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

        [Header("자원 영향 (보유/성공 시)")]
        public int foodWaterChange;       // EventFoodWaterChange
        public int weaponAmmoChange;      // EventWeaponAmmoChange
        public int moraleChange;          // EventMoraleChange
        public int medicalSupplyChange;   // EventMedicalSupplyChange

        [Header("자원 영향 (미보유/실패 시)")]
        public int failFoodWater;
        public int failMorale;
        public int failMedical;

        [Header("상태이상")]
        public string statusEffect;       // 유발 상태이상
        public int statusDuration;        // 지속 턴
        public string statusModifier;     // 상태이상 수식어

        [Header("메타")]
        public string difficulty;         // 난이도
        [TextArea(1, 3)] public string note; // 비고
    }
}
