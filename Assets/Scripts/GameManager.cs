using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public enum GameState
{
    Init,
    SpawningPieces,
    PlayerTurnTransition, // 턴 전환 연출 대기 상태
    PlayerTurn,
    PlayerAttacking,
    EnemyTurnTransition, // 턴 전환 연출 대기 상태
    EnemyTurn,
    StageClear,
    GameOver
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    public GameState currentState;
    public int currentStage = 1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 임시로 게임 시작 시 바로 스폰 시퀀스 진입
        ChangeState(GameState.SpawningPieces);
    }

    public void ChangeState(GameState newState)
    {
        currentState = newState;
        
        switch (currentState)
        {
            case GameState.SpawningPieces:
                if (SpawnManager.Instance != null)
                {
                    SpawnManager.Instance.StartSpawning(currentStage);
                }
                break;
            case GameState.PlayerTurnTransition:
                StartCoroutine(TurnTransitionRoutine(true, GameState.PlayerTurn));
                break;
            case GameState.PlayerTurn:
                Debug.Log("=== 플레이어 턴 시작! ===");
                break;
            case GameState.PlayerAttacking:
                StartCoroutine(PlayerAttackRoutine());
                break;
            case GameState.EnemyTurnTransition:
                StartCoroutine(TurnTransitionRoutine(false, GameState.EnemyTurn));
                break;
            case GameState.EnemyTurn:
                Debug.Log("=== 적 턴 시작! ===");
                StartCoroutine(EnemyTurnRoutine());
                break;
            case GameState.StageClear:
                StartCoroutine(StageClearRoutine());
                break;
        }
    }

    // UI 버튼(END 버튼)의 OnClick 이벤트에 연결할 메서드
    public void OnEndTurnButtonClicked()
    {
        if (currentState == GameState.PlayerTurn)
        {
            ChangeState(GameState.PlayerAttacking);
        }
    }

    private IEnumerator PlayerAttackRoutine()
    {
        ChessPieceController[] allPieces = FindObjectsByType<ChessPieceController>(FindObjectsSortMode.None);
        ChessPieceController player = null;
        List<ChessPieceController> enemies = new List<ChessPieceController>();
        
        foreach (var p in allPieces)
        {
            if (p.isPlayerPiece) player = p;
            else enemies.Add(p);
        }

        if (player == null)
        {
            ChangeState(GameState.EnemyTurn);
            yield break;
        }

        // 플레이어 이동 확정 (턴 시작 위치를 현재 위치로 갱신)
        player.CommitMove();

        // 1. 내려찍기 연출 (DOTween)
        player.isSpawning = true; // Update 루프 보간 차단 (애니메이션 충돌 방지)
        Vector3 originalLocalPos = player.transform.localPosition;
        
        // transform.up은 월드 좌표계이므로, 로컬 좌표계 기준의 위쪽 방향을 구합니다.
        Vector3 localUp = player.transform.localRotation * Vector3.up; 
        Vector3 upLocalPos = originalLocalPos + (localUp * 1.5f); // 해당 면의 수직 방향으로 떠오름
        
        player.transform.DOKill();
        // 천천히 떠오르기
        player.transform.DOLocalMove(upLocalPos, 0.6f).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(0.6f);

        // 빠르게 내려찍기
        player.transform.DOLocalMove(originalLocalPos, 0.15f).SetEase(Ease.InExpo);
        yield return new WaitForSeconds(0.15f);

        player.isSpawning = false; // 보간 원복

        // 2. 내려찍기 이펙트 발생
        if (player.slamEffectPrefab != null)
        {
            Instantiate(player.slamEffectPrefab, player.transform.position, player.transform.rotation);
        }

        // 3. 0.5초 대기
        yield return new WaitForSeconds(0.5f);

        // 4. 수직 방향 4칸(상하좌우 십자) + 플레이어 자신의 위치 포함 (총 5칸) 범위 탐색
        List<GridPos> attackTiles = new List<GridPos>();
        attackTiles.Add(player.currentGridPos); // 중앙(플레이어 위치) 포함

        int[][] dirs = new int[][] { new int[]{1,0}, new int[]{-1,0}, new int[]{0,1}, new int[]{0,-1} };
        foreach (var d in dirs)
        {
            GridPos neighbor = GridManager.Instance.GetNeighbor(player.currentGridPos, d[0], d[1]);
            attackTiles.Add(neighbor);
        }

        bool hitAnyEnemy = false;

        // 5. 범위 내 적에게 데미지 부여
        foreach (var enemy in enemies)
        {
            if (attackTiles.Contains(enemy.currentGridPos))
            {
                if (enemy.currentGridPos.Equals(player.currentGridPos))
                {
                    // 룩 밑에 깔린 적은 넉백 없이 압사 연출
                    enemy.TakeFatalDamage();
                }
                else
                {
                    // 주변 칸의 적은 기존처럼 넉백
                    enemy.TakeDamage(player.attackPower, player.transform.localPosition);
                }
                hitAnyEnemy = true;
            }
        }

        // 6. 피격 애니메이션 대기 후 턴 넘김
        if (hitAnyEnemy)
        {
            yield return new WaitForSeconds(0.75f); // 넉백(0.2s) + 페이드아웃(0.5s) 대기
        }
        else
        {
            yield return new WaitForSeconds(0.2f); // 타격된 적이 없을 시 짧은 대기
        }

        // --- 여기서 살아남은 적 확인 ---
        bool enemyAlive = false;
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.currentHP > 0)
            {
                enemyAlive = true;
                break;
            }
        }

        if (!enemyAlive)
        {
            ChangeState(GameState.StageClear);
        }
        else
        {
            ChangeState(GameState.EnemyTurnTransition);
        }
    }

    private IEnumerator TurnTransitionRoutine(bool isPlayerTurn, GameState nextState)
    {
        // 턴 UI 표시
        if (StageManager.Instance != null)
        {
            StageManager.Instance.ShowTurnUI(isPlayerTurn);
        }
        
        // 1.5초간 대기 (이 상태에서는 PlayerTurn이 아니므로 조작이 막힘)
        yield return new WaitForSeconds(1.5f);
        
        // 턴 UI 숨기기
        if (StageManager.Instance != null)
        {
            StageManager.Instance.HideTurnUI(isPlayerTurn);
        }
        
        // 실제 턴으로 진입
        ChangeState(nextState);
    }

    private IEnumerator StageClearRoutine()
    {
        Debug.Log("=== 스테이지 클리어! ===");
        
        ChessPieceController[] allPieces = FindObjectsByType<ChessPieceController>(FindObjectsSortMode.None);
        ChessPieceController player = null;
        foreach (var p in allPieces)
        {
            if (p != null && p.isPlayerPiece) player = p;
        }

        if (player != null)
        {
            // 플레이어 승천
            player.RocketLaunchAndDestroy();
            yield return new WaitForSeconds(2.0f); // 날아가는 시간 대기
        }

        // 보드 회전 리셋
        BoardRotator rotator = FindObjectOfType<BoardRotator>();
        if (rotator != null)
        {
            rotator.ResetRotation(1.5f);
            yield return new WaitForSeconds(1.5f);
        }

        // 스테이지 증가 및 다음 스테이지 시작
        currentStage++;
        ChangeState(GameState.SpawningPieces);
    }

    // 적 턴 BFS AI 코루틴
    private IEnumerator EnemyTurnRoutine()
    {
        ChessPieceController[] allPieces = FindObjectsByType<ChessPieceController>(FindObjectsSortMode.None);
        ChessPieceController player = null;
        List<ChessPieceController> enemies = new List<ChessPieceController>();
        
        foreach (var p in allPieces)
        {
            if (p.isPlayerPiece) player = p;
            else if (p != null && p.currentHP > 0) // 파괴되었거나 죽은 적 제외
            {
                enemies.Add(p);
            }
        }

        if (player == null)
        {
            ChangeState(GameState.PlayerTurn);
            yield break;
        }

        // 1. BFS 거리맵 생성 (플레이어 룩 위치가 0)
        var flowField = GridManager.Instance.GenerateFlowField(player.currentGridPos);

        // [신규] 예약(점유) 타일 목록 생성: 현재 적들의 위치로 초기화
        HashSet<GridPos> reservedTiles = new HashSet<GridPos>();
        foreach (var enemy in enemies)
        {
            if (enemy != null) reservedTiles.Add(enemy.currentGridPos);
        }
        // 플레이어 자리로 겹쳐서 들어가는 것 방지 (포획 미구현이므로)
        reservedTiles.Add(player.currentGridPos);

        // 2. 적군들 순차적으로 1칸씩 이동
        foreach (var enemy in enemies)
        {
            if (enemy == null) continue; // 안전 장치
            
            // 내 현재 위치는 이동할 것이므로 예약에서 해제
            reservedTiles.Remove(enemy.currentGridPos);

            GridPos bestMove = enemy.currentGridPos;
            int minDistance = int.MaxValue;

            // 상하좌우(모서리 넘기 포함) 4칸을 탐색하여 거리가 가장 작은 칸 선택
            int[][] dirs = new int[][] { new int[]{1,0}, new int[]{-1,0}, new int[]{0,1}, new int[]{0,-1} };
            foreach (var d in dirs)
            {
                GridPos neighbor = GridManager.Instance.GetNeighbor(enemy.currentGridPos, d[0], d[1]);
                if (flowField.ContainsKey(neighbor))
                {
                    // 이미 다른 기물이 예약(점유)한 자리는 패스!
                    if (reservedTiles.Contains(neighbor)) continue;

                    if (flowField[neighbor] < minDistance)
                    {
                        minDistance = flowField[neighbor];
                        bestMove = neighbor;
                    }
                }
            }

            // 이동 확정 후 해당 칸을 예약 목록에 추가 (다른 동료가 이 자리로 오지 못하게 막음)
            reservedTiles.Add(bestMove);

            if (!bestMove.Equals(enemy.currentGridPos))
            {
                CubeFace oldFace = enemy.currentGridPos.face;

                // 이동 실행
                enemy.currentGridPos = bestMove;
                enemy.CommitMove();
                
                Vector3 newNormal = GridManager.Instance.GetNormalFromFace(bestMove.face);
                Vector3 targetPos = GridManager.Instance.GetLocalPosition(newNormal, bestMove.x, bestMove.y, enemy.board);
                Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, newNormal);

                // Update()의 Lerp(보정)와 DOTween 애니메이션이 서로 충돌하며 덜덜 떨리는 현상 방지
                enemy.isSpawning = true; 
                enemy.transform.DOKill(); // 기존 애니메이션 강제 정지
                
                // 같은 면 내에서 이동할 때는 큐브 표면을 부드럽게 미끄러지게(Slide) 연출
                if (oldFace == bestMove.face)
                {
                    enemy.transform.DOLocalMove(targetPos, 0.4f).SetEase(Ease.OutCubic);
                }
                else // 면과 면의 모서리를 넘어갈 때는 큐브 안쪽으로 파고드는 것을 방지하기 위해 아주 살짝만 점프
                {
                    enemy.transform.DOLocalJump(targetPos, 0.2f, 1, 0.4f).SetEase(Ease.OutCubic);
                }
                
                enemy.transform.DOLocalRotateQuaternion(targetRot, 0.4f).SetEase(Ease.OutCubic);
                
                yield return new WaitForSeconds(0.45f); // 다음 적군 이동까지 약간 대기 (속도 상향)
                
                enemy.InitializeAfterSpawn(targetPos, targetRot); // 내부 물리 좌표 동기화
            }
        }

        yield return new WaitForSeconds(0.5f);
        ChangeState(GameState.PlayerTurnTransition); // 적 턴 끝, 다시 플레이어 턴(트랜지션)으로
    }
}
