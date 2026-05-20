using UnityEngine;
using DG.Tweening;
using TMPro;

public class EnemyController : MonoBehaviour
{
    [Header("적 데이터")]
    public EnemyData data;

    [Header("HP UI (선택사항)")]
    public TMP_Text hpText;
    public UnityEngine.UI.Image hpBarFill;

    public int currentHp   { get; private set; }
    public bool isDead     { get; private set; }
    [HideInInspector] public int attackRange = 1; // SpawnManager에서 주입

    void Start()
    {
        if (data != null) Initialize(data);
    }

    public void Initialize(EnemyData enemyData, int atkRange = 1)
    {
        data        = enemyData;
        currentHp   = data.maxHp;
        isDead      = false;
        attackRange = atkRange;
        RefreshHpUI();
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        currentHp = Mathf.Max(currentHp - damage, 0);
        RefreshHpUI();
        transform.DOKill();
        transform.DOShakePosition(0.3f, 0.15f, 10, 90, false, true);
        if (currentHp <= 0) Die();
    }

    private void Die()
    {
        isDead = true;
        transform.DOKill();
        transform.DOScale(Vector3.zero, 0.4f)
            .SetEase(Ease.InBack)
            .OnComplete(() => Destroy(gameObject));
    }

    private void RefreshHpUI()
    {
        if (hpText != null)
            hpText.text = $"{currentHp}/{(data != null ? data.maxHp : 0)}";
        if (hpBarFill != null && data != null)
            hpBarFill.fillAmount = (float)currentHp / data.maxHp;
    }
}
