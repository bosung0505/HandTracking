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

    [Header("Scene References")]
    public GameObject topBlackHole;      // 하이어라키에 배치된 아군 블랙홀
    public GameObject bottomBlackHole;   // 하이어라키에 배치된 적군 블랙홀

    [Header("Board Reference")]
    public Transform board;

    [Header("Spawn Settings")]
    public float jumpDuration = 1.0f;
    public float jumpPower = 2.0f;

    private Vector3 originalTopScale;
    private Vector3 originalBottomScale;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 씬에 배치된 블랙홀의 원래 스케일을 저장해 둡니다.
        if (topBlackHole != null) originalTopScale = topBlackHole.transform.localScale;
        if (bottomBlackHole != null) originalBottomScale = bottomBlackHole.transform.localScale;
    }

    public void StartSpawning(int currentStage)
    {
        StartCoroutine(SpawnSequence(currentStage));
    }

    private IEnumerator SpawnSequence(int stage)
    {
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

        // --- 플레이어 기물 생성 (의 색 기죽 파악용) ---
        List<Vector2Int> spawnCoords  = GetPerimeterCoordinates();
        List<Vector2Int> playerCoords = new List<Vector2Int>(spawnCoords);
        Shuffle(playerCoords);
        Vector2Int playerSpawnCoord = playerCoords[0]; // 비숙 색 판단에 사용
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
                    // 비숙 종류 판단 (프리팩의 enemyType으로)
                    bool isBishop = enemyData.enemyPrefab != null &&
                                    enemyData.enemyPrefab.GetComponent<ChessPieceController>() != null &&
                                    enemyData.enemyPrefab.GetComponent<ChessPieceController>().enemyType
                                        == ChessPieceController.EnemyType.Bishop;

                    for (int i = 0; i < enemyData.count; i++)
                    {
                        if (coordIndex >= enemyCoords.Count) break;

                        if (isBishop)
                        {
                            // 비숙: 플레이어와 다른 색(x+y 홈짝) 좌표를 각각 1개씩 찾아 생성
                            // i==0 이면 동색(올 수 있음), i==1 이면 다른 색
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

    private void SpawnPiece(GameObject prefab, Vector3 normal, Vector2Int gridCoord, Vector3 startPos, bool isPlayer = false)
    {
        if (prefab == null) return;

        GameObject pieceObj = Instantiate(prefab, board);
        
        ChessPieceController controller = pieceObj.GetComponent<ChessPieceController>();
        if (controller != null)
        {
            controller.isSpawning = true; // 점프 중 Update() 내부의 로컬 보정 중지
            controller.isPlayerPiece = isPlayer; // 프리팹 체크 누락 방지 강제 할당
            controller.board = this.board; // 보드 레퍼런스 강제 할당 (유저 세팅 누락 방지)
        }

        pieceObj.transform.localPosition = startPos;
        // 튀어나올 때 무작위 회전값을 주어 빙글빙글 돌면서 떨어지게 연출
        pieceObj.transform.localRotation = Quaternion.Euler(Random.Range(-90,90), Random.Range(0,360), Random.Range(-90,90));

        Vector3 targetPos = GridManager.Instance.GetLocalPosition(normal, gridCoord.x, gridCoord.y, board);
        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, normal);

        // DOTween을 활용한 포물선 점프 애니메이션 (DOLocalJump)
        pieceObj.transform.DOLocalJump(targetPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
        pieceObj.transform.DOLocalRotateQuaternion(targetRot, jumpDuration).SetEase(Ease.OutQuad);
        
        // 점프가 끝나면 스폰 상태 해제 및 안착 좌표 설정
        DOVirtual.DelayedCall(jumpDuration, () => {
            if (controller != null)
            {
                controller.InitializeAfterSpawn(targetPos, targetRot);
            }
        });
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

    // 주어진 인덱스 이후의 주어진 홈짝(parity)에 맞는 첫 번째 좌표 반환 (비숙 색 필터)
    private Vector2Int FindCoordWithParity(List<Vector2Int> coords, int startIndex, int parity)
    {
        for (int i = startIndex; i < coords.Count; i++)
            if ((coords[i].x + coords[i].y) % 2 == parity)
                return coords[i];
        // 폰지못하면 시작 인덱스로 폴백
        return coords[Mathf.Min(startIndex, coords.Count - 1)];
    }

    // 리스트 무작위 섬기 알고리즘
    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
