using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UpgradeCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI 참조")]
    public Image iconImage;
    public Image cardBackground;
    public Image topBorderLine;
    public Image unlockBadge;
    public TMP_Text unlockBadgeText;
    public TMP_Text nameText;
    public TMP_Text descText;
    public TMP_Text badgeText;
    public Button button;

    [Header("컬러 테마")]
    public Color colorAtk       = new Color(0.91f, 0.36f, 0.36f);
    public Color colorRange     = new Color(0.30f, 0.73f, 0.67f);
    public Color colorSpecial   = new Color(0.78f, 0.49f, 1.00f);
    public Color cardNormalBg   = new Color(0.08f, 0.09f, 0.13f);
    public Color cardHoverBg    = new Color(0.11f, 0.13f, 0.19f);
    public Color cardSelectedBg = new Color(0.11f, 0.13f, 0.19f);

    [Header("애니메이션")]
    public float hoverRiseY    = 8f;
    public float selectScaleUp = 1.04f;
    public float animSpeed     = 8f;

    UpgradeData _data;
    Action<UpgradeData> _onSelected;
    bool _isSelected;
    bool _isDisabled;
    bool _basePosInitialized = false; // ← 추가
    Vector3 _basePos;
    Vector3 _targetPos;
    Vector3 _targetScale;
    RectTransform _rt;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _targetScale = Vector3.one;
        button.onClick.AddListener(OnClick);
    }

    // ── basePos를 Setup 시점에 다시 잡아줌 ──────────────────────
    public void Setup(UpgradeData data, Action<UpgradeData> callback)
    {
        _data       = data;
        _onSelected = callback;
        _isSelected = false;
        _isDisabled = false;

        // 레이아웃이 완성된 뒤 Setup이 호출되므로 여기서 위치 저장
        _basePos             = _rt.anchoredPosition3D;
        _targetPos           = _basePos;
        _targetScale         = Vector3.one;
        _basePosInitialized  = true;

        nameText.text  = data.upgradeName;
        descText.text  = data.description;
        badgeText.text = data.badgeText;
        iconImage.sprite = data.icon;

        Color accent = GetAccentColor(data.type);
        topBorderLine.color = accent;
        badgeText.color     = accent;
        topBorderLine.gameObject.SetActive(false);

        if (unlockBadge != null)
        {
            unlockBadge.gameObject.SetActive(data.isSpecial);
            if (data.isSpecial && unlockBadgeText != null)
                unlockBadgeText.text = "해금!";
        }

        cardBackground.color    = cardNormalBg;
        button.interactable     = true;

        var group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1f;

        // 스케일/위치 즉시 리셋
        transform.localScale       = Vector3.one;
        _rt.anchoredPosition3D     = _basePos;
    }

    void Update()
    {
        if (!_basePosInitialized) return; // 초기화 전엔 Lerp 안 함

        _rt.anchoredPosition3D = Vector3.Lerp(
            _rt.anchoredPosition3D, _targetPos, Time.unscaledDeltaTime * animSpeed);
        transform.localScale = Vector3.Lerp(
            transform.localScale, _targetScale, Time.unscaledDeltaTime * animSpeed);
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        topBorderLine.gameObject.SetActive(selected);
        cardBackground.color = selected ? cardSelectedBg : cardNormalBg;
        _targetScale = selected ? Vector3.one * selectScaleUp : Vector3.one;
        _targetPos   = selected
            ? _basePos + new Vector3(0, hoverRiseY, 0)
            : _basePos;
    }

    public void SetDisabled(bool disabled)
    {
        _isDisabled = disabled;
        button.interactable = !disabled;
        var group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = disabled ? 0.35f : 1f;
        if (disabled) { _targetPos = _basePos; _targetScale = Vector3.one; }
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_isDisabled || _isSelected) return;
        cardBackground.color = cardHoverBg;
        topBorderLine.gameObject.SetActive(true);
        _targetPos = _basePos + new Vector3(0, hoverRiseY * 0.6f, 0);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_isDisabled || _isSelected) return;
        cardBackground.color = cardNormalBg;
        topBorderLine.gameObject.SetActive(false);
        _targetPos = _basePos;
    }

    public void OnPointerClick(PointerEventData e) => OnClick();

    void OnClick()
    {
        if (_isDisabled || _data == null) return;
        _onSelected?.Invoke(_data);
    }

    Color GetAccentColor(UpgradeType t) => t switch
    {
        UpgradeType.AttackPower    => colorAtk,
        UpgradeType.AttackRange    => colorRange,
        UpgradeType.PromoteToQueen => colorSpecial,
        UpgradeType.FreeFaceMove   => colorRange,
        _ => Color.white
    };
}
