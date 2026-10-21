using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 발사 성공에 따른 총구 효과·탄피·사운드·무기 반동 표현을 재생
/// </summary>
[RequireComponent(typeof(ActiveWeapon))]
[DisallowMultipleComponent]
public class WeaponFireEffects : MonoBehaviour
{
    // 후속음이 다음 발사와 겹치지 않게 확보할 최소 여유 시간
    private const float PostFireEndMargin = 0.01f;

    // 후속음을 다음 발사 전에 마칠 수 있는 최대 재생 속도 배율
    private const float MaxPostFirePitchMultiplier = 2f;

    private ActiveWeapon activeWeapon;

    // 플레이어 탄피 착지점을 발 아래쪽으로 보정할 선택 참조
    private Player player;

    private Transform weaponVisualTransform;
    private Vector3 weaponVisualBaseLocalPosition;
    private Tween visualRecoilTween;

    // 다음 발사 또는 비활성화 시 취소할 후속음 예약
    private Coroutine postFireSoundRoutine;

    private void Awake()
    {
        activeWeapon = GetComponent<ActiveWeapon>();
        player = GetComponent<Player>();

        weaponVisualTransform = activeWeapon.GetWeaponVisualTransform();
        if (weaponVisualTransform != null)
        {
            weaponVisualBaseLocalPosition = weaponVisualTransform.localPosition;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        postFireSoundRoutine = null;

        visualRecoilTween?.Kill();
        visualRecoilTween = null;

        if (weaponVisualTransform != null)
        {
            weaponVisualTransform.localPosition = weaponVisualBaseLocalPosition;
        }
    }

    /// <summary>
    /// 무기 전용 Transform을 발사 반대 방향으로 후퇴시킨 뒤 기준 위치로 복귀
    /// </summary>
    public void PlayVisualRecoil(Weapon weapon)
    {
        if (weaponVisualTransform == null)
        {
            return;
        }

        float recoilDistance = weapon.weaponDetails.weaponVisualRecoilDistance;
        float recoveryTime = weapon.weaponDetails.weaponVisualRecoilRecoveryTime;

        if (recoilDistance <= 0f || recoveryTime <= 0f)
        {
            return;
        }

        visualRecoilTween?.Kill();

        weaponVisualTransform.localPosition = weaponVisualBaseLocalPosition + new Vector3(-recoilDistance, 0f, 0f);
        visualRecoilTween = weaponVisualTransform.DOLocalMove(weaponVisualBaseLocalPosition, recoveryTime)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);
    }

    /// <summary>
    /// 설정된 총구 스프라이트 효과를 풀에서 대여해 재생하고 성공 여부 반환
    /// </summary>
    public bool TryPlayMuzzleSpriteEffect(Weapon weapon)
    {
        EffectDefinitionSO weaponShootSpriteEffect = weapon.weaponDetails.weaponShootSpriteEffect;

        if (weaponShootSpriteEffect == null || weaponShootSpriteEffect.effectViewPrefab == null)
        {
            return false;
        }

        if (!PoolManager.HasInstance)
        {
            return false;
        }

        Component rentedComponent = PoolManager.Instance.RentComponent(weaponShootSpriteEffect.effectViewPrefab, activeWeapon.GetShootEffectPosition(), Quaternion.identity);

        EffectView effectView = rentedComponent as EffectView;

        if (effectView == null)
        {
            PoolManager.Instance.ReturnComponent(rentedComponent);
            return false;
        }

        // 재생 중에도 무기 조준 방향을 따르도록 효과 위치를 추적 대상으로 전달
        Transform effectPositionTransform = activeWeapon.GetWeaponEffectPositionTransform();
        bool flipY = effectPositionTransform != null && effectPositionTransform.lossyScale.y < 0f;

        effectView.Play(weaponShootSpriteEffect, false, flipY, effectPositionTransform);

        return true;
    }

    /// <summary>
    /// 설정된 탄피를 배출하고 플레이어면 발 아래쪽 착지점으로 궤적 보정
    /// </summary>
    public void TrySpawnBulletShell(Weapon weapon, float aimAngle)
    {
        BulletShellEffectSO bulletShellEffect = weapon.weaponDetails.weaponBulletShellEffect;

        if (bulletShellEffect == null || bulletShellEffect.bulletShellViewPrefab == null)
        {
            return;
        }

        if (!PoolManager.HasInstance)
        {
            return;
        }

        Vector2 spawnPosition = activeWeapon.GetShellEjectPosition();

        Component rentedComponent = PoolManager.Instance.RentComponent(bulletShellEffect.bulletShellViewPrefab, spawnPosition, Quaternion.identity);

        BulletShellView bulletShellView = rentedComponent as BulletShellView;

        if (bulletShellView == null)
        {
            PoolManager.Instance.ReturnComponent(rentedComponent);
            return;
        }

        float ejectAngle = aimAngle + bulletShellEffect.ejectAngleOffsetDegrees
            + Random.Range(-bulletShellEffect.ejectAngleRandomSpreadDegrees, bulletShellEffect.ejectAngleRandomSpreadDegrees);
        Vector2 ejectDirection = DirectionUtility2D.DirectionFromDegrees(ejectAngle);

        float ejectSpeed = Random.Range(bulletShellEffect.ejectSpeedMin, bulletShellEffect.ejectSpeedMax);
        Vector2 groundVelocity = ejectDirection * ejectSpeed;

        float initialHeightSpeed = Random.Range(bulletShellEffect.initialHeightSpeedMin, bulletShellEffect.initialHeightSpeedMax);

        // 수직 높이가 0으로 돌아오는 시점의 수평 위치를 착지점으로 계산
        float timeToLand = bulletShellEffect.gravity > 0f ? 2f * initialHeightSpeed / bulletShellEffect.gravity : 0f;
        Vector2 landingPosition = spawnPosition + groundVelocity * timeToLand;

        if (player != null && timeToLand > 0f)
        {
            // 플레이어 발 위치 아래쪽 반원에서 기존 비행 거리 이내의 착지점 선택
            float landingRadius = ejectSpeed * timeToLand;
            Vector2 landingOffset = Random.insideUnitCircle * landingRadius;
            landingOffset.y = -Mathf.Abs(landingOffset.y);

            landingPosition = (Vector2)player.transform.position + landingOffset;
            groundVelocity = (landingPosition - spawnPosition) / timeToLand;
        }

        bool clockwise = Random.value < 0.5f;

        bulletShellView.Launch(
            spawnPosition, groundVelocity, bulletShellEffect.gravity, initialHeightSpeed, landingPosition,
            bulletShellEffect.rotationStepDegrees, bulletShellEffect.rotationStepInterval, clockwise, bulletShellEffect.lifetime,
            bulletShellEffect.spawnPopHeight, bulletShellEffect.spawnPopDuration, bulletShellEffect.landingSound);
    }

