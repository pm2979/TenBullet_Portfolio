using UnityEngine;

/// <summary>
/// 게임의 전체적인 흐름과 상태를 제어하는 핵심 매니저 클래스
/// 싱글톤 패턴으로 구현되어 플레이어 스폰, 점수 관리, 레벨 전환, UI 페이드 효과 등을 총괄
/// </summary>
[DisallowMultipleComponent]
public class GameManager : MonoSingleton<GameManager>
{
    private Player player;


    /// <summary>
    /// 싱글톤 초기화 수행 (플레이어 생성은 데이터베이스 초기화 완료 후 InitializeAsync에서 진행)
    /// </summary>
    protected override void Awake()
    {
        // 베이스 클래스 싱글톤 로직 호출
        base.Awake();
    }

    /// <summary>
    /// 현재 생성된 플레이어 객체 참조 반환
    /// </summary>
    public Player GetPlayer()
    {
        return player;
    }

}

