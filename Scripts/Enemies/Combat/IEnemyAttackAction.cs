/// <summary>Controller의 공격 단계 신호를 받아 개별 공격 출력을 실행하는 계약</summary>
// 단계 시간·선택·취소는 Controller가 소유하고 Action은 출력과 자체 정리만 소유
public interface IEnemyAttackAction
{
    /// <summary>공통 후보 조건 통과 후 Action 고유 의존성 검사</summary>
    bool CanStart(EnemyAttackContext context);

    /// <summary>Windup 진입 시 공격별 준비 상태 초기화</summary>
    void OnWindupStart(EnemyAttackContext context, TargetSnapshot targetSnapshot);

    /// <summary>Controller가 확정한 AimSnapshot을 공격 출력에 고정</summary>
    void OnAimLocked(EnemyAttackContext context, AimSnapshot aimSnapshot);

    /// <summary>Active 진입 시 실제 공격 출력 시작</summary>
    void OnActiveStart(EnemyAttackContext context, AimSnapshot aimSnapshot);

    /// <summary>Recovery 진입 시 남은 Active 출력 중단</summary>
    void OnRecoveryStart(EnemyAttackContext context);

    /// <summary>단계 전환과 무관한 실제 출력 Marker 처리</summary>
    void OnActionMarker(EnemyAttackContext context, int markerId);

    /// <summary>정상 완료 후 Action 보유 상태 정리</summary>
    void OnCompleted(EnemyAttackContext context);

    /// <summary>취소 이유에 맞춰 진행 중 출력과 Action 보유 상태 정리</summary>
    void OnCancelled(EnemyAttackContext context, EnemyAttackCancelReason reason);
}
