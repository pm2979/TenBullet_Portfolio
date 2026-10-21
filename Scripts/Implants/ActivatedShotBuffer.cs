// 발동한 Shot 식별자를 고정 크기로 보관하는 순환 버퍼
public sealed class ActivatedShotBuffer
{
    private readonly int[] _shotIds;
    private int _writeIndex;

    // 지정한 수용량의 발동 기록 저장소 생성
    public ActivatedShotBuffer(int capacity = 8)
    {
        _shotIds = new int[capacity];
    }

    // 동일한 Shot의 발동 기록 존재 여부 반환
    public bool Contains(int shotId)
    {
        for (int i = 0; i < _shotIds.Length; i++)
        {
            if (_shotIds[i] == shotId)
            {
                return true;
            }
        }

        return false;
    }

    // 가장 오래된 기록부터 덮어쓰는 Shot 식별자 추가
    public void Add(int shotId)
    {
        _shotIds[_writeIndex] = shotId;
        _writeIndex = (_writeIndex + 1) % _shotIds.Length;
    }

    // 모든 발동 기록과 쓰기 위치 초기화
    public void Clear()
    {
        for (int i = 0; i < _shotIds.Length; i++)
        {
            _shotIds[i] = 0;
        }

        _writeIndex = 0;
    }
}
