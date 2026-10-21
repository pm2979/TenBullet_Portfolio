using System;

/// <summary>
/// 등록된 능력치 보정 하나를 제거할 때 사용하는 런타임 식별자다.
/// 발급한 RuntimeStats 인스턴스에서만 유효하며 저장 데이터의 식별자로 사용하지 않는다.
/// </summary>
public readonly struct StatModifierHandle : IEquatable<StatModifierHandle>
{
    private readonly int _id;

    internal StatModifierHandle(int id)
    {
        _id = id;
    }

    public bool Equals(StatModifierHandle other)
    {
        return _id == other._id;
    }

    public override bool Equals(object obj)
    {
        return obj is StatModifierHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _id.GetHashCode();
    }
}
