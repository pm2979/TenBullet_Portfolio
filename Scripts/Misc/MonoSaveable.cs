using UnityEngine;

/// <summary>
/// MonoBehaviour를 상속받으며 세이브 시스템에 자동 등록되는 베이스 클래스
/// </summary>
public abstract class MonoSaveable : MonoBehaviour, ISaveable
{
    protected virtual void Awake()
    {
    }

    protected virtual void Start()
    {
        var saveManager = SaveManager.Instance;
        saveManager?.RegisterSaveable(this);

        var currentData = saveManager?.GetGameData();
        if (currentData != null)
        {
            LoadData(currentData);
        }
    }

    protected virtual void OnDestroy()
    {
        // 메모리 누수 방지를 위한 등록 해제
        if (SaveManager.HasInstance)
        {
            SaveManager.Instance.UnregisterSaveable(this);
        }
    }

    public abstract void LoadData(GameData data);
    public abstract void SaveData(ref GameData data);
}
