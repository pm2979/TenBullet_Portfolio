using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
// 플레이어 체력 변화를 탄약 아이콘 목록으로 표시하는 UI
public class PlayerHealthUI : MonoBehaviour
{
    // 현재 화면에 생성되어 있는 탄약 아이콘 목록
    private List<GameObject> playerHealthList = new List<GameObject>();

    private Health health;

    private Health subscribedHealth;

    public void Initialize(Player player)
    {
        Unsubscribe();

        health = player != null ? player.health : null;

        // 현재 체력으로 즉시 UI를 갱신 (Changed 이벤트를 기다리지 않는다)
        if (health != null)
        {
            SetHealthBar(health.GetCurrentHealth());
        }

        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (!isActiveAndEnabled || health == null)
        {
            return;
        }

        if (subscribedHealth == health)
        {
            return;
        }

        Unsubscribe();

        subscribedHealth = health;
        subscribedHealth.Changed += Health_Changed;
    }

    private void Unsubscribe()
    {
        if (subscribedHealth != null)
        {
            subscribedHealth.Changed -= Health_Changed;
        }

        subscribedHealth = null;
    }

    // 체력 변경 이벤트 발생 시 탄약 아이콘을 갱신한다
    private void Health_Changed(HealthChange healthChange)
    {
        SetHealthBar(healthChange.CurrentHealth);
    }

    // 기존에 생성된 탄약 아이콘을 모두 제거한다
    private void ClearHealthBar()
    {
        foreach (GameObject ammoIcon in playerHealthList)
        {
            Destroy(ammoIcon);
        }

        playerHealthList.Clear();
    }

    // 현재 체력만큼 탄약 아이콘을 다시 생성해 배치한다
    private void SetHealthBar(int currentHealth)
    {
        ClearHealthBar();

        // 체력 1당 탄약 1개
        int healthAmmos = Mathf.Max(0, currentHealth);

        for (int i = 0; i < healthAmmos; i++)
        {
            GameObject Ammo = Instantiate(DatabaseResource.Instance.Database.playerHealthIconPrefab, transform);

            // 탄약 간격만큼 가로로 배치
            Ammo.GetComponent<RectTransform>().anchoredPosition = new Vector2(MiscSettings.uiAmmoIconSpacing * i, 0f);

            playerHealthList.Add(Ammo);
        }

    }
}
