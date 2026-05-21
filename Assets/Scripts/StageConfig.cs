using UnityEngine;

[System.Serializable]
public class StageConfig
{
    [Header("스테이지 번호")]
    public int stageNumber;

    [Header("일반 적")]
    public int normalEnemyCount;
    public int normalEnemyHp;
    public int normalEnemyAtk;
    public int normalEnemyAtkRange = 1;

    [Header("보스")]
    public bool isBossStage;
    public int bossHp;
    public int bossAtk;
    public int bossAtkRange = 2;
}
