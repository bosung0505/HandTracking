using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Assets/Scripts/PlayerHpUI.cs
// 플레이어 기물 프리팹의 자식으로 World Space Canvas를 만들고 붙이세요.
// (아래 "설치 방법" 참고)
//
// ── 설치 방법 ────────────────────────────────────────────────────
// 1. playerPiecePrefab 열기
// 2. 자식 오브젝트 추가: Create Empty → 이름 "HpUI"
// 3. HpUI에 Add Component → Canvas
//    - Render Mode: World Space
//    - 크기: Width 1 / Height 0.5 (작게)
// 4. HpUI 자식에 UI > Image → 이름 "HpBarBG" (배경)
// 5. HpBarBG 자식에 UI > Image → 이름 "HpBarFill" (채워지는 바)
//    - Image Type: Filled / Fill Method: Horizontal
// 6. HpUI 자식에 UI > Text-TMP → 이름 "HpText"
// 7. HpUI에 PlayerHpUI.cs 붙이고 각 칸 연결
// ────────────────────────────────────────────────────────────────

public class PlayerHpUI : MonoBehaviour
{
    [Header("UI 참조")]
    public Image hpBarFill;     // Fill 타입 Image
    public TMP_Text hpText;     // "3/3" 텍스트

    [Header("색상")]
    public Color colorFull    = new Color(0.3f, 0.9f, 0.4f);  // 초록
    public Color colorMid     = new Color(0.95f, 0.8f, 0.1f); // 노랑
    public Color colorLow     = new Color(0.9f, 0.2f, 0.2f);  // 빨강

    [Header("항상 카메라 방향으로")]
    public bool faceCameraAlways = true;

    void Start()
    {
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.onHpChanged.AddListener(Refresh);
            Refresh();
        }
    }

    void LateUpdate()
    {
        // HP 바가 항상 카메라를 바라보게
        if (faceCameraAlways && Camera.main != null)
            transform.LookAt(transform.position + Camera.main.transform.forward);
    }

    void OnDestroy()
    {
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.onHpChanged.RemoveListener(Refresh);
    }

    public void Refresh()
    {
        if (PlayerStats.Instance == null) return;

        int cur = PlayerStats.Instance.currentHp;
        int max = PlayerStats.Instance.maxHp;
        float ratio = (float)cur / max;

        if (hpBarFill != null)
        {
            hpBarFill.fillAmount = ratio;
            hpBarFill.color = ratio > 0.6f ? colorFull
                            : ratio > 0.3f ? colorMid
                            : colorLow;
        }

        if (hpText != null)
            hpText.text = $"{cur}/{max}";
    }
}
