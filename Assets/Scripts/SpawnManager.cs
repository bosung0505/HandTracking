using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject playerPiecePrefab;
    public GameObject enemyPiecePrefab;
    public GameObject bossPiecePrefab;

    [Header("Scene References")]
    public GameObject topBlackHole;
    public GameObject bottomBlackHole;

    [Header("Board Reference")]
    public Transform board;

    [Header("Spawn Settings")]
    public float jumpDuration = 1.0f;
    public float jumpPower    = 2.0f;

    [Header("Stage Configs (1~10)")]
    public StageConfig[] stageConfigs = new StageConfig[]
    {
        new StageConfig { stageNumber=1,  normalEnemyCount=1, normalEnemyHp=2,  normalEnemyAtk=1, normalEnemyAtkRange=1, isBossStage=false },
        new StageConfig { stageNumber=2,  normalEnemyCount=2, normalEnemyHp=2,  normalEnemyAtk=1, normalEnemyAtkRange=1, isBossStage=false },
        new StageConfig { stageNumber=3,  normalEnemyCount=2, normalEnemyHp=2,  normalEnemyAtk=1, normalEnemyAtkRange=1, isBossStage=true,  bossHp=5, bossAtk=2, bossAtkRange=2 },
        new StageConfig { stageNumber=4,  normalEnemyCount=3, normalEnemyHp=2,  normalEnemyAtk=1, normalEnemyAtkRange=1, isBossStage=false },
        new StageConfig { stageNumber=5,  normalEnemyCount=4, normalEnemyHp=2,  normalEnemyAtk=1, normalEnemyAtkRange=1, isBossStage=false },
        new StageConfig { stageNumber=6,  normalEnemyCount=3, normalEnemyHp=2,  normalEnemyAtk=1, normalEnemyAtkRange=1, isBossStage=true,  bossHp=10, bossAtk=3, bossAtkRange=2 },
        new StageConfig { stageNumber=7,  normalEnemyCount=4, normalEnemyHp=2,  normalEnemyAtk=1, normalEnemyAtkRange=1, isBossStage=false },
        new StageConfig { stageNumber=8,  normalEnemyCount=5, normalEnemyHp=2,  normalEnemyAtk=1, normalEnemyAtkRange=1, isBossStage=false },
        new StageConfig { stageNumber=9,  normalEnemyCount=4, normalEnemyHp=2,  normalEnemyAtk=1, normalEnemyAtkRange=1, isBossStage=true,  bossHp=15, bossAtk=4, bossAtkRange=3 },
        new StageConfig { stageNumber=10, normalEnemyCount=6, normalEnemyHp=2,  normalEnemyAtk=1, normalEnemyAtkRange=1, isBossStage=false },
    };

    private Vector3 originalTopScale;
    private Vector3 originalBottomScale;
    private ChessPieceController playerPiece;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (topBlackHole != null)    originalTopScale    = topBlackHole.transform.localScale;
        if (bottomBlackHole != null) originalBottomScale = bottomBlackHole.transform.localScale;
    }

    public void StartSpawning(int currentStage)
    {
        StartCoroutine(SpawnSequence(currentStage));
    }

    private IEnumerator SpawnSequence(int stage)
    {
        // [팀원 추가] 이전 스테이지에 남은 적 기물 정리
        CleanupEnemies();

        // 스테이지 UI 표시
        if (StageManager.Instance != null) StageManager.Instance.ShowStageUI(stage);

        Vector3 topCenter    = topBlackHole    != null ? topBlackHole.transform.localPosition    : new Vector3(0,  GridManager.Instance.faceDistance, 0);
        Vector3 bottomCenter = bottomBlackHole != null ? bottomBlackHole.transform.localPosition : new Vector3(0, -GridManager.Instance.faceDistance, 0);

        if (topBlackHole != null && bottomBlackHole != null)
        {
            topBlackHole.SetActive(true);
            bottomBlackHole.SetActive(true);
            topBlackHole.transform.localScale    = new Vector3(0, originalTopScale.y,    0);
            bottomBlackHole.transform.localScale = new Vector3(0, originalBottomScale.y, 0);
            topBlackHole.transform.DOScale(originalTopScale,    1.5f).SetEase(Ease.OutBack);
            bottomBlackHole.transform.DOScale(originalBottomScale, 1.5f).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(1.5f);
        }

        // --- 플레이어 기물 생성 (비숍 색 파악용 좌표 먼저 결정) ---
        List<Vector2Int> spawnCoords  = GetPerimeterCoordinates();
        List<Vector2Int> playerCoords = new List<Vector2Int>(spawnCoords);
        Shuffle(playerCoords);
        Vector2Int playerSpawnCoord = playerCoords[0]; // 비숍 색 판단에 사용
        SpawnPiece(playerPiecePrefab, Vector3.up, playerSpawnCoord, topCenter, true);
        yield return new WaitForSeconds(0.2f);

        // --- 적군 기물 생성 ---
        List<Vector2Int> enemyCoords = new List<Vector2Int>(spawnCoords);
        Shuffle(enemyCoords);
        int coordIndex = 0;
        int playerParity = (playerSpawnCoord.x + playerSpawnCoord.y) % 2; // 0=짝수색, 1=홀수색

        if (StageManager.Instance != null)
        {
            StageData stageData = StageManager.Instance.GetStageData(stage);
            if (stageData != null)
            {
                foreach (var enemyData in stageData.enemies)
                {
                    // 비숍 종류 판단 (프리팹의 enemyType으로)
                    bool isBishop = enemyData.enemyPrefab != null &&
                                    enemyData.enemyPrefab.GetComponent<ChessPieceController>() != null &&
                                    enemyData.enemyPrefab.GetComponent<ChessPieceController>().enemyType
                                        == ChessPieceController.EnemyType.Bishop;

                    for (int i = 0; i < enemyData.count; i++)
                    {
                        if (coordIndex >= enemyCoords.Count) break;

                        if (isBishop)
                        {
                            // 비숍: 플레이어와 같은 색(x+y 홀짝) 좌표에만 생성 가능
                            // i==0이면 동색, i==1이면 다른 색
                            int targetParity = (i % 2 == 0) ? playerParity : 1 - playerParity;
                            Vector2Int bishopCoord = FindCoordWithParity(enemyCoords, coordIndex, targetParity);
                            SpawnPiece(enemyData.enemyPrefab, Vector3.down, bishopCoord, bottomCenter, false);
                            coordIndex++;
                        }
                        else
                        {
                            SpawnPiece(enemyData.enemyPrefab, Vector3.down, enemyCoords[coordIndex], bottomCenter, false);
                            coordIndex++;
                        }
                        yield return new WaitForSeconds(0.2f);
                    }
                }
            }
        }
        else
        {
            // StageManager 없을 때 안전망 (기본 적 스폰)
            int enemyCount = Mathf.Min(stage, 12);
            for (int i = 0; i < enemyCount; i++)
            {
                SpawnPiece(enemyPiecePrefab, Vector3.down, enemyCoords[i], bottomCenter, false);
                yield return new WaitForSeconds(0.2f);
            }
        }

        yield return new WaitForSeconds(jumpDuration + 0.5f);

        if (topBlackHole != null && bottomBlackHole != null)
        {
            topBlackHole.transform.DOScale(new Vector3(0, originalTopScale.y, 0),    1.0f).SetEase(Ease.InBack).OnComplete(() => topBlackHole.SetActive(false));
            bottomBlackHole.transform.DOScale(new Vector3(0, originalBottomScale.y, 0), 1.0f).SetEase(Ease.InBack).OnComplete(() => bottomBlackHole.SetActive(false));
        }
        yield return new WaitForSeconds(1.0f);

        if (StageManager.Instance != null) StageManager.Instance.HideStageUI();
        GameManager.Instance.ChangeState(GameState.PlayerTurnTransition);
    }

    // [팀원 추가] 이전 스테이지 적 기물 전부 정리
    private void CleanupEnemies()
    {
        foreach (var p in FindObjectsOfType<ChessPieceController>())
            if (!p.isPlayerPiece) Destroy(p.gameObject);
    }

    private GameObject SpawnPiece(GameObject prefab, Vector3 normal, Vector2Int gridCoord, Vector3 startPos, bool isPlayer)
    {
        if (prefab == null) return null;
        GameObject obj = Instantiate(prefab, board);
        ChessPieceController ctrl = obj.GetComponent<ChessPieceController>();
        if (ctrl != null)
        {
            ctrl.isSpawning    = true;
            ctrl.isPlayerPiece = isPlayer;
            ctrl.board         = board;
        }
        obj.transform.localPosition = startPos;
        obj.transform.localRotation = Quaternion.Euler(Random.Range(-90,90), Random.Range(0,360), Random.Range(-90,90));

        Vector3    targetPos = GridManager.Instance.GetLocalPosition(normal, gridCoord.x, gridCoord.y, board);
        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, normal);

        obj.transform.DOLocalJump(targetPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
        obj.transform.DOLocalRotateQuaternion(targetRot, jumpDuration).SetEase(Ease.OutQuad);
        DOVirtual.DelayedCall(jumpDuration, () => { if (ctrl != null) ctrl.InitializeAfterSpawn(targetPos, targetRot); });

        return obj;
    }

    // 중앙 2x2를 둘러싼 4x4 테두리(12칸) 좌표 목록 반환
    private List<Vector2Int> GetPerimeterCoordinates()
    {
        List<Vector2Int> coords = new List<Vector2Int>();
        for (int x = 2; x <= 5; x++)
            for (int y = 2; y <= 5; y++)
                if (x == 2 || x == 5 || y == 2 || y == 5)
                    coords.Add(new Vector2Int(x, y));
        return coords;
    }

    // 주어진 인덱스 이후에서 홀짝(parity)에 맞는 첫 번째 좌표 반환 (비숍 색 필터)
    private Vector2Int FindCoordWithParity(List<Vector2Int> coords, int startIndex, int parity)
    {
        for (int i = startIndex; i < coords.Count; i++)
            if ((coords[i].x + coords[i].y) % 2 == parity)
                return coords[i];
        // 찾지 못하면 시작 인덱스로 폴백
        return coords[Mathf.Min(startIndex, coords.Count - 1)];
    }

    // 리스트 무작위 섞기 알고리즘
    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            T tmp = list[i]; list[i] = list[r]; list[r] = tmp;
        }
    }
}
