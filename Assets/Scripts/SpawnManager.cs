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
        // 스테이지 UI 표시
        if (StageManager.Instance != null) StageManager.Instance.ShowStageUI(stage);

        // 1. 블랙홀 스케일 초기화 및 스폰 시작 위치 지정
        Vector3 topCenter = topBlackHole != null ? topBlackHole.transform.localPosition : new Vector3(0, GridManager.Instance.faceDistance, 0);
        Vector3 bottomCenter = bottomBlackHole != null ? bottomBlackHole.transform.localPosition : new Vector3(0, -GridManager.Instance.faceDistance, 0);

        if (topBlackHole != null && bottomBlackHole != null)
        {
            topBlackHole.SetActive(true);
            bottomBlackHole.SetActive(true);

            // 블랙홀 스케일링 등장 연출 (Y축은 유지하고 X, Z축만 0으로 초기화)
            topBlackHole.transform.localScale = new Vector3(0, originalTopScale.y, 0);
            bottomBlackHole.transform.localScale = new Vector3(0, originalBottomScale.y, 0);
            
            // 원래 씬에 배치해두었던 스케일 크기대로 복원 (Y축 고정)
            topBlackHole.transform.DOScale(new Vector3(originalTopScale.x, originalTopScale.y, originalTopScale.z), 1.5f).SetEase(Ease.OutBack);
            bottomBlackHole.transform.DOScale(new Vector3(originalBottomScale.x, originalBottomScale.y, originalBottomScale.z), 1.5f).SetEase(Ease.OutBack);
            
            yield return new WaitForSeconds(1.5f); // 등장 애니메이션 대기
        }

        // 2. 기물 생성
        List<Vector2Int> spawnCoords = GetPerimeterCoordinates();
        
        // --- 플레이어 기물 생성 (윗면, 고정 1개) ---
        List<Vector2Int> playerCoords = new List<Vector2Int>(spawnCoords);
        Shuffle(playerCoords);
        SpawnPiece(playerPiecePrefab, Vector3.up, playerCoords[0], topCenter, true);
        yield return new WaitForSeconds(0.2f);

        // --- 적군 기물 생성 (스테이지 데이터 비례) ---
        List<Vector2Int> enemyCoords = new List<Vector2Int>(spawnCoords);
        Shuffle(enemyCoords);
        int coordIndex = 0;

        if (StageManager.Instance != null)
        {
            StageData stageData = StageManager.Instance.GetStageData(stage);
            if (stageData != null)
            {
                foreach (var enemyData in stageData.enemies)
                {
                    for (int i = 0; i < enemyData.count; i++)
                    {
                        if (coordIndex >= enemyCoords.Count) break; // 최대 12칸 안전장치
                        
                        SpawnPiece(enemyData.enemyPrefab, Vector3.down, enemyCoords[coordIndex], bottomCenter, false);
                        coordIndex++;
                        yield return new WaitForSeconds(0.2f);
                    }
                }
            }
        }
        else
        {
            // StageManager가 없을 경우를 대비한 기존 로직 유지
            int enemyCount = stage; 
            if (enemyCount > 12) enemyCount = 12; 
            for (int i = 0; i < enemyCount; i++)
            {
                SpawnPiece(enemyPiecePrefab, Vector3.down, enemyCoords[i], bottomCenter, false);
                yield return new WaitForSeconds(0.2f);
            }
        }

        // 마지막 기물이 착지할 때까지 대기
        yield return new WaitForSeconds(jumpDuration + 0.5f);

        // 3. 블랙홀 사라짐 연출 (Y축 유지, X/Z축만 0으로 축소)
        if (topBlackHole != null && bottomBlackHole != null)
        {
            topBlackHole.transform.DOScale(new Vector3(0, originalTopScale.y, 0), 1.0f).SetEase(Ease.InBack).OnComplete(() => topBlackHole.SetActive(false));
            bottomBlackHole.transform.DOScale(new Vector3(0, originalBottomScale.y, 0), 1.0f).SetEase(Ease.InBack).OnComplete(() => bottomBlackHole.SetActive(false));
        }

        yield return new WaitForSeconds(1.0f);

        // 스테이지 UI 숨기기
        if (StageManager.Instance != null) StageManager.Instance.HideStageUI();

        // 4. 스폰 시퀀스 종료 및 턴 전환 연출 시작
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

    // 중앙 2x2를 둘러싸는 4x4 테두리(12칸) 좌표 목록 반환
    private List<Vector2Int> GetPerimeterCoordinates()
    {
        List<Vector2Int> coords = new List<Vector2Int>();
        // 8x8 보드에서 중앙은 (3,3), (3,4), (4,3), (4,4)
        // 이를 감싸는 테두리는 X: 2~5, Y: 2~5 중 테두리에 해당하는 좌표
        for (int x = 2; x <= 5; x++)
        {
            for (int y = 2; y <= 5; y++)
            {
                if (x == 2 || x == 5 || y == 2 || y == 5)
                {
                    coords.Add(new Vector2Int(x, y));
                }
            }
        }
        return coords;
    }

    // 리스트 무작위 섞기 알고리즘
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
