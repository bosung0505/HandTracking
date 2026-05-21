using UnityEngine;
using UnityEngine.Events;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("기물 상태")]
    public int attackPower = 1;
    public int attackRange = 1;
    public bool isQueen = false;
    public bool isFreeFaceMove = false;

    [Header("HP")]
    public int maxHp = 3;
    public int currentHp = 3;

    [Header("선택 횟수")]
    public int atkPickCount = 0;
    public int rngPickCount = 0;
    public int unlockThreshold = 3;

    [HideInInspector] public UnityEvent onStatsChanged = new UnityEvent();
    [HideInInspector] public UnityEvent onHpChanged    = new UnityEvent();
    [HideInInspector] public UnityEvent onDead         = new UnityEvent();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void TakeDamage(int damage)
    {
        if (currentHp <= 0) return;
        currentHp = Mathf.Max(currentHp - damage, 0);
        Debug.Log($"[Player] 피격! 데미지:{damage} 남은HP:{currentHp}/{maxHp}");
        onHpChanged?.Invoke();
        if (currentHp <= 0) onDead?.Invoke();
    }

    public void FullHeal()
    {
        currentHp = maxHp;
        onHpChanged?.Invoke();
    }

    public void ApplyUpgrade(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.AttackPower:    attackPower++;  atkPickCount++; break;
            case UpgradeType.AttackRange:    attackRange++;  rngPickCount++; break;
            case UpgradeType.PromoteToQueen: isQueen = true; atkPickCount=0; break;
            case UpgradeType.FreeFaceMove:   isFreeFaceMove=true; rngPickCount=0; break;
        }
        onStatsChanged?.Invoke();
    }

    public UpgradeType GetCardA() =>
        (atkPickCount >= unlockThreshold && !isQueen) ? UpgradeType.PromoteToQueen : UpgradeType.AttackPower;
    public UpgradeType GetCardB() =>
        (rngPickCount >= unlockThreshold && !isFreeFaceMove) ? UpgradeType.FreeFaceMove : UpgradeType.AttackRange;

    public int AtkProgress => isQueen        ? 0 : Mathf.Min(atkPickCount, unlockThreshold);
    public int RngProgress => isFreeFaceMove  ? 0 : Mathf.Min(rngPickCount, unlockThreshold);
}
