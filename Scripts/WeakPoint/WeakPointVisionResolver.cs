using UnityEngine;

/// <summary>
/// 플레이어가 특정 위치의 약점을 볼 수 있는지 판정하는 좁은 공용 경계.
/// 노출 여부는 StatType.WeakPointReveal 게이트, 거리는 max(기본 시야, 무기 최종 사거리),
/// 차폐는 소유 몬스터 본체 기준 Wall Linecast로 정한다. 투사체가 닿을 수 있는 거리까지는
/// 약점도 보인다는 규칙을 한 곳에서만 계산해 WeakPointTarget들이 각자 복제하지 않게 한다
/// </summary>
public static class WeakPointVisionResolver
{
    private const string WallLayerName = "Wall";
    private const int UninitializedLayerMask = -1;

    private static int _wallLayerMask = UninitializedLayerMask;

    /// <summary>
    /// 주어진 약점이 지금 플레이어에게 보이는지 판정한다. 차폐는 회전하는 약점이 아니라
    /// 소유 몬스터 본체 위치를 기준으로 본다.
    /// Player나 Stats·PlayerDataSO가 아직 준비되지 않았으면 보이지 않는 것으로 본다(안전측)
    /// </summary>
    public static bool IsVisible(Player player, Vector2 weakPointPosition, Vector2 ownerPosition)
    {
        if (!IsRevealed(player))
        {
            return false;
        }

        Vector2 playerPosition = player.GetPlayerPosition();
        float viewDistance = ResolveViewDistance(player);

        if ((playerPosition - weakPointPosition).sqrMagnitude > viewDistance * viewDistance)
        {
            return false;
        }

        // 물리 질의는 노출·거리 조건을 모두 통과한 뒤에만 수행한다
        return !Physics2D.Linecast(playerPosition, ownerPosition, ResolveWallLayerMask());
    }

    private static bool IsRevealed(Player player)
    {
        if (player == null || player.playerDetails == null || player.stats == null || player.stats.RuntimeStats == null)
        {
            return false;
        }

        return player.stats.RuntimeStats.ApplyModifiers(StatType.WeakPointReveal, player.playerDetails.weakPointRevealBase) > 0f;
    }

    // 사거리 최종값은 발사 경로와 같은 경계를 재사용해 Range Modifier 계산을 복제하지 않는다
    private static float ResolveViewDistance(Player player)
    {
        float baseViewDistance = player.playerDetails.baseViewDistance;

        Weapon weapon = player.activeWeapon != null ? player.activeWeapon.GetCurrentWeapon() : null;

        if (weapon == null || weapon.weaponDetails == null)
        {
            return baseViewDistance;
        }

        float weaponRange = ProjectileFireSnapshotResolver.ResolveRange(player, weapon.weaponDetails.weaponRange);

        return Mathf.Max(baseViewDistance, weaponRange);
    }

    // Wall 레이어는 런타임에 바뀌지 않으므로 한 번만 조회해 캐시한다.
    // 레이어가 없으면 0이 되어 Linecast가 아무것도 맞히지 않고 항상 비차폐로 동작한다
    private static int ResolveWallLayerMask()
    {
        if (_wallLayerMask == UninitializedLayerMask)
        {
            _wallLayerMask = LayerMask.GetMask(WallLayerName);
        }

        return _wallLayerMask;
    }
}
