using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using TMPro;

/// <summary>
/// 모든 UI Text에 부착되어 언어 변경 이벤트를 수신하는 컴포넌트
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedText : MonoBehaviour
{
    // 인스펙터에서 할당하되 캡슐화 유지
    [SerializeField] private LocalizationTable _table;
    [SerializeField] private string _key;

    private LocalizeStringEvent _stringEvent;
    private TextMeshProUGUI _textComponent;

    private void Awake()
    {
        _textComponent = GetComponent<TextMeshProUGUI>();
        _stringEvent = GetComponent<LocalizeStringEvent>();
    }

    private void OnEnable()
    {
        
        // 활성화될 때 현재 언어에 맞게 즉시 갱신
        SetReference(_table, _key);
    }

    /// <summary>
    /// 인스펙터의 String Reference에 설정된 값을 수동으로 갱신하거나 동적 할당
    /// </summary>
    public void SetReference(LocalizationTable table, string key)
    {
        // 단순하고 명확한 로직
        _stringEvent.StringReference.SetReference(table.ToString(), key);
    }

    private void UpdateText()
    {
        // 단순하고 명확한 로직 유지
        if (_textComponent == null) return;
        
        // 캐싱된 테이블 이름을 사용하여 텍스트 갱신
        _textComponent.text = _table.GetText(_key);
    }

    private void OnDisable()
    {
    }
}