    /// <summary>
    /// 발사 성공음을 재생하며 설정이나 오디오 관리자가 없으면 생략
    /// </summary>
    public void PlaySound(Weapon weapon)
    {
        SoundDataSO fireSound = weapon?.weaponDetails?.weaponFireSound;
        TryPlaySound(fireSound);
    }

    /// <summary>
    /// 발사 성공음과 다음 발사 전 종료 가능한 후속음을 예약 재생
    /// </summary>
    public void PlaySound(Weapon weapon, float finalFireInterval)
    {
        PlaySound(weapon);

        // 이전 예약을 취소해 최신 발사 간격 기준으로 후속음 재계산
        if (postFireSoundRoutine != null)
        {
            StopCoroutine(postFireSoundRoutine);
            postFireSoundRoutine = null;
        }

        if (weapon?.weaponDetails == null || weapon.weaponDetails.weaponPostFireSound == null)
        {
            return;
        }

        SoundDataSO postFireSound = weapon.weaponDetails.weaponPostFireSound;
        float timing = Mathf.Clamp(weapon.weaponDetails.weaponPostFireSoundTiming, 0f, 0.99f);

        if (!TryResolvePostFireSchedule(postFireSound, finalFireInterval, timing, out float delay, out float pitchMultiplier))
        {
            return;
        }

        postFireSoundRoutine = StartCoroutine(PlayPostFireSoundAfterDelay(postFireSound, delay, pitchMultiplier));
    }

    // 유효한 사운드를 지정된 재생 속도로 요청
    private static void TryPlaySound(SoundDataSO sound, float pitchMultiplier = 1f)
    {
        if (sound == null || !AudioManager.HasInstance)
        {
            return;
        }

        AudioManager.Instance.PlaySfx(sound, null, pitchMultiplier);
    }

    // 예약 시간이 지난 뒤 후속음을 재생하고 예약 상태 해제
    private IEnumerator PlayPostFireSoundAfterDelay(SoundDataSO sound, float delay, float pitchMultiplier)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        TryPlaySound(sound, pitchMultiplier);
        postFireSoundRoutine = null;
    }

    // 가장 긴 재생 시간 기준으로 다음 발사 전 종료 가능한 지연과 속도 계산
    private static bool TryResolvePostFireSchedule(
        SoundDataSO sound,
        float fireInterval,
        float normalizedTiming,
        out float delay,
        out float pitchMultiplier)
    {
        delay = 0f;
        pitchMultiplier = 1f;

        if (sound == null || !float.IsFinite(fireInterval) || fireInterval <= PostFireEndMargin)
        {
            return false;
        }

        float minimumPitch = sound.PitchRange.x;
        if (!float.IsFinite(minimumPitch) || minimumPitch <= 0f || sound.Clips == null || sound.Clips.Count == 0)
        {
            return false;
        }

        float maximumPlaybackDuration = 0f;
        for (int i = 0; i < sound.Clips.Count; i++)
        {
            AudioClip clip = sound.Clips[i];
            if (clip == null)
            {
                return false;
            }

            maximumPlaybackDuration = Mathf.Max(maximumPlaybackDuration, clip.length / minimumPitch);
        }

        float latestStartTime = fireInterval - maximumPlaybackDuration - PostFireEndMargin;
        if (latestStartTime >= 0f)
        {
            // 기본 속도로 끝낼 수 있으면 선호 시점보다 늦지 않게 시작
            float preferredStartTime = fireInterval * Mathf.Clamp01(normalizedTiming);
            delay = Mathf.Min(preferredStartTime, latestStartTime);
            return true;
        }

        // 즉시 시작해도 부족한 경우에만 허용 범위 안에서 재생 속도 증가
        float availableDuration = fireInterval - PostFireEndMargin;
        float requiredPitchMultiplier = maximumPlaybackDuration / availableDuration;

        if (!float.IsFinite(requiredPitchMultiplier) || requiredPitchMultiplier > MaxPostFirePitchMultiplier)
        {
            return false;
        }

        pitchMultiplier = requiredPitchMultiplier;
        return true;
    }
}
