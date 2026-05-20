# 스테이지 클리어 업그레이드 UI — 유니티 설치 가이드

## 폴더 구조 (유니티 Assets 안에 복사)

```
Assets/
├── Scripts/
│   ├── UpgradeData.cs          (ScriptableObject 정의)
│   ├── PlayerStats.cs          (스탯 + 선택 횟수 관리)
│   ├── UpgradeCardUI.cs        (개별 카드 컴포넌트)
│   ├── UpgradePanelUI.cs       (패널 전체 컨트롤러)
│   └── GameManager.cs          (스테이지 흐름)
│
└── UI/
    └── Sprites/
        ├── icon_attack_power.png
        ├── icon_attack_range.png
        ├── icon_queen_promote.png
        ├── icon_free_move.png
        ├── icon_rook.png
        ├── icon_queen.png
        ├── card_normal.png
        ├── card_selected_atk.png
        ├── card_selected_rng.png
        ├── card_special_queen.png
        ├── card_special_move.png
        ├── panel_bg.png
        ├── overlay_dark.png
        ├── pip_empty.png / pip_atk.png / pip_rng.png
        ├── btn_normal.png / btn_active.png / btn_hover.png
        └── badge_unlock.png
```

---

## 1단계 — 스프라이트 임포트 설정

모든 PNG 선택 → Inspector:
- Texture Type: **Sprite (2D and UI)**
- Pixels Per Unit: **100**
- Filter Mode: **Bilinear**
- `panel_bg`, `card_*`, `overlay_dark` → Mesh Type: **Full Rect**

---

## 2단계 — ScriptableObject 4개 생성

`Assets/UI/Data/` 폴더 안에서:
마우스 우클릭 > **Create > Game > UpgradeData**

| 파일명                  | upgradeName       | type              | badgeText    | icon                    | isSpecial |
|------------------------|-------------------|-------------------|--------------|-------------------------|-----------|
| SO_AtkPower.asset      | 공격력 강화        | AttackPower       | ATK +1       | icon_attack_power       | false     |
| SO_AtkRange.asset      | 공격 범위 확장     | AttackRange       | RANGE +1     | icon_attack_range       | false     |
| SO_QueenPromotion.asset | 여왕으로 승급      | PromoteToQueen    | PROMOTION    | icon_queen_promote      | true      |
| SO_FreeFaceMove.asset  | 차원 이동 해방     | FreeFaceMove      | UNLOCK       | icon_free_move          | true      |

---

## 3단계 — Hierarchy 구성

```
[Scene]
├── GameManager (빈 오브젝트)
│   ├── GameManager.cs
│   └── PlayerStats.cs
│
└── Canvas (Render Mode: Screen Space - Overlay)
    └── UpgradePanel (빈 오브젝트)
        ├── CanvasGroup 컴포넌트 추가
        ├── UpgradePanelUI.cs 추가
        │
        ├── Overlay (Image, overlay_dark.png, 전체화면, Raycast Target ON)
        ├── PanelBG (Image, panel_bg.png, 앵커 Center, 사이즈 900×680)
        │   ├── StageBadgeText (TMP)
        │   ├── TitleText (TMP "선택의 기로")
        │   ├── SubtitleText (TMP "하나의 힘을 택하라")
        │   │
        │   ├── StatRow (HorizontalLayoutGroup)
        │   │   ├── PiecePill (Image + TMP "룩/퀸")
        │   │   ├── AtkPill   (Image + TMP 공격력값)
        │   │   └── RngPill   (Image + TMP 범위값)
        │   │
        │   ├── PipRow (HorizontalLayoutGroup)
        │   │   ├── Label "공격력"
        │   │   ├── AtkPip0~2 (Image × 3, pip_empty.png)
        │   │   ├── Label "범위"
        │   │   └── RngPip0~2 (Image × 3, pip_empty.png)
        │   │
        │   ├── ChooseLabel (TMP "하나를 선택하세요")
        │   │
        │   ├── CardContainer (HorizontalLayoutGroup, spacing 20)
        │   │   ├── CardA (Image card_normal.png + UpgradeCardUI.cs)
        │   │   │   ├── TopBorderLine (Image, 높이 3px)
        │   │   │   ├── UnlockBadge   (Image badge_unlock.png + TMP)
        │   │   │   ├── IconBG        (Image)
        │   │   │   │   └── Icon      (Image)
        │   │   │   ├── NameText      (TMP)
        │   │   │   ├── DescText      (TMP)
        │   │   │   ├── BadgeText     (TMP)
        │   │   │   └── Button        (Button 컴포넌트)
        │   │   └── CardB (CardA와 동일 구조)
        │   │
        │   ├── ResultText (TMP, 중앙 하단)
        │   ├── Divider (Image 가로선)
        │   ├── ConfirmButton (Image btn_normal.png + Button + TMP "다음 스테이지")
        │   └── HintText (TMP, 작은 이탤릭)
```

---

## 4단계 — Inspector 연결

**UpgradePanelUI** 컴포넌트:
- Panel Group → UpgradePanel의 CanvasGroup
- Stage Badge Text → StageBadgeText
- Hint Text → HintText
- Piece Label / Atk Value Text / Rng Value Text → 각 TMP
- Atk Pips (3) / Rng Pips (3) → 각 pip Image
- Card A / Card B → 각 CardA/B의 UpgradeCardUI
- So Atk Power ~ So Free Face Move → 위에서 만든 SO 4개
- Result Text → ResultText
- Confirm Button → ConfirmButton

**UpgradeCardUI** (CardA, CardB 각각):
- Icon Image, Card Background, Top Border Line, Unlock Badge, Name/Desc/Badge Text, Button → 각 자식 오브젝트 연결

---

## 5단계 — 테스트

Play 모드에서 **Space 키** → 스테이지 클리어 UI 표시
카드 클릭 → 선택 → "다음 스테이지" 버튼 → 적용 후 자동 닫힘

---

## 게임 로직 연동 (실제 스테이지 클리어 시)

```csharp
// 적을 모두 처치했을 때 또는 스테이지 조건 충족 시:
GameManager.Instance.StageClear();
```

PlayerStats.Instance에서 언제든 현재 스탯 조회 가능:
```csharp
int atk   = PlayerStats.Instance.attackPower;
int range = PlayerStats.Instance.attackRange;
bool isQueen    = PlayerStats.Instance.isQueen;
bool freeMove   = PlayerStats.Instance.isFreeFaceMove;
```
