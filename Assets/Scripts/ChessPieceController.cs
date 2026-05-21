using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class ChessPieceController : MonoBehaviour
{
    [Header("Control Settings")]
    [Tooltip("체크 해제 시 기존 마우스 방식으로 조작할 수 있습니다.")]
    public bool useHandTracking = true;
    [Tooltip("플레이어의 기물인지 여부 (체크 시 턴과 경로 시각화 적용)")]
    public bool isPlayerPiece = false;

    [HideInInspector]
    public GridPos currentGridPos;
    [HideInInspector]
    public GridPos turnStartGridPos;
    private List<GridPos> validMoves = new List<GridPos>();

    [Header("Combat Settings")]
    public int maxHP = 1;
    public int currentHP = 1;
    public int attackPower = 1;

    public enum EnemyType { Pawn, Bishop }
    [Tooltip("적 기물 종류 (플레이어 기물에는 무시됨)")]
    public EnemyType enemyType = EnemyType.Pawn;

    [Header("Combat Effects (Prefabs)")]
    public GameObject hitEffectPrefab;
    public GameObject slamEffectPrefab; // 플레이어 전용 (내려찍기 이펙트)

    [Header("Board Settings")]
    [Tooltip("체스 판 정육면체 오브젝트")]
    public Transform board;

    [Header("Grid Settings")]
    public float cellSize = 0.48f;
    public float gridLimit = 1.68f;

    [Header("Hover & Selection Settings")]
    [Tooltip("선택/호버 시 커지는 배율")]
    public float hoverScaleMultiplier = 1.3f;
    public float hoverHeight = 0.5f;
    public float dropSpeed = 15f;
    public float scaleSpeed = 10f;

    [Header("Squish Settings")]
    public float squishScaleY = 0.2f;
    private bool isSquished = false;

    [Header("Outline Settings")]
    [Tooltip("외곽선 색상")]
    public Color outlineColor = new Color(0f, 1f, 1f, 1f);
    [Tooltip("외곽선의 두께")]
    public float outlineWidth = 0.002f;

    [Header("Hover Visuals")]
    public Transform hoverIndicator;

    private bool isDragging = false;
    private bool isHovered = false;

    private Vector3 baseLocalPosition;
    private Vector3 currentLocalNormal;
    private Quaternion targetLocalRotation;

    private Collider pieceCollider;
    private Vector3 originalScale;
    private GameObject outlineObject;

    [HideInInspector]
    public bool isSpawning = false;

    // ── 초기화 ─────────────────────────────────────────────────────

    void Start()
    {
        pieceCollider = GetComponent<Collider>();
        originalScale = transform.localScale;
        currentHP = maxHP;

        if (board != null)
        {
            transform.SetParent(board, true);
            InitializeSnap();

            if (hoverIndicator != null)
            {
                hoverIndicator.SetParent(board, true);
                hoverIndicator.gameObject.SetActive(false);
            }
        }

        CreateOutlineObject();
    }

    private void CreateOutlineObject()
    {
        MeshFilter mf = GetComponentInChildren<MeshFilter>();
        if (mf != null)
        {
            outlineObject = new GameObject("Outline");
            outlineObject.transform.SetParent(mf.transform, false);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one;

            MeshFilter outlineMf = outlineObject.AddComponent<MeshFilter>();
            outlineMf.sharedMesh = mf.sharedMesh;

            MeshRenderer outlineMr = outlineObject.AddComponent<MeshRenderer>();
            Shader outlineShader = Shader.Find("Custom/SimpleOutline");
            if (outlineShader != null)
            {
                Material outlineMat = new Material(outlineShader);
                outlineMat.SetColor("_OutlineColor", outlineColor);
                outlineMat.SetFloat("_OutlineWidth", outlineWidth);
                outlineMr.sharedMaterial = outlineMat;
            }
            else
            {
                Debug.LogWarning("SimpleOutline 쉐이더를 찾을 수 없습니다.");
            }
            outlineObject.SetActive(false);
        }
    }

    // ── 호버 / 드래그 인터페이스 ────────────────────────────────────

    public void Hover()
    {
        isHovered = true;
        if (outlineObject != null) outlineObject.SetActive(true);

        if (isPlayerPiece && GameManager.Instance != null &&
            GameManager.Instance.currentState == GameState.PlayerTurn && !isDragging)
        {
            validMoves = GetValidMoves();
            if (PathVisualizer.Instance != null) PathVisualizer.Instance.ShowPath(validMoves, board);
        }
    }

    public void Unhover()
    {
        isHovered = false;
        if (outlineObject != null) outlineObject.SetActive(false);

        if (!isDragging && PathVisualizer.Instance != null)
            PathVisualizer.Instance.ClearPath();
    }

    public void Grab()
    {
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.PlayerAttacking) return;
        if (!isPlayerPiece) return;
        if (GameManager.Instance != null && GameManager.Instance.currentState != GameState.PlayerTurn) return;

        isDragging = true;
        if (pieceCollider != null) pieceCollider.enabled = false;

        validMoves = GetValidMoves();
        if (PathVisualizer.Instance != null) PathVisualizer.Instance.ShowPath(validMoves, board);
    }

    public void Drop()
    {
        if (!isDragging) return;
        isDragging = false;
        if (pieceCollider != null) pieceCollider.enabled = true;

        if (PathVisualizer.Instance != null) PathVisualizer.Instance.ClearPath();

        if (isPlayerPiece)
        {
            CubeFace dropFace; int dropX, dropY;
            GridManager.Instance.GetGridCoordinate(currentLocalNormal, baseLocalPosition, out dropFace, out dropX, out dropY);
            GridPos dropPos = new GridPos(dropFace, dropX, dropY);

            if (validMoves.Contains(dropPos))
            {
                currentGridPos = dropPos;
                baseLocalPosition = GridManager.Instance.GetLocalPosition(currentLocalNormal, dropX, dropY, board);
            }
            else
            {
                currentGridPos = turnStartGridPos;
                currentLocalNormal = GridManager.Instance.GetNormalFromFace(turnStartGridPos.face);
                baseLocalPosition = GridManager.Instance.GetLocalPosition(currentLocalNormal, turnStartGridPos.x, turnStartGridPos.y, board);
                targetLocalRotation = Quaternion.FromToRotation(Vector3.up, currentLocalNormal);
            }
        }
    }

    public void UpdateBoardHit(RaycastHit[] hits)
    {
        if (board == null) return;

        foreach (var hit in hits)
        {
            if (hit.transform == board)
            {
                Vector3 localHitPoint = board.InverseTransformPoint(hit.point);
                currentLocalNormal = board.InverseTransformDirection(hit.normal).normalized;
                currentLocalNormal = new Vector3(Mathf.Round(currentLocalNormal.x), Mathf.Round(currentLocalNormal.y), Mathf.Round(currentLocalNormal.z));
                baseLocalPosition = CalculateSnapPosition(localHitPoint, currentLocalNormal);
                targetLocalRotation = Quaternion.FromToRotation(Vector3.up, currentLocalNormal);
                break;
            }
        }
    }

    // ── 마우스 입력 (useHandTracking = false) ───────────────────────

    void OnMouseEnter() { if (!useHandTracking) Hover(); }
    void OnMouseExit()  { if (!useHandTracking) Unhover(); }

    // ── Update ──────────────────────────────────────────────────────

    void Update()
    {
        if (isSpawning) return;

        // 1. 마우스 조작
        if (!useHandTracking)
        {
            if (Input.GetMouseButtonDown(0) && isHovered) Grab();
            else if (Input.GetMouseButtonUp(0) && isDragging) Drop();

            if (isDragging)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray);
                UpdateBoardHit(hits);
            }
        }

        // 2. 외곽선 실시간 업데이트
        if (outlineObject != null && outlineObject.activeSelf)
        {
            MeshRenderer mr = outlineObject.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null)
            {
                mr.sharedMaterial.SetColor("_OutlineColor", outlineColor);
                mr.sharedMaterial.SetFloat("_OutlineWidth", outlineWidth);
            }
        }

        // 3. Squish 판정
        if (!isPlayerPiece)
        {
            ChessPieceController player = null;
            foreach (var p in FindObjectsByType<ChessPieceController>(FindObjectsSortMode.None))
                if (p.isPlayerPiece) { player = p; break; }

            if (player != null)
            {
                bool shouldSquish = !player.GetIsDragging() &&
                                    player.currentGridPos.Equals(this.currentGridPos) &&
                                    GameManager.Instance != null &&
                                    GameManager.Instance.currentState == GameState.PlayerTurn;

                if (shouldSquish && !isSquished)  { isSquished = true;  SetAlpha(0.5f); }
                else if (!shouldSquish && isSquished) { isSquished = false; SetAlpha(1.0f); }
            }
        }

        // 4. 스케일링
        float targetScaleMultiplier = (isHovered || isDragging) ? hoverScaleMultiplier : 1.0f;
        Vector3 targetScaleVec = originalScale * targetScaleMultiplier;
        if (isSquished) targetScaleVec.y = originalScale.y * squishScaleY;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScaleVec, Time.deltaTime * scaleSpeed);

        // 5. 이동/회전
        if (isDragging)
        {
            transform.localPosition = baseLocalPosition + (currentLocalNormal * hoverHeight);
            transform.localRotation = targetLocalRotation;
            if (hoverIndicator != null)
            {
                hoverIndicator.gameObject.SetActive(true);
                hoverIndicator.localPosition = baseLocalPosition + (currentLocalNormal * 0.01f);
                hoverIndicator.localRotation = targetLocalRotation;
            }
        }
        else
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, baseLocalPosition, Time.deltaTime * dropSpeed);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRotation, Time.deltaTime * dropSpeed);
            if (hoverIndicator != null) hoverIndicator.gameObject.SetActive(false);
        }
    }

    // ── 그리드 스냅 ─────────────────────────────────────────────────

    private void InitializeSnap()
    {
        Vector3 localPos = transform.localPosition;
        float absX = Mathf.Abs(localPos.x), absY = Mathf.Abs(localPos.y), absZ = Mathf.Abs(localPos.z);

        if      (absX >= absY && absX >= absZ) currentLocalNormal = new Vector3(Mathf.Sign(localPos.x), 0, 0);
        else if (absY >= absX && absY >= absZ) currentLocalNormal = new Vector3(0, Mathf.Sign(localPos.y), 0);
        else                                   currentLocalNormal = new Vector3(0, 0, Mathf.Sign(localPos.z));

        baseLocalPosition   = CalculateSnapPosition(localPos, currentLocalNormal);
        targetLocalRotation = Quaternion.FromToRotation(Vector3.up, currentLocalNormal);
        transform.localPosition = baseLocalPosition;
        transform.localRotation = targetLocalRotation;
    }

    private Vector3 CalculateSnapPosition(Vector3 localPoint, Vector3 localNormal)
    {
        Vector3 snapped = localPoint;
        if (localNormal.x == 0) snapped.x = GetGridSnap(localPoint.x);
        if (localNormal.y == 0) snapped.y = GetGridSnap(localPoint.y);
        if (localNormal.z == 0) snapped.z = GetGridSnap(localPoint.z);
        return snapped;
    }

    private float GetGridSnap(float value)
    {
        float snapped = (Mathf.Floor(value / cellSize) + 0.5f) * cellSize;
        return Mathf.Clamp(snapped, -gridLimit, gridLimit);
    }

    public void InitializeAfterSpawn(Vector3 targetLocalPos, Quaternion targetLocalRot)
    {
        isSpawning = false;
        baseLocalPosition   = targetLocalPos;
        targetLocalRotation = targetLocalRot;

        currentLocalNormal = targetLocalRot * Vector3.up;
        currentLocalNormal = new Vector3(
            Mathf.Round(currentLocalNormal.x),
            Mathf.Round(currentLocalNormal.y),
            Mathf.Round(currentLocalNormal.z));

        CubeFace face; int x, y;
        GridManager.Instance.GetGridCoordinate(currentLocalNormal, baseLocalPosition, out face, out x, out y);
        currentGridPos  = new GridPos(face, x, y);
        turnStartGridPos = currentGridPos;
    }

    public void CommitMove()
    {
        turnStartGridPos = currentGridPos;
    }

    // ── 이동 가능 경로 계산 (업그레이드 반영) ──────────────────────
    // 기본(룩): 상하좌우 직선 + 가장자리 1칸 면 이동
    // 퀸 승급:  대각선 4방향 추가
    // 면 이동 해제: 모든 방향에서 면 경계 넘기 허용

    public List<GridPos> GetValidMoves()
    {
        List<GridPos> moves = new List<GridPos>();

        bool isQueen      = isPlayerPiece && PlayerStats.Instance != null && PlayerStats.Instance.isQueen;
        bool freeFaceMove = isPlayerPiece && PlayerStats.Instance != null && PlayerStats.Instance.isFreeFaceMove;

        int[][] cardDirs = new int[][] { new int[]{1,0}, new int[]{-1,0}, new int[]{0,1}, new int[]{0,-1} };
        int[][] diagDirs = new int[][] { new int[]{1,1}, new int[]{1,-1}, new int[]{-1,1}, new int[]{-1,-1} };

        List<int[]> allDirs = new List<int[]>(cardDirs);
        if (isQueen) allDirs.AddRange(diagDirs);

        ChessPieceController[] allPieces = FindObjectsByType<ChessPieceController>(FindObjectsSortMode.None);

        foreach (var d in allDirs)
        {
            GridPos cur = turnStartGridPos;
            for (int i = 1; i <= 7; i++)
            {
                int nx = cur.x + d[0];
                int ny = cur.y + d[1];
                bool withinFace = nx >= 0 && nx <= 7 && ny >= 0 && ny <= 7;

                GridPos checkPos;
                if (withinFace)
                {
                    checkPos = new GridPos(cur.face, nx, ny);
                }
                else if (freeFaceMove || i == 1)
                {
                    // 면 경계 넘기 (면 전환 후 좌표계가 바뀌므로 더 이상 탐색 불가)
                    checkPos = GridManager.Instance.GetNeighbor(cur, d[0], d[1]);
                    moves.Add(checkPos);
                    break;
                }
                else
                {
                    break; // 면 이동 해제 없으면 모서리에서 탐색 종료
                }

                moves.Add(checkPos);

                // 가로막힘 검사
                bool isBlocked = false;
                foreach (var p in allPieces)
                {
                    if (p != this && p != null && p.gameObject.activeSelf && p.currentGridPos.Equals(checkPos))
                    { isBlocked = true; break; }
                }
                if (isBlocked) break;

                cur = checkPos;
            }
        }
        return moves;
    }

    // ── 전투 메서드 ─────────────────────────────────────────────────

    public void TakeDamage(int damage, Vector3 attackSourceLocalPos)
    {
        // HP 처리: 플레이어는 PlayerStats에 위임 (HP 바 갱신 + 사망 이벤트)
        if (isPlayerPiece)
        {
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.TakeDamage(damage);
                currentHP = PlayerStats.Instance.currentHp; // 로컬 필드 동기화
            }
            else
            {
                currentHP -= damage;
            }
        }
        else
        {
            currentHP -= damage;
        }

        // 피격 이펙트
        if (hitEffectPrefab != null)
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);

        // 넉백 방향
        Vector3 pushDir = (transform.localPosition - attackSourceLocalPos).normalized;
        pushDir -= Vector3.Project(pushDir, currentLocalNormal);
        pushDir.Normalize();
        Vector3 punchPos = baseLocalPosition + pushDir * 0.3f;

        isSpawning = true;
        transform.DOKill();

        if (currentHP <= 0)
        {
            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOLocalMove(punchPos, 0.2f).SetEase(Ease.OutExpo));

            if (isPlayerPiece)
            {
                // 플레이어 사망: PlayerStats.onDead → GameManager.GameOver 가 처리함
                // 여기서는 넉백 연출만 수행
                seq.AppendInterval(0.3f);
                seq.OnComplete(() => isSpawning = false);
            }
            else
            {
                // 적 사망: 페이드 아웃 후 파괴
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                    if (r.material.HasProperty("_Color"))
                        r.material.DOFade(0f, 0.5f);

                seq.AppendInterval(0.5f);
                seq.OnComplete(() => Destroy(gameObject));
            }
        }
        else
        {
            // 생존: 넉백 후 복귀
            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOLocalMove(punchPos, 0.2f).SetEase(Ease.OutExpo));
            seq.Append(transform.DOLocalMove(baseLocalPosition, 0.2f).SetEase(Ease.OutBack));
            seq.OnComplete(() => isSpawning = false);
        }
    }

    // 플레이어에게 정수리를 밟혔을 때 즉사하는 연출
    public void TakeFatalDamage()
    {
        currentHP = 0;
        isSpawning = true;
        transform.DOKill();

        if (hitEffectPrefab != null)
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOScaleY(0f, 0.1f).SetEase(Ease.InExpo));
        seq.OnComplete(() => Destroy(gameObject));
    }

    public bool GetIsDragging() => isDragging;

    private void SetAlpha(float alpha)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (outlineObject != null && r.gameObject == outlineObject) continue;
            if (r.material.HasProperty("_Color"))
                r.material.DOFade(alpha, 0.2f);
        }
    }

    // 스테이지 클리어 시 솟구쳐 오르며 사라지는 연출
    public void RocketLaunchAndDestroy()
    {
        isSpawning = true;
        if (pieceCollider != null) pieceCollider.enabled = false;
        transform.DOKill();
        Vector3 targetPos = baseLocalPosition + (currentLocalNormal * 20f);
        transform.DOLocalMove(targetPos, 1.5f).SetEase(Ease.InExpo).OnComplete(() => Destroy(gameObject));
    }

    // 폰/비숍 박치기 공격 애니메이션
    public void PerformAttackAnimation(Vector3 playerLocalPos)
    {
        isSpawning = true;
        transform.DOKill();

        Vector3 myPos = baseLocalPosition;
        Vector3 toPlayer = playerLocalPos - myPos;
        toPlayer -= Vector3.Project(toPlayer, currentLocalNormal);
        toPlayer.Normalize();

        Vector3 liftPos   = myPos + currentLocalNormal * 0.35f;
        Vector3 windupPos = liftPos - toPlayer * 0.3f;
        Vector3 strikePos = myPos + toPlayer * 0.45f + currentLocalNormal * 0.1f;

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOLocalMove(liftPos,   0.18f).SetEase(Ease.OutQuad));
        seq.Append(transform.DOLocalMove(windupPos, 0.15f).SetEase(Ease.OutQuad));
        seq.Append(transform.DOLocalMove(strikePos, 0.10f).SetEase(Ease.InExpo));
        seq.AppendInterval(0.05f);
        seq.Append(transform.DOLocalMove(myPos,     0.22f).SetEase(Ease.OutBack));
        seq.OnComplete(() => isSpawning = false);
    }
}
