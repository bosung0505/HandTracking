using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public enum GameState
{
    Init,
    SpawningPieces,
    PlayerTurn,
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
            case GameState.PlayerTurn:
                Debug.Log("=== 플레이어 턴 시작! ===");
                break;
            case GameState.EnemyTurn:
                Debug.Log("=== 적 턴 시작! ===");
                StartCoroutine(EnemyTurnRoutine());
                break;
        }
    }

    // UI 버튼(END 버튼)의 OnClick 이벤트에 연결할 메서드
    public void OnEndTurnButtonClicked()
    {
        if (currentState == GameState.PlayerTurn)
        {
            // END 버튼을 누르면 플레이어의 이동을 완전히 확정(Commit) 짓습니다.
            ChessPieceController[] pieces = FindObjectsOfType<ChessPieceController>();
            foreach (var p in pieces)
            {
                if (p.isPlayerPiece) p.CommitMove();
            }
            ChangeState(GameState.EnemyTurn);
        }
    }

    // 적 턴 BFS AI 코루틴
    private IEnumerator EnemyTurnRoutine()
    {
        ChessPieceController[] allPieces = FindObjectsOfType<ChessPieceController>();
        ChessPieceController player = null;
        List<ChessPieceController> enemies = new List<ChessPieceController>();
        
        foreach (var p in allPieces)
        {
            if (p.isPlayerPiece) player = p;
            else enemies.Add(p);
        }

        if (player == null)
        {
            ChangeState(GameState.PlayerTurn);
            yield break;
        }

        // 1. BFS 거리맵 생성 (플레이어 룩 위치가 0)
        var flowField = GridManager.Instance.GenerateFlowField(player.currentGridPos);

        // 2. 적군들 순차적으로 1칸씩 이동
        foreach (var enemy in enemies)
        {
            GridPos bestMove = enemy.currentGridPos;
            int minDistance = int.MaxValue;

            // 상하좌우(모서리 넘기 포함) 4칸을 탐색하여 거리가 가장 작은(플레이어와 가까운) 칸 선택
            int[][] dirs = new int[][] { new int[]{1,0}, new int[]{-1,0}, new int[]{0,1}, new int[]{0,-1} };
            foreach (var d in dirs)
            {
                GridPos neighbor = GridManager.Instance.GetNeighbor(enemy.currentGridPos, d[0], d[1]);
                if (flowField.ContainsKey(neighbor))
                {
                    // TODO: 나중에 이 자리에 다른 적군이 있는지 확인하는 예외처리 추가 필요
                    if (flowField[neighbor] < minDistance)
                    {
                        minDistance = flowField[neighbor];
                        bestMove = neighbor;
                    }
                }
            }

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
        ChangeState(GameState.PlayerTurn); // 적 턴 끝, 다시 플레이어 턴으로
    }
}
