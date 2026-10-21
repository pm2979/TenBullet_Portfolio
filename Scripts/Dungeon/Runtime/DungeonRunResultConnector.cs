using System;

/// <summary>
/// DungeonSession 종료 결과를 한 번만 소비해 Gameplay 입력 차단, Progression 반영, Dungeon 정리를
/// 고정된 순서로 수행한다.
/// </summary>
public sealed class DungeonRunResultConnector : IDisposable
{
    private readonly DungeonSession _session;
    private readonly DungeonRunResultHandler _resultHandler;
    private readonly WantedPosterSO _playedPoster;
    private readonly int _boardRefreshSeed;
    private readonly Action _blockGameplayInput;
    private readonly Action _disposeDungeon;
    private bool _disposed;

    // Session 종료 소비에 필요한 의존성을 결합하고 구독을 시작한다. resultHandler가 없으면
    // Progression 반영 없이 입력 차단과 Dispose만 수행한다.
    public DungeonRunResultConnector(
        DungeonSession session,
        DungeonRunResultHandler resultHandler,
        WantedPosterSO playedPoster,
        int boardRefreshSeed,
        Action blockGameplayInput,
        Action disposeDungeon)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _resultHandler = resultHandler;
        _playedPoster = playedPoster;
        _boardRefreshSeed = boardRefreshSeed;
        _blockGameplayInput = blockGameplayInput ?? throw new ArgumentNullException(nameof(blockGameplayInput));
        _disposeDungeon = disposeDungeon ?? throw new ArgumentNullException(nameof(disposeDungeon));

        _session.Ended += HandleEnded;
    }

    // Session 구독을 해제한다. 반복 호출에 안전하다.
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.Ended -= HandleEnded;
    }

    private void HandleEnded(DungeonRunResult result)
    {
        _blockGameplayInput();
        _resultHandler?.Apply(result, _playedPoster, _boardRefreshSeed);
        _disposeDungeon();
    }
}
