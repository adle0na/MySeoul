/// <summary>
/// LCS_Folder UI 스크립트가 BagPanelController 를 참조할 때 쓰는 인터페이스.
/// 컴파일 순서 문제(LCS_Folder < Scripts)를 우회한다.
/// </summary>
public interface IBagController
{
    bool IsInventoryOpen { get; }
    void UserToggle();
    void NotifyBagClosing();
    void ForceOpen();
}
