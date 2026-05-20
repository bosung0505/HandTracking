using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Assets/Scripts/UpgradePanelUI.cs
// UpgradePanel Canvas 루트에 붙이세요.

public class UpgradePanelUI : MonoBehaviour
{
    public static UpgradePanelUI Instance { get; private set; }

    [Header("패널 루트")]
    public CanvasGroup panelGroup;   // 페이드 인/아웃용

    [Header("헤더")]
    public TMP_Text stageBadgeText;  // "Stage 3 Clear"
    public TMP_Text hintText;

    [Header("스탯 표시")]
    public TMP_Text pieceLabel;      // 룩 / 퀸
    public TMP_Text atkValueText;
    public TMP_Text rngValueText;

    [Header("진행 게이지 (pip)")]
    public Image[] atkPips;          // 3개 연결
    public Image[] rngPips;          // 3개 연결
    public Color pipEmpty   = new Color(1,1,1,0.12f);
    public Color pipFilledAtk = new Color(0.91f,0.36f,0.36f);
    public Color pipFilledRng = new Color(0.30f,0.73f,0.67f);

    [Header("카드")]
    public UpgradeCardUI cardA;
    public UpgradeCardUI cardB;

    [Header("ScriptableObject 참조")]
    public UpgradeData soAtkPower;
    public UpgradeData soAtkRange;
    public UpgradeData soQueenPromotion;
    public UpgradeData soFreeFaceMove;

    [Header("결과 메시지")]
    public TMP_Text resultText;

    [Header("확인 버튼")]
    public Button confirmButton;
    public TMP_Text confirmButtonText;

    [Header("페이드 설정")]
    public float fadeSpeed = 4f;

    // ── 내부 ──
    UpgradeData _pendingChoice;
    int _stageNumber = 1;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        panelGroup.alpha          = 0;
        panelGroup.interactable   = false;
        panelGroup.blocksRaycasts = false;
        resultText.alpha          = 0;
        confirmButton.interactable = false;
        confirmButton.onClick.AddListener(OnConfirm);
    }

    // ── Public ──────────────────────────────────────────────────

    /// <summary>스테이지 클리어 후 이 메서드를 호출합니다.</summary>
    public void ShowUpgradePanel(int stageNumber)
    {
        _stageNumber = stageNumber;
        Time.timeScale = 0f;

        RefreshAll();
        StartCoroutine(FadePanel(true));
    }

    // ── 내부 로직 ────────────────────────────────────────────────

    void RefreshAll()
    {
        var stats = PlayerStats.Instance;

        stageBadgeText.text = $"Stage {_stageNumber} Clear";
        pieceLabel.text     = stats.isQueen ? "퀸" : "룩";
        atkValueText.text   = stats.attackPower.ToString();
        rngValueText.text   = stats.attackRange.ToString();

        RefreshPips(atkPips, stats.AtkProgress, pipFilledAtk);
        RefreshPips(rngPips, stats.RngProgress, pipFilledRng);

        // 카드 선택지 결정
        UpgradeData dataA = ResolveData(stats.GetCardA());
        UpgradeData dataB = ResolveData(stats.GetCardB());

        cardA.Setup(dataA, OnCardSelected);
        cardB.Setup(dataB, OnCardSelected);

        _pendingChoice = null;
        confirmButton.interactable = false;

        UpdateHintText(stats);
    }

    void RefreshPips(Image[] pips, int filled, Color filledColor)
    {
        for (int i = 0; i < pips.Length; i++)
            pips[i].color = i < filled ? filledColor : pipEmpty;
    }

    void OnCardSelected(UpgradeData chosen)
    {
        _pendingChoice = chosen;
        bool isA = (chosen == ResolveData(PlayerStats.Instance.GetCardA()));
        cardA.SetSelected(isA);
        cardB.SetSelected(!isA);
        cardA.SetDisabled(!isA);
        cardB.SetDisabled(isA);
        confirmButton.interactable = true;
    }

    void OnConfirm()
    {
        if (_pendingChoice == null) return;
        confirmButton.interactable = false;

        PlayerStats.Instance.ApplyUpgrade(_pendingChoice.type);
        string msg = GetFlavorMessage(_pendingChoice.type);

        StartCoroutine(ShowResultThenClose(msg));
    }

    IEnumerator ShowResultThenClose(string msg)
    {
        resultText.text  = msg;
        resultText.alpha = 0;

        // 메시지 페이드 인
        while (resultText.alpha < 1f)
        {
            resultText.alpha += Time.unscaledDeltaTime * fadeSpeed;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1.8f);

        // 패널 페이드 아웃
        yield return StartCoroutine(FadePanel(false));

        Time.timeScale = 1f;
        resultText.alpha = 0;

        // 다음 스테이지 시작 알림 (필요 시 GameManager 구독)
        GameManager.Instance?.OnUpgradeComplete();
    }

    IEnumerator FadePanel(bool show)
    {
        panelGroup.interactable   = show;
        panelGroup.blocksRaycasts = show;
        float target = show ? 1f : 0f;

        while (!Mathf.Approximately(panelGroup.alpha, target))
        {
            panelGroup.alpha = Mathf.MoveTowards(
                panelGroup.alpha, target, Time.unscaledDeltaTime * fadeSpeed);
            yield return null;
        }
        panelGroup.alpha = target;
    }

    UpgradeData ResolveData(UpgradeType t) => t switch
    {
        UpgradeType.AttackPower    => soAtkPower,
        UpgradeType.AttackRange    => soAtkRange,
        UpgradeType.PromoteToQueen => soQueenPromotion,
        UpgradeType.FreeFaceMove   => soFreeFaceMove,
        _ => soAtkPower
    };

    string GetFlavorMessage(UpgradeType t) => t switch
    {
        UpgradeType.AttackPower    => "검기가 날카로워졌다.",
        UpgradeType.AttackRange    => "시야가 넓어진다.",
        UpgradeType.PromoteToQueen => "룩이 여왕의 힘에 눈을 뜬다.",
        UpgradeType.FreeFaceMove   => "면과 면 사이의 경계가 사라진다.",
        _ => ""
    };

    void UpdateHintText(PlayerStats s)
    {
        if (s.isQueen && s.isFreeFaceMove)
            hintText.text = "모든 특별 업그레이드를 획득했습니다!";
        else if (s.isQueen)
            hintText.text = "범위를 3번 선택하면 차원 이동이 해금됩니다";
        else if (s.isFreeFaceMove)
            hintText.text = "공격력을 3번 선택하면 퀸으로 승급됩니다";
        else
            hintText.text = "공격력 또는 범위를 3번 선택하면 특별 업그레이드가 해금됩니다";
    }
}
