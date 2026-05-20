using UnityEngine;

// Assets/Scripts/EnemyData.cs
// 마우스 우클릭 > Create > Game > EnemyData 로 생성

public enum EnemyType
{
    Normal, // 일반 적
    Boss    // 보스 (3, 6, 9스테이지)
}

[CreateAssetMenu(fileName = "EnemyData", menuName = "Game/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("기본 정보")]
    public string enemyName;
    public EnemyType enemyType;

    [Header("스탯")]
    public int maxHp;
    public int attackDamage; // 플레이어에게 주는 피해 (추후 플레이어 HP 구현 시 사용)
}
