using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public enum GameState { Init, SpawningPieces, PlayerTurn, EnemyTurn, StageClear, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("스테이지")]
    public GameState currentState;
    public int currentStage = 1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 플레이어 사망 이벤트 구독
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.onDead.AddListener(() => ChangeState(GameState.GameOver));

        ChangeState(GameState.SpawningPieces);
    }

    private void Update()
    {
        // 테스트용 - 빌드 전 제거
        if (Input.GetKeyDown(KeyCode.Space))
            ChangeState(GameState.StageClear);
    }

    public void ChangeState(GameState newState)
    {
        currentState = newState;
        switch (currentState)
        {
            case GameState.SpawningPieces:
                SpawnManager.Instance?.StartSpawning(currentStage);
                break;
            case GameState.PlayerTurn:
                Debug.Log("=== 플레이어 턴 시작! ===");
                break;
            case GameState.EnemyTurn:
                StartCoroutine(EnemyTurnRoutine());
                break;
            case GameState.StageClear:
                Debug.Log($"=== Stage {currentStage} Clear! ===");
                // 스테이지 클리어 시 플레이어 HP 전체 회복
                PlayerStats.Instance?.FullHeal();
                UpgradePanelUI.Instance?.ShowUpgradePanel(currentStage);
                break;
            case GameState.GameOver:
                Debug.Log("=== 게임 오버 ===");
                // TODO: 게임 오버 UI 연결
                break;
        }
    }

    // ── END 턴 버튼 ──────────────────────────────────────────────

    public void OnEndTurnButtonClicked()
    {
        if (currentState != GameState.PlayerTurn) return;

        ChessPieceController player = null;
        foreach (var p in FindObjectsOfType<ChessPieceController>())
            if (p.isPlayerPiece) { player = p; p.CommitMove(); }

        // 플레이어 공격
        if (player != null && PlayerStats.Instance != null)
            AttackEnemiesInRange(player.currentGridPos,
                PlayerStats.Instance.attackPower,
                PlayerStats.Instance.attackRange);

        StartCoroutine(CheckAfterAttack());
    }

    private IEnumerator CheckAfterAttack()
    {
        yield return new WaitForSeconds(0.5f);
        if (AreAllEnemiesDefeated()) ChangeState(GameState.StageClear);
        else ChangeState(GameState.EnemyTurn);
    }

    // ── 플레이어 공격 ────────────────────────────────────────────

    private void AttackEnemiesInRange(GridPos attackerPos, int power, int range)
    {
        foreach (var piece in FindObjectsOfType<ChessPieceController>())
        {
            if (piece.isPlayerPiece) continue;
            EnemyController ec = piece.GetComponent<EnemyController>();
            if (ec == null || ec.isDead) continue;
            if (piece.currentGridPos.face != attackerPos.face) continue;

            int dx = Mathf.Abs(piece.currentGridPos.x - attackerPos.x);
            int dy = Mathf.Abs(piece.currentGridPos.y - attackerPos.y);
            if (Mathf.Max(dx, dy) <= range)
            {
                ec.TakeDamage(power);
                Debug.Log($"[Attack] {piece.name} 피격! 데미지:{power} 남은HP:{ec.currentHp}");
            }
        }
    }

    // ── 업그레이드 완료 콜백 ────────────────────────────────────

    public void OnUpgradeComplete()
    {
        currentStage++;
        ChangeState(GameState.SpawningPieces);
    }

    // ── 적 턴 ────────────────────────────────────────────────────

    private IEnumerator EnemyTurnRoutine()
    {
        Debug.Log("=== 적 턴 시작! ===");
        ChessPieceController player = null;
        var enemies = new List<ChessPieceController>();

        foreach (var p in FindObjectsOfType<ChessPieceController>())
        {
            if (p.isPlayerPiece) player = p;
            else
            {
                EnemyController ec = p.GetComponent<EnemyController>();
                if (ec == null || !ec.isDead) enemies.Add(p);
            }
        }

        if (player == null) { ChangeState(GameState.PlayerTurn); yield break; }

        var flowField = GridManager.Instance.GenerateFlowField(player.currentGridPos);

        foreach (var enemy in enemies)
        {
            EnemyController ec = enemy.GetComponent<EnemyController>();
            if (ec != null && ec.isDead) continue;

            GridPos bestMove = enemy.currentGridPos;
            int minDist = int.MaxValue;

            int[][] dirs = { new int[]{1,0}, new int[]{-1,0}, new int[]{0,1}, new int[]{0,-1} };
            foreach (var d in dirs)
            {
                GridPos nb = GridManager.Instance.GetNeighbor(enemy.currentGridPos, d[0], d[1]);
                if (flowField.ContainsKey(nb) && flowField[nb] < minDist)
                { minDist = flowField[nb]; bestMove = nb; }
            }

            // 공격 범위 내에 플레이어가 있으면 공격
            int atkRange = ec != null ? ec.attackRange : 1;
            int pdx = Mathf.Abs(player.currentGridPos.x - enemy.currentGridPos.x);
            int pdy = Mathf.Abs(player.currentGridPos.y - enemy.currentGridPos.y);
            bool playerInRange = player.currentGridPos.face == enemy.currentGridPos.face
                                 && Mathf.Max(pdx, pdy) <= atkRange;

            if (playerInRange)
            {
                int dmg = ec?.data?.attackDamage ?? 1;
                Debug.Log($"[Enemy] {enemy.name} 플레이어 공격! 데미지:{dmg} 범위:{atkRange}");
                PlayerStats.Instance?.TakeDamage(dmg);

                // 플레이어 사망 처리
                if (PlayerStats.Instance != null && PlayerStats.Instance.currentHp <= 0)
                {
                    ChangeState(GameState.GameOver);
                    yield break;
                }
                yield return new WaitForSeconds(0.3f);
                continue;
            }

            if (!bestMove.Equals(enemy.currentGridPos))
                yield return StartCoroutine(MoveEnemyTo(enemy, bestMove));
        }

        yield return new WaitForSeconds(0.5f);

        if (AreAllEnemiesDefeated()) ChangeState(GameState.StageClear);
        else ChangeState(GameState.PlayerTurn);
    }

    private IEnumerator MoveEnemyTo(ChessPieceController enemy, GridPos bestMove)
    {
        CubeFace oldFace = enemy.currentGridPos.face;
        enemy.currentGridPos = bestMove;
        enemy.CommitMove();

        Vector3    newNormal = GridManager.Instance.GetNormalFromFace(bestMove.face);
        Vector3    targetPos = GridManager.Instance.GetLocalPosition(newNormal, bestMove.x, bestMove.y, enemy.board);
        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, newNormal);

        enemy.isSpawning = true;
        enemy.transform.DOKill();

        if (oldFace == bestMove.face)
            enemy.transform.DOLocalMove(targetPos, 0.4f).SetEase(Ease.OutCubic);
        else
            enemy.transform.DOLocalJump(targetPos, 0.2f, 1, 0.4f).SetEase(Ease.OutCubic);

        enemy.transform.DOLocalRotateQuaternion(targetRot, 0.4f).SetEase(Ease.OutCubic);
        yield return new WaitForSeconds(0.45f);
        enemy.InitializeAfterSpawn(targetPos, targetRot);
    }

    private bool AreAllEnemiesDefeated()
    {
        foreach (var p in FindObjectsOfType<ChessPieceController>())
        {
            if (p.isPlayerPiece) continue;
            EnemyController ec = p.GetComponent<EnemyController>();
            if (ec == null || !ec.isDead) return false;
        }
        return true;
    }
}
