using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 타이틀 진입 연출: 화면 페이드 인 후 지연 시간 뒤 타이틀 로고 페이드 인
/// 총알 낙하 루프는 TitleBulletRain이 독립 담당
/// </summary>
public class TitleIntroView : MonoBehaviour
{
    [Header("References")]
    [Tooltip("총알 연출을 덮는 검은 화면. 메뉴 패널보다 뒤에 배치")]
    [SerializeField] private CanvasGroup _screenFade;
    [SerializeField] private CanvasGroup _titleLogo;

    [Header("Timing")]
    [Tooltip("검은 화면이 걷히는 시간 (초)")]
    [SerializeField, Min(0f)] private float _screenFadeDuration = 1.5f;
    [Tooltip("게임 시작 후 타이틀 로고 등장까지 대기 시간 (초)")]
    [SerializeField, Min(0f)] private float _titleDelay = 2f;
    [Tooltip("타이틀 로고 페이드 인 시간 (초)")]
    [SerializeField, Min(0f)] private float _titleFadeDuration = 1f;

    private void Awake()
    {
        // 첫 프레임 노출 방지를 위한 시작값 고정
        _screenFade.alpha = 1f;
        _screenFade.blocksRaycasts = false;
        _titleLogo.alpha = 0f;
        _titleLogo.blocksRaycasts = false;
    }

    private IEnumerator Start()
    {
        // 씬 로드 직후 프레임 지연(스파이크)에 페이드가 생략되지 않도록 안정 프레임 이후 시작
        yield return null;
        yield return null;

        _screenFade.DOFade(0f, _screenFadeDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);

        _titleLogo.DOFade(1f, _titleFadeDuration)
            .SetDelay(_titleDelay)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);
    }
}
