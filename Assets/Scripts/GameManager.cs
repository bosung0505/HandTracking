using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

// ── [내 브랜치] 턴 전환 연출 상태 포함한 완전한 열거형 ──────────
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
                StartCoroutine(EnemyTurnRoutine());
                break;
            case GameState.StageClear:
                StartCoroutine(StageClearRoutine());
                break;
            case GameState.GameOver:
                StartCoroutine(GameOverRoutine());
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

        // PlayerAttackRoutine이 공격 연출 + 상태 전환을 처리함
        if (player != null && PlayerStats.Instance != null)
            AttackEnemiesInRange(player.currentGridPos,
                PlayerStats.Instance.attackPower,
                PlayerStats.Instance.attackRange);

        StartCoroutine(CheckAfterAttack());
    }

    private IEnumerator CheckAfterAttack()
    {
        // PlayerAttackRoutine이 실행 중이면 완료될 때까지 대기
        while (currentState == GameState.PlayerAttacking)
            yield return null;

        // PlayerAttackRoutine이 이미 다음 상태(EnemyTurnTransition/StageClear)로
        // 전환했으면 중복 처리하지 않음
        if (currentState == GameState.EnemyTurnTransition ||
            currentState == GameState.EnemyTurn          ||
            currentState == GameState.StageClear         ||
            currentState == GameState.GameOver)
            yield break;

        // 안전망: PlayerAttacking 상태를 거치지 않은 경우 직접 처리
        yield return new WaitForSeconds(0.5f);
        if (AreAllEnemiesDefeated()) ChangeState(GameState.StageClear);
        else ChangeState(GameState.EnemyTurn);
    }

    // ── 플레이어 공격 ────────────────────────────────────────────

    private void AttackEnemiesInRange(GridPos attackerPos, int power, int range)
    {
        // PlayerAttackRoutine(내려찍기 연출)이 실제 공격 범위 계산 및 데미지를 처리함
        ChangeState(GameState.PlayerAttacking);
    }

    private IEnumerator PlayerAttackRoutine()
    {
        ChessPieceController[] allPieces = FindObjectsByType<ChessPieceController>(FindObjectsSortMode.None);
        ChessPieceController player = null;
        var enemies = new List<ChessPieceController>();

        foreach (var p in allPieces)
        {
            if (p.isPlayerPiece) player = p;
            else
            {
                EnemyController ec = p.GetComponent<EnemyController>();
                if (ec == null || !ec.isDead) enemies.Add(p);
            }
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

    // ── 업그레이드 완료 콜백 (UpgradePanelUI에서 호출) ──────────
    // [팀원 추가] 업그레이드 선택 후 다음 스테이지로 진행하는 콜백

    public void OnUpgradeComplete()
    {
        currentStage++;
        ChangeState(GameState.SpawningPieces);
    }

    // ── 적 턴 (폰 + 비숍 AI) ─────────────────────────────────────

    private IEnumerator EnemyTurnRoutine()
    {
        ChessPieceController[] allPieces = FindObjectsByType<ChessPieceController>(FindObjectsSortMode.None);
        ChessPieceController player = null;
        List<ChessPieceController> enemies = new List<ChessPieceController>();

        foreach (var p in allPieces)
        {
            if (p.isPlayerPiece) player = p;
            else if (p != null && p.currentHP > 0)
                enemies.Add(p);
        }

        if (player == null) { ChangeState(GameState.PlayerTurn); yield break; }

        // ===== [Phase 1] 공격 체크 (이동 전, enemies 순서대로 순차 처리) =====
        int[][] diagDirs  = new int[][] { new int[]{1,1}, new int[]{1,-1}, new int[]{-1,1}, new int[]{-1,-1} };
        int[][] cardDirs  = new int[][] { new int[]{1,0}, new int[]{-1,0}, new int[]{0,1},  new int[]{0,-1}  };
        HashSet<ChessPieceController> attackerSet = new HashSet<ChessPieceController>();

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            // ── 폰: 대각선 1칸 공격 ──
            if (enemy.enemyType == ChessPieceController.EnemyType.Pawn)
            {
                bool isAttacker = false;
                foreach (var d in diagDirs)
                {
                    int nx = enemy.currentGridPos.x + d[0];
                    int ny = enemy.currentGridPos.y + d[1];
                    if (nx < 0 || nx > 7 || ny < 0 || ny > 7) continue;
                    if (new GridPos(enemy.currentGridPos.face, nx, ny).Equals(player.currentGridPos))
                    { isAttacker = true; break; }
                }
                if (!isAttacker) continue;

                attackerSet.Add(enemy);
                enemy.PerformAttackAnimation(player.transform.localPosition);
                yield return new WaitForSeconds(0.43f);
                if (player != null && player.currentHP > 0)
                    player.TakeDamage(enemy.attackPower, enemy.transform.localPosition);
                yield return new WaitForSeconds(0.5f);
                if (currentState == GameState.GameOver) yield break;
            }
            // ── 비숍: 대각선 스캔 → 바로 앞 타일로 슬라이드 → 박치기 ──
            else if (enemy.enemyType == ChessPieceController.EnemyType.Bishop)
            {
                GridPos launchPos = GridPos.Invalid;
                foreach (var d in diagDirs)
                {
                    GridPos last = enemy.currentGridPos;
                    for (int step = 1; step <= 7; step++)
                    {
                        int nx = last.x + d[0]; int ny = last.y + d[1];
                        if (nx < 0 || nx > 7 || ny < 0 || ny > 7) break; // 면 끝
                        GridPos cand = new GridPos(enemy.currentGridPos.face, nx, ny);
                        if (cand.Equals(player.currentGridPos)) { launchPos = last; break; } // 플레이어 발견
                        bool blocked = false;
                        foreach (var e2 in enemies) if (e2 != null && e2 != enemy && e2.currentGridPos.Equals(cand)) { blocked = true; break; }
                        if (blocked) break;
                        last = cand;
                    }
                    if (!launchPos.Equals(GridPos.Invalid)) break;
                }
                if (launchPos.Equals(GridPos.Invalid)) continue; // 공격 불가

                attackerSet.Add(enemy);

                // 1. launchPos까지 슬라이드
                Vector3 launchNormal = GridManager.Instance.GetNormalFromFace(launchPos.face);
                Vector3 launchLocalPos = GridManager.Instance.GetLocalPosition(launchNormal, launchPos.x, launchPos.y, enemy.board);
                Quaternion launchRot = Quaternion.FromToRotation(Vector3.up, launchNormal);

                enemy.isSpawning = true;
                enemy.transform.DOKill();
                enemy.transform.DOLocalMove(launchLocalPos, 0.45f).SetEase(Ease.OutCubic);
                enemy.transform.DOLocalRotateQuaternion(launchRot, 0.45f);
                yield return new WaitForSeconds(0.45f);
                enemy.InitializeAfterSpawn(launchLocalPos, launchRot); // 내부 좌표 갱신

                // 2. 박치기
                enemy.PerformAttackAnimation(player.transform.localPosition);
                yield return new WaitForSeconds(0.43f);
                if (player != null && player.currentHP > 0)
                    player.TakeDamage(enemy.attackPower, enemy.transform.localPosition);
                yield return new WaitForSeconds(0.5f);
                if (currentState == GameState.GameOver) yield break;
            }
        }

        // ===== [Phase 2] 이동 (공격하지 않은 기물만) =====
        var flowField = GridManager.Instance.GenerateFlowField(player.currentGridPos);

        HashSet<GridPos> reservedTiles = new HashSet<GridPos>();
        foreach (var enemy in enemies)
            if (enemy != null) reservedTiles.Add(enemy.currentGridPos);
        reservedTiles.Add(player.currentGridPos);

        foreach (var enemy in enemies)
        {
            if (enemy == null || attackerSet.Contains(enemy)) continue;

            reservedTiles.Remove(enemy.currentGridPos);

            // ── 폰: 카디널 BFS 1칸 이동 ──
            if (enemy.enemyType == ChessPieceController.EnemyType.Pawn)
            {
                GridPos bestMove = enemy.currentGridPos;
                int minDist = int.MaxValue;
                foreach (var d in cardDirs)
                {
                    GridPos nb = GridManager.Instance.GetNeighbor(enemy.currentGridPos, d[0], d[1]);
                    if (!reservedTiles.Contains(nb) && flowField.ContainsKey(nb) && flowField[nb] < minDist)
                    { minDist = flowField[nb]; bestMove = nb; }
                }
                reservedTiles.Add(bestMove);
                yield return StartCoroutine(MovePiece(enemy, bestMove));
            }
            // ── 비숍: 대각선 돌진 이동 ──
            else if (enemy.enemyType == ChessPieceController.EnemyType.Bishop)
            {
                GridPos bestMove = enemy.currentGridPos;
                int minDist = flowField.ContainsKey(enemy.currentGridPos) ? flowField[enemy.currentGridPos] : int.MaxValue;

                bool atEdge = enemy.currentGridPos.x == 0 || enemy.currentGridPos.x == 7 ||
                              enemy.currentGridPos.y == 0 || enemy.currentGridPos.y == 7;

                // 대각선 돌진: 같은 면 안에서 끝까지
                foreach (var d in diagDirs)
                {
                    GridPos last = enemy.currentGridPos;
                    for (int step = 1; step <= 7; step++)
                    {
                        int nx = last.x + d[0]; int ny = last.y + d[1];
                        if (nx < 0 || nx > 7 || ny < 0 || ny > 7) break;
                        GridPos cand = new GridPos(enemy.currentGridPos.face, nx, ny);
                        if (reservedTiles.Contains(cand)) break;
                        last = cand;
                    }
                    if (last.Equals(enemy.currentGridPos)) continue;
                    if (flowField.ContainsKey(last) && flowField[last] < minDist)
                    { minDist = flowField[last]; bestMove = last; }
                }

                // 면 전환: 면 끝에 있을 때 카디널로 면 넘기
                if (atEdge)
                {
                    foreach (var d in cardDirs)
                    {
                        int nx = enemy.currentGridPos.x + d[0];
                        int ny = enemy.currentGridPos.y + d[1];
                        if (nx >= 0 && nx <= 7 && ny >= 0 && ny <= 7) continue; // 면 안이면 스킵
                        GridPos cross = GridManager.Instance.GetNeighbor(enemy.currentGridPos, d[0], d[1]);
                        if (reservedTiles.Contains(cross)) continue;
                        if (flowField.ContainsKey(cross) && flowField[cross] < minDist)
                        { minDist = flowField[cross]; bestMove = cross; }
                    }
                }

                reservedTiles.Add(bestMove);
                yield return StartCoroutine(MovePiece(enemy, bestMove));
            }
        }

        yield return new WaitForSeconds(0.3f);
        ChangeState(GameState.PlayerTurnTransition);
    }

    // 기물 이동 공통 코루틴 (폰/비숍 Phase 2에서 재사용)
    private IEnumerator MovePiece(ChessPieceController piece, GridPos bestMove)
    {
        if (bestMove.Equals(piece.currentGridPos)) yield break;

        CubeFace oldFace = piece.currentGridPos.face;
        piece.currentGridPos = bestMove;
        piece.CommitMove();

        Vector3 newNormal  = GridManager.Instance.GetNormalFromFace(bestMove.face);
        Vector3 targetPos  = GridManager.Instance.GetLocalPosition(newNormal, bestMove.x, bestMove.y, piece.board);
        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, newNormal);

        piece.isSpawning = true;
        piece.transform.DOKill();

        // 비숍은 여러 칸 이동이므로 거리 비례 속도 적용
        bool isBishop = piece.enemyType == ChessPieceController.EnemyType.Bishop;
        if (oldFace == bestMove.face)
        {
            float dist = isBishop
                ? Mathf.Abs(bestMove.x - (piece.turnStartGridPos.x)) + Mathf.Abs(bestMove.y - (piece.turnStartGridPos.y))
                : 1f;
            float dur = isBishop ? Mathf.Clamp(dist * 0.07f, 0.25f, 0.6f) : 0.4f;
            piece.transform.DOLocalMove(targetPos, dur).SetEase(Ease.OutCubic);
            piece.transform.DOLocalRotateQuaternion(targetRot, dur);
            yield return new WaitForSeconds(dur + 0.05f);
        }
        else
        {
            piece.transform.DOLocalJump(targetPos, 0.2f, 1, 0.4f).SetEase(Ease.OutCubic);
            piece.transform.DOLocalRotateQuaternion(targetRot, 0.4f);
            yield return new WaitForSeconds(0.45f);
        }

        piece.InitializeAfterSpawn(targetPos, targetRot);
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
            player.RocketLaunchAndDestroy();
            yield return new WaitForSeconds(2.0f);
        }

        BoardRotator rotator = FindObjectOfType<BoardRotator>();
        if (rotator != null)
        {
            rotator.ResetRotation(1.5f);
            yield return new WaitForSeconds(1.5f);
        }

        // [팀원 추가] 스테이지 클리어 시 플레이어 HP 전체 회복
        PlayerStats.Instance?.FullHeal();

        currentStage++;
        ChangeState(GameState.SpawningPieces);
    }

    private IEnumerator GameOverRoutine()
    {
        Debug.Log("=== 게임 오버! ===");

        // 게임 오버 UI 표시
        if (StageManager.Instance != null)
            StageManager.Instance.ShowGameOverUI();

        yield return new WaitForSeconds(3.0f);

        // 남은 기물 전부 정리
        ChessPieceController[] allPieces = FindObjectsByType<ChessPieceController>(FindObjectsSortMode.None);
        foreach (var p in allPieces)
        {
            if (p != null) Destroy(p.gameObject);
        }

        // 게임 오버 UI 숨기기
        if (StageManager.Instance != null)
            StageManager.Instance.HideGameOverUI();

        yield return new WaitForSeconds(0.5f);

        // 스테이지 1부터 재시작
        currentStage = 1;
        ChangeState(GameState.SpawningPieces);
    }

    // ── 적 전원 처치 확인 ────────────────────────────────────────
    // [팀원 추가] CheckAfterAttack()에서 호출되는 유틸리티 메서드

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
