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
        new StageConfig { stageNumber=9,  normalEnemyCount=4, normalEnemyHp=2, normalEnemyAtk=1, normalEnemyAtkRange=1, isBossStage=true,  bossHp=15, bossAtk=4, bossAtkRange=3 },
        new StageConfig { stageNumber=10, normalEnemyCount=6, normalEnemyHp=2, normalEnemyAtk=1, normalEnemyAtkRange=1, isBossStage=false },
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
        CleanupEnemies();

        Vector3 topCenter    = topBlackHole    != null ? topBlackHole.transform.localPosition    : new Vector3(0,  GridManager.Instance.faceDistance, 0);
        Vector3 bottomCenter = bottomBlackHole != null ? bottomBlackHole.transform.localPosition : new Vector3(0, -GridManager.Instance.faceDistance, 0);

        if (topBlackHole != null && bottomBlackHole != null)
        {
            topBlackHole.SetActive(true);
            bottomBlackHole.SetActive(true);
            topBlackHole.transform.localScale    = new Vector3(0, originalTopScale.y,    0);
            bottomBlackHole.transform.localScale = new Vector3(0, originalBottomScale.y, 0);
            topBlackHole.transform.DOScale(originalTopScale,       1.5f).SetEase(Ease.OutBack);
            bottomBlackHole.transform.DOScale(originalBottomScale, 1.5f).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(1.5f);
        }

        List<Vector2Int> spawnCoords = GetPerimeterCoordinates();

        // 플레이어: 최초 1회만 스폰
        if (playerPiece == null)
        {
            List<Vector2Int> playerCoords = new List<Vector2Int>(spawnCoords);
            Shuffle(playerCoords);
            GameObject pObj = SpawnPiece(playerPiecePrefab, Vector3.up, playerCoords[0], topCenter, true);
            if (pObj != null) playerPiece = pObj.GetComponent<ChessPieceController>();
            yield return new WaitForSeconds(0.2f);
        }

        StageConfig config = GetStageConfig(stage);
        List<Vector2Int> enemyCoords = new List<Vector2Int>(spawnCoords);
        Shuffle(enemyCoords);
        int idx = 0;

        // 보스 스폰
        if (config.isBossStage)
        {
            GameObject bossPrefab = bossPiecePrefab != null ? bossPiecePrefab : enemyPiecePrefab;
            GameObject bossObj = SpawnPiece(bossPrefab, Vector3.down, enemyCoords[idx++], bottomCenter, false);
            if (bossObj != null)
            {
                EnemyController ec = bossObj.GetComponent<EnemyController>();
                if (ec == null) ec = bossObj.AddComponent<EnemyController>();
                ec.Initialize(CreateEnemyData("보스", EnemyType.Boss, config.bossHp, config.bossAtk), config.bossAtkRange);
            }
            yield return new WaitForSeconds(0.2f);
        }

        // 일반 적 스폰
        for (int i = 0; i < config.normalEnemyCount && idx < enemyCoords.Count; i++, idx++)
        {
            GameObject enemyObj = SpawnPiece(enemyPiecePrefab, Vector3.down, enemyCoords[idx], bottomCenter, false);
            if (enemyObj != null)
            {
                EnemyController ec = enemyObj.GetComponent<EnemyController>();
                if (ec == null) ec = enemyObj.AddComponent<EnemyController>();
                ec.Initialize(CreateEnemyData("적", EnemyType.Normal, config.normalEnemyHp, config.normalEnemyAtk), config.normalEnemyAtkRange);
            }
            yield return new WaitForSeconds(0.2f);
        }

        yield return new WaitForSeconds(jumpDuration + 0.5f);

        if (topBlackHole != null && bottomBlackHole != null)
        {
            topBlackHole.transform.DOScale(new Vector3(0, originalTopScale.y, 0), 1.0f)
                .SetEase(Ease.InBack).OnComplete(() => topBlackHole.SetActive(false));
            bottomBlackHole.transform.DOScale(new Vector3(0, originalBottomScale.y, 0), 1.0f)
                .SetEase(Ease.InBack).OnComplete(() => bottomBlackHole.SetActive(false));
        }

        yield return new WaitForSeconds(1.0f);
        GameManager.Instance.ChangeState(GameState.PlayerTurn);
    }

    private void CleanupEnemies()
    {
        foreach (var p in FindObjectsOfType<ChessPieceController>())
            if (!p.isPlayerPiece) Destroy(p.gameObject);
    }

    private StageConfig GetStageConfig(int stage)
    {
        int i = Mathf.Clamp(stage - 1, 0, stageConfigs.Length - 1);
        return stageConfigs[i];
    }

    private EnemyData CreateEnemyData(string name, EnemyType type, int hp, int atk)
    {
        EnemyData d     = ScriptableObject.CreateInstance<EnemyData>();
        d.enemyName     = name;
        d.enemyType     = type;
        d.maxHp         = hp;
        d.attackDamage  = atk;
        return d;
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

    private List<Vector2Int> GetPerimeterCoordinates()
    {
        var coords = new List<Vector2Int>();
        for (int x = 2; x <= 5; x++)
            for (int y = 2; y <= 5; y++)
                if (x == 2 || x == 5 || y == 2 || y == 5)
                    coords.Add(new Vector2Int(x, y));
        return coords;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            T tmp = list[i]; list[i] = list[r]; list[r] = tmp;
        }
    }
}
