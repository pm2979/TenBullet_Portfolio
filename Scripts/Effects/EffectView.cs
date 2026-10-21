using System.Collections;
using UnityEngine;

/// <summary>풀에서 대여한 플립북 이펙트의 초기화·프레임 재생·대상 추적·반환을 관리</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class EffectView : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Coroutine playRoutine;
    private Transform followTarget;
    private bool hasFinished;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (followTarget != null)
        {
            transform.position = followTarget.position;
            transform.rotation = followTarget.rotation;
        }
    }

    /// <summary>이전 대여 상태를 덮어쓰고 정의된 표시 설정으로 플립북 재생을 시작</summary>
    public void Play(EffectDefinitionSO effectDefinition, bool flipX = false, bool flipY = false, Transform followTarget = null)
    {
        if (effectDefinition == null)
        {
            Debug.LogError("EffectView: effectDefinition이 null입니다.", this);
            return;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        hasFinished = false;
        this.followTarget = followTarget;

        spriteRenderer.sprite = null;
        spriteRenderer.color = Color.white;
        spriteRenderer.flipX = flipX;
        spriteRenderer.flipY = flipY;
        spriteRenderer.sortingOrder = effectDefinition.sortingOrder;
        transform.localScale = effectDefinition.displayScale;

        if (followTarget != null)
        {
            transform.position = followTarget.position;
            transform.rotation = followTarget.rotation;
        }

        gameObject.SetActive(true);

        playRoutine = StartCoroutine(PlayRoutine(effectDefinition));
    }

    // 정의된 속도로 모든 프레임을 표시한 뒤 재생 완료 처리
    private IEnumerator PlayRoutine(EffectDefinitionSO effectDefinition)
    {
        WaitForSeconds frameWait = new WaitForSeconds(1f / effectDefinition.frameRate);

        for (int i = 0; i < effectDefinition.frames.Length; i++)
        {
            spriteRenderer.sprite = effectDefinition.frames[i];

            yield return frameWait;
        }

        FinishPlaying();
    }

    // 재생 참조를 정리하고 PoolManager 소유 객체는 반환하며 비소유 객체는 비활성화
    private void FinishPlaying()
    {
        if (hasFinished)
        {
            return;
        }

        hasFinished = true;
        playRoutine = null;
        followTarget = null;
        spriteRenderer.sprite = null;

        if (PoolManager.HasInstance && PoolManager.Instance.ReturnComponent(this))
        {
            return;
        }

        gameObject.SetActive(false);
    }
}
