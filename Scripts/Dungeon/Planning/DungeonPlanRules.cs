using System;
using System.Collections.Generic;

/// <summary>
/// Dungeon 배치 계획 구성 요소가 공유하는 결정적 계산 규칙.
/// </summary>
internal static class DungeonPlanRules
{
    // 문자열과 정수를 런타임 버전에 독립적인 결정적 Seed로 결합한다.
    public static int CombineSeed(int seed, string value, int salt = 0)
    {
        unchecked
        {
            uint hash = (uint)seed ^ 2166136261u ^ (uint)salt;

            if (value != null)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
            }

            return (int)hash;
        }
    }

    // 리스트를 Unity 전역 난수 상태와 무관한 결정적 순서로 섞는다.
    public static void Shuffle<T>(IList<T> values, int seed)
    {
        DungeonPlanRandom random = new DungeonPlanRandom(seed);

        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
        }
    }

    // 계획 생성을 시작할 수 있는 최소 Dungeon 정의 계약인지 확인한다. 자체 생성기와 Edgar 생성기가
    // 생성 백엔드와 무관한 이 게임 규칙 검증을 공유한다.
    public static bool HasValidDefinition(DungeonDefinitionSO definition)
    {
        if (definition == null ||
            string.IsNullOrWhiteSpace(definition.ContentId) ||
            definition.GenerationVersion <= 0 ||
            definition.MinimumContentRoomCount <= 0 ||
            definition.MaximumContentRoomCount < definition.MinimumContentRoomCount ||
            definition.RoomPool == null ||
            definition.RoomPool.Count == 0 ||
            !HasRequiredRoleCandidate(definition, RoomRole.Entrance) ||
            !HasRequiredRoleCandidate(definition, RoomRole.Combat))
        {
            return false;
        }

        if (definition.DungeonType == DungeonType.Boss)
        {
            return definition.MinimumContentRoomCount >= 2 &&
                definition.MaximumContentRoomCount <= 4 &&
                definition.BossArenaRoom != null &&
                definition.BossArenaRoom.HasRole(RoomRole.Boss);
        }

        return definition.DungeonType == DungeonType.Normal;
    }

    // Room Pool에 지정 역할 후보가 하나 이상 있는지 확인한다.
    public static bool HasRequiredRoleCandidate(DungeonDefinitionSO definition, RoomRole role)
    {
        for (int i = 0; i < definition.RoomPool.Count; i++)
        {
            RoomArchetypeSO room = definition.RoomPool[i];

            if (room != null && room.HasRole(role))
            {
                return true;
            }
        }

        return false;
    }

    // 두 출입구 방향이 서로 마주 보는지 반환한다.
    public static bool AreOpposite(RoomConnectionDirection first, RoomConnectionDirection second)
    {
        return GetOppositeDirection(first) == second;
    }

    // 출입구 방향과 맞물리는 반대 방향을 반환한다.
    public static RoomConnectionDirection GetOppositeDirection(RoomConnectionDirection direction)
    {
        switch (direction)
        {
            case RoomConnectionDirection.Up:
                return RoomConnectionDirection.Down;

            case RoomConnectionDirection.Right:
                return RoomConnectionDirection.Left;

            case RoomConnectionDirection.Down:
                return RoomConnectionDirection.Up;

            case RoomConnectionDirection.Left:
                return RoomConnectionDirection.Right;

            default:
                return direction;
        }
    }

    // 출입구 방향에서 인접 셀로 이동하는 단위 좌표를 반환한다.
    public static UnityEngine.Vector2Int GetDirectionOffset(RoomConnectionDirection direction)
    {
        switch (direction)
        {
            case RoomConnectionDirection.Up:
                return UnityEngine.Vector2Int.up;
            case RoomConnectionDirection.Right:
                return UnityEngine.Vector2Int.right;
            case RoomConnectionDirection.Down:
                return UnityEngine.Vector2Int.down;
            case RoomConnectionDirection.Left:
                return UnityEngine.Vector2Int.left;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }
    }
}

/// <summary>
/// 생성 버전이 유지되는 동안 같은 Seed에서 같은 순서를 보장하는 전용 난수원.
/// </summary>
internal struct DungeonPlanRandom
{
    private uint _state;

    public DungeonPlanRandom(int seed)
    {
        _state = unchecked((uint)seed) + 0x9E3779B9u;

        if (_state == 0u)
        {
            _state = 0xA341316Cu;
        }
    }

    // 0 이상 maxExclusive 미만의 결정적 정수를 반환한다.
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }

        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return (int)(_state % (uint)maxExclusive);
    }
}
