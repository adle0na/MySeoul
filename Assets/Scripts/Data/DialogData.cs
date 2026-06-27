using UnityEngine;

namespace SeoulLast.Data
{
    // 대화 노드 1개 = event_dialog 테이블 한 행.
    // 이벤트(EventData.startDialogId)에서 시작해 분기의 nextDialogId로 그래프 진행.
    [CreateAssetMenu(fileName = "Dialog_", menuName = "NoPainYesGame/Dialog Data")]
    public class DialogData : ScriptableObject
    {
        public string dialogId;                      // DialogId
        public string spawnItemId;                   // DialogItemId (화면에 스폰되는 아이템)
        [TextArea(2, 4)] public string description;  // Description (대사/시나리오)

        // 분기 A/B/C. EventChoice 재사용:
        //  - label    = Branch_name ("" 이면 버튼 없이 자동 진행)
        //  - requiredItem = Branch_ItemID
        //  - opensInventory = Branch_OpensInventory
        //  - newState = Branch_NewState
        //  - nextEventId = Branch_NextEventId (= 다음 "대화" id, "Done" = 이벤트 종료)
        public EventChoice[] choices;
    }
}
