using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// 일반 두 칸과 특수 한 칸으로 구성된 현재 수배 게시판 상태.
/// </summary>
public sealed class WantedBoardState
{
    public const int NormalSlotCount = 2;
    public const int SpecialSlotIndex = 2;
    public const int TotalSlotCount = 3;

    private readonly WantedBoardSlot[] _slots = new WantedBoardSlot[TotalSlotCount];
    private readonly ReadOnlyCollection<WantedBoardSlot> _readOnlySlots;

    public IReadOnlyList<WantedBoardSlot> Slots => _readOnlySlots;
    public bool IsReady { get; private set; }

    public WantedBoardState()
    {
        _readOnlySlots = Array.AsReadOnly(_slots);
    }

    public bool Contains(WantedPosterSO poster)
    {
        if (!IsReady || poster == null)
        {
            return false;
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].Poster == poster)
            {
                return true;
            }
        }

        return false;
    }

    internal void ReplaceAll(IReadOnlyList<WantedBoardSlot> slots)
    {
        if (slots == null || slots.Count != TotalSlotCount)
        {
            throw new ArgumentException("게시판은 정확히 3개 슬롯이어야 합니다.", nameof(slots));
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i] = slots[i];
        }

        IsReady = true;
    }

    internal void ReplaceSpecial(WantedBoardSlot slot)
    {
        if (!IsReady || slot.SlotType != WantedPosterSlotType.Special)
        {
            throw new InvalidOperationException("준비된 게시판의 특수 슬롯만 교체할 수 있습니다.");
        }

        _slots[SpecialSlotIndex] = slot;
    }

    internal void ReplaceSeed(int slotIndex, int seed)
    {
        if (!IsReady || slotIndex < 0 || slotIndex >= _slots.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        WantedBoardSlot current = _slots[slotIndex];
        _slots[slotIndex] = new WantedBoardSlot(current.SlotType, current.Poster, seed);
    }

    internal void Clear()
    {
        Array.Clear(_slots, 0, _slots.Length);
        IsReady = false;
    }
}
