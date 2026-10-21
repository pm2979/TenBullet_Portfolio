// 플레이어가 상호작용(사용)할 수 있는 오브젝트(예: 상자, 문, 아이템 등)가 구현하는 인터페이스
public interface IInteractable
{
    // 플레이어가 해당 아이템/오브젝트를 사용했을 때 실행되는 동작을 정의한다
    void Interact();
}
