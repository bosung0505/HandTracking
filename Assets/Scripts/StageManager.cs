using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

[System.Serializable]
public class StageEnemyData
{
    [Tooltip("스폰할 적의 프리팹")]
    public GameObject enemyPrefab;
    [Tooltip("스폰할 마릿수")]
    public int count;
}

[System.Serializable]
public class StageData
{
    [Tooltip("이 스테이지에 스폰될 적들의 정보 리스트")]
    public List<StageEnemyData> enemies = new List<StageEnemyData>();
}

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("Stage Settings")]
    public List<StageData> stages = new List<StageData>();

    [Header("UI Settings")]
    [Tooltip("STAGE 텍스트가 들어있는 UI 패널의 CanvasGroup (알파값 조절용)")]
    public CanvasGroup stageUIGroup;
    [Tooltip("현재 스테이지 텍스트를 출력할 TextMeshPro 컴포넌트")]
    public TextMeshProUGUI stageText; 

    [Header("Turn UI Settings")]
    [Tooltip("My Turn 알림 UI 패널의 CanvasGroup")]
    public CanvasGroup myTurnUIGroup;
    [Tooltip("Opponent's Turn 알림 UI 패널의 CanvasGroup")]
    public CanvasGroup opponentTurnUIGroup;

    [Header("Game Over UI Settings")]
    [Tooltip("GAME OVER UI 패널의 CanvasGroup")]
    public CanvasGroup gameOverUIGroup;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 초기에는 UI 투명화 및 비활성화 처리
        if (stageUIGroup != null)
        {
            stageUIGroup.alpha = 0f;
            stageUIGroup.gameObject.SetActive(false);
        }
        if (myTurnUIGroup != null)
        {
            myTurnUIGroup.alpha = 0f;
            myTurnUIGroup.gameObject.SetActive(false);
        }
        if (opponentTurnUIGroup != null)
        {
            opponentTurnUIGroup.alpha = 0f;
            opponentTurnUIGroup.gameObject.SetActive(false);
        }
        if (gameOverUIGroup != null)
        {
            gameOverUIGroup.alpha = 0f;
            gameOverUIGroup.gameObject.SetActive(false);
        }
    }

    public StageData GetStageData(int stageNumber)
    {
        // 배열 인덱스는 0부터 시작하므로 -1
        int index = stageNumber - 1;
        
        if (stages.Count == 0) return null;

        // 준비된 스테이지를 초과하면 마지막 스테이지 정보를 반복해서 사용
        if (index >= stages.Count)
        {
            index = stages.Count - 1;
        }

        return stages[index];
    }
    
    public void ShowStageUI(int stageNumber)
    {
        if (stageUIGroup != null)
        {
            stageUIGroup.gameObject.SetActive(true);
            if (stageText != null) stageText.text = "STAGE " + stageNumber;
            stageUIGroup.DOFade(1f, 1.0f); // 1초 동안 페이드 인
        }
    }
    
    public void HideStageUI()
    {
        if (stageUIGroup != null)
        {
            stageUIGroup.DOFade(0f, 1.0f).OnComplete(() => {
                stageUIGroup.gameObject.SetActive(false);
            });
        }
    }

    public void ShowTurnUI(bool isPlayerTurn)
    {
        CanvasGroup targetGroup = isPlayerTurn ? myTurnUIGroup : opponentTurnUIGroup;
        if (targetGroup != null)
        {
            targetGroup.gameObject.SetActive(true);
            targetGroup.DOFade(1f, 0.3f); // 0.3초 동안 빠르게 페이드 인
        }
    }

    public void HideTurnUI(bool isPlayerTurn)
    {
        CanvasGroup targetGroup = isPlayerTurn ? myTurnUIGroup : opponentTurnUIGroup;
        if (targetGroup != null)
        {
            targetGroup.DOFade(0f, 0.3f).OnComplete(() =>
            {
                targetGroup.gameObject.SetActive(false);
            });
        }
    }

    public void ShowGameOverUI()
    {
        if (gameOverUIGroup != null)
        {
            gameOverUIGroup.gameObject.SetActive(true);
            gameOverUIGroup.DOFade(1f, 0.5f);
        }
    }

    public void HideGameOverUI()
    {
        if (gameOverUIGroup != null)
        {
            gameOverUIGroup.DOFade(0f, 0.5f).OnComplete(() =>
            {
                gameOverUIGroup.gameObject.SetActive(false);
            });
        }
    }
}
