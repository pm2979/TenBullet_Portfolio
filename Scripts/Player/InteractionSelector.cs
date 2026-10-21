using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상호작용 후보의 위치와 선택 가능 상태를 나타낸다.
/// </summary>
public readonly struct InteractionCandidate
{
    public readonly Vector2 Position;
    public readonly bool IsValid;

    public InteractionCandidate(Vector2 position, bool isValid)
    {
        Position = position;
        IsValid = isValid;
    }
}

/// <summary>
/// 상호작용 반경 안에서 가장 가까운 유효 후보를 선택한다.
/// </summary>
public sealed class InteractionSelector
{
    /// <summary>
    /// 가장 가까운 유효 후보의 인덱스를 반환하며, 없으면 -1을 반환한다.
    /// 거리가 같으면 입력 목록에서 먼저 나온 후보를 선택한다.
    /// </summary>
    public int SelectNearestIndex(Vector2 originPosition, float interactionRadius, IReadOnlyList<InteractionCandidate> candidates)
    {
        int bestIndex = -1;
        float bestDistanceSquared = 0f;
        float radiusSquared = interactionRadius * interactionRadius;

        for (int i = 0; i < candidates.Count; i++)
        {
            InteractionCandidate candidate = candidates[i];

            if (!candidate.IsValid) continue;

            float distanceSquared = (candidate.Position - originPosition).sqrMagnitude;

            if (distanceSquared > radiusSquared) continue;

            if (bestIndex == -1 || distanceSquared < bestDistanceSquared)
            {
                bestIndex = i;
                bestDistanceSquared = distanceSquared;
            }
        }

        return bestIndex;
    }
}
