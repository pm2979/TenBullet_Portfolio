using System.Collections.Generic;
using UnityEngine.Localization.Settings;

/// <summary>
/// LocalizationTable에 대한 로컬라이징 텍스트 조회 확장 메서드 모음
/// 초기화 여부 확인과 예외 처리를 포함한 안전한 접근 방식 제공
/// </summary>
public static class LocalizeExtensions
{
    // [최적화] ToString()의 가비지 생성을 방지하기 위한 이름 캐시
    private static readonly Dictionary<LocalizationTable, string> _tableNameCache = new Dictionary<LocalizationTable, string>();

    static LocalizeExtensions()
    {
        // Enum 값을 미리 문자열로 캐싱 (성능 최적화 및 GC 방지)
        foreach (LocalizationTable table in System.Enum.GetValues(typeof(LocalizationTable)))
        {
            _tableNameCache[table] = table.ToString();
        }
    }

    /// <summary>
    /// 안전하게 로컬라이징 텍스트를 가져오는 확장 메서드
    /// </summary>
    public static string GetText(this LocalizationTable table, string key)
    {
        // 키 유효성 검사
        if (string.IsNullOrEmpty(key))
        {
            Logger.Log($"Localization Key가 비어 있습니다. Table: {table}");
            return "KEY_EMPTY";
        }

        // 초기화 여부 확인
        if (!LocalizationSettings.InitializationOperation.IsDone)
        {
            Logger.Log("Localization 시스템이 아직 초기화되지 않았습니다.");
            return "SYSTEM_NOT_READY";
        }

        try
        {
            string tableName = _tableNameCache[table];
            string result = LocalizationSettings.StringDatabase.GetLocalizedString(tableName, key);
            
            if (string.IsNullOrEmpty(result))
            {
                Logger.Log($"텍스트를 찾을 수 없습니다. Table: {tableName}, Key: {key}");
                return $"MISSING_{key}";
            }

            return result;
        }
        catch (System.Exception e)
        {
            Logger.Log($"Localization 로드 중 예외 발생: {e.Message}");
            return "ERROR";
        }
    }
}