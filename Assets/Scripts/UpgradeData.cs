using UnityEngine;

// Assets/Scripts/UpgradeData.cs
// 마우스 우클릭 > Create > Game/UpgradeData 로 생성

public enum UpgradeType
{
    AttackPower,    // 공격력 +1
    AttackRange,    // 공격 범위 +1
    PromoteToQueen, // 기물: 룩 -> 퀸 (공격력 3회 선택 시 해금)
    FreeFaceMove    // 면 이동 제한 해제 (범위 3회 선택 시 해금)
}

[CreateAssetMenu(fileName = "UpgradeData", menuName = "Game/UpgradeData")]
public class UpgradeData : ScriptableObject
{
    [Header("기본 정보")]
    public string upgradeName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;
    public UpgradeType type;

    [Header("뱃지 텍스트 (카드 하단)")]
    public string badgeText;

    [Header("특별 업그레이드 여부")]
    public bool isSpecial; // 해금형 업그레이드인지 표시 (카드 테두리 강조용)
}
