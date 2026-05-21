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
    public float scaleSpeed = 10f; // 부드러운 스케일링을 위한 속도

    [Header("Squish Settings")]
    public float squishScaleY = 0.2f;
    private bool isSquished = false;

    [Header("Outline Settings")]
    [Tooltip("외곽선 색상")]
    public Color outlineColor = new Color(0f, 1f, 1f, 1f); // 기본 청록색
    [Tooltip("외곽선의 두께 (크게 커지는 현상 방지를 위해 아주 작은 값으로 설정)")]
    public float outlineWidth = 0.002f;

    [Header("Hover Visuals")]
    public Transform hoverIndicator;

    private bool isDragging = false;
    private bool isHovered = false; // 레이저/마우스에 닿았는지 여부
    
    private Vector3 baseLocalPosition;
    private Vector3 currentLocalNormal;
    private Quaternion targetLocalRotation;

    private Collider pieceCollider;
    private Vector3 originalScale;

    private GameObject outlineObject;

    [HideInInspector]
    public bool isSpawning = false;

    void Start()
    {
        pieceCollider = GetComponent<Collider>();
        originalScale = transform.localScale;
        currentHP = maxHP; // 스폰 시 최대 체력으로 초기화

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

        // 외곽선을 표시할 자식 오브젝트를 동적으로 생성
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

    public void Hover()
    {
        isHovered = true;
        if (outlineObject != null) outlineObject.SetActive(true);

        if (isPlayerPiece && GameManager.Instance != null && GameManager.Instance.currentState == GameState.PlayerTurn && !isDragging)
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
        {
            PathVisualizer.Instance.ClearPath();
        }
    }

    public void Grab()
    {
        // 공격 등 특정 상태에서는 입력 차단
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.PlayerAttacking) return;
        
        // 적군 기물이면 잡지 못함
        if (!isPlayerPiece) return;
        // 턴이 아니면 잡지 못함
        if (GameManager.Instance != null && GameManager.Instance.currentState != GameState.PlayerTurn) return;

        isDragging = true;
        if(pieceCollider != null) pieceCollider.enabled = false;

        if (isPlayerPiece)
        {
            validMoves = GetValidMoves();
            if (PathVisualizer.Instance != null) PathVisualizer.Instance.ShowPath(validMoves, board);
        }
    }

    public void Drop()
    {
        if (!isDragging) return;
        isDragging = false;
        if(pieceCollider != null) pieceCollider.enabled = true;

        if (PathVisualizer.Instance != null) PathVisualizer.Instance.ClearPath();

        if (isPlayerPiece)
        {
            // 방금 떨어뜨린 위치의 논리 좌표 계산
            CubeFace dropFace; int dropX, dropY;
            GridManager.Instance.GetGridCoordinate(currentLocalNormal, baseLocalPosition, out dropFace, out dropX, out dropY);
            GridPos dropPos = new GridPos(dropFace, dropX, dropY);

            if (validMoves.Contains(dropPos))
            {
                // 이동 성공 (가승인): 임시로 논리 좌표 갱신 후 스냅 (END 전까지는 확정 아님)
                currentGridPos = dropPos;
                baseLocalPosition = GridManager.Instance.GetLocalPosition(currentLocalNormal, dropX, dropY, board);
            }
            else
            {
                // 이동 취소: 원래 턴을 시작했던 타일로 완전히 되돌아감
                currentGridPos = turnStartGridPos;
                currentLocalNormal = GridManager.Instance.GetNormalFromFace(turnStartGridPos.face);
                baseLocalPosition = GridManager.Instance.GetLocalPosition(currentLocalNormal, turnStartGridPos.x, turnStartGridPos.y, board);
                targetLocalRotation = Quaternion.FromToRotation(Vector3.up, currentLocalNormal);
            }
        }
    }

    // 잡고 있는 도중 매 프레임 위치 업데이트
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

    // --- 마우스 로직 (useHandTracking = false 일 때만 작동) ---
    void OnMouseEnter()
    {
        if (!useHandTracking) Hover();
    }

    void OnMouseExit()
    {
        if (!useHandTracking) Unhover();
    }

    void Update()
    {
        if (isSpawning) return; // 스폰(점프) 애니메이션 중에는 로컬 제어를 중지합니다.

        // 1. 마우스 조작 (핸드 트래킹이 꺼져있을 때)
        if (!useHandTracking)
        {
            if (Input.GetMouseButtonDown(0) && isHovered)
            {
                Grab();
            }
            else if (Input.GetMouseButtonUp(0) && isDragging)
            {
                Drop();
            }

            if (isDragging)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray);
                UpdateBoardHit(hits);
            }
        }

        // 2. 실시간 외곽선 설정 업데이트 (플레이 중 인스펙터 수정 반영)
        if (outlineObject != null && outlineObject.activeSelf)
        {
            MeshRenderer mr = outlineObject.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null)
            {
                mr.sharedMaterial.SetColor("_OutlineColor", outlineColor);
                mr.sharedMaterial.SetFloat("_OutlineWidth", outlineWidth);
            }
        }

        // [신규] 3. 찌부러짐(Squish) 판정: 턴 확정 전 플레이어가 밟았을 때
        if (!isPlayerPiece && !isSpawning)
        {
            ChessPieceController player = null;
            ChessPieceController[] allPieces = FindObjectsByType<ChessPieceController>(FindObjectsSortMode.None);
            foreach(var p in allPieces) if (p.isPlayerPiece) player = p;

            if (player != null)
            {
                // 드래그 중이 아니며, 플레이어 턴이고, 좌표가 겹친다면
                bool shouldSquish = (!player.GetIsDragging() && player.currentGridPos.Equals(this.currentGridPos) && GameManager.Instance != null && GameManager.Instance.currentState == GameState.PlayerTurn);
                
                if (shouldSquish && !isSquished)
                {
                    isSquished = true;
                    SetAlpha(0.5f);
                }
                else if (!shouldSquish && isSquished)
                {
                    isSquished = false;
                    SetAlpha(1.0f);
                }
            }
        }

        // 4. 부드러운 스케일링 로직 (hoverScaleMultiplier 및 Squish 적용)
        float targetScaleMultiplier = (isHovered || isDragging) ? hoverScaleMultiplier : 1.0f;
        Vector3 targetScaleVec = originalScale * targetScaleMultiplier;
        if (isSquished) targetScaleVec.y = originalScale.y * squishScaleY; // Y축만 납작하게 덮어씌움
        
        transform.localScale = Vector3.Lerp(transform.localScale, targetScaleVec, Time.deltaTime * scaleSpeed);

        // 5. 이동 및 회전 로직
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
            
            if (hoverIndicator != null)
            {
                hoverIndicator.gameObject.SetActive(false);
            }
        }
    }

    private void InitializeSnap()
    {
        Vector3 localPos = transform.localPosition;
        
        float absX = Mathf.Abs(localPos.x);
        float absY = Mathf.Abs(localPos.y);
        float absZ = Mathf.Abs(localPos.z);

        if (absX >= absY && absX >= absZ) currentLocalNormal = new Vector3(Mathf.Sign(localPos.x), 0, 0);
        else if (absY >= absX && absY >= absZ) currentLocalNormal = new Vector3(0, Mathf.Sign(localPos.y), 0);
        else currentLocalNormal = new Vector3(0, 0, Mathf.Sign(localPos.z));

        baseLocalPosition = CalculateSnapPosition(localPos, currentLocalNormal);
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
        baseLocalPosition = targetLocalPos;
        targetLocalRotation = targetLocalRot;
        
        currentLocalNormal = targetLocalRot * Vector3.up;
        currentLocalNormal = new Vector3(Mathf.Round(currentLocalNormal.x), Mathf.Round(currentLocalNormal.y), Mathf.Round(currentLocalNormal.z));

        // 스폰 완료 후 자신의 논리 좌표 확정
        CubeFace face; int x, y;
        GridManager.Instance.GetGridCoordinate(currentLocalNormal, baseLocalPosition, out face, out x, out y);
        currentGridPos = new GridPos(face, x, y);
        turnStartGridPos = currentGridPos;
    }

    public void CommitMove()
    {
        turnStartGridPos = currentGridPos;
    }

    // 룩(Rook)의 이동 가능 경로 계산
    public List<GridPos> GetValidMoves()
    {
        List<GridPos> moves = new List<GridPos>();
        int[][] dirs = new int[][] { new int[]{1,0}, new int[]{-1,0}, new int[]{0,1}, new int[]{0,-1} };
        
        ChessPieceController[] allPieces = FindObjectsByType<ChessPieceController>(FindObjectsSortMode.None);

        foreach(var d in dirs)
        {
            for (int i = 1; i <= 7; i++)
            {
                // 이동 가능 거리 계산은 현재 드래그 중인 위치가 아닌 '턴 시작 위치'를 기준으로 합니다!
                int nx = turnStartGridPos.x + d[0] * i;
                int ny = turnStartGridPos.y + d[1] * i;
                if (nx >= 0 && nx <= 7 && ny >= 0 && ny <= 7)
                {
                    GridPos checkPos = new GridPos(turnStartGridPos.face, nx, ny);
                    moves.Add(checkPos);

                    // 다른 기물에 가로막혀 있는지 검사 (딱 그 기물이 있는 위치까지만 이동 가능)
                    bool isBlocked = false;
                    foreach (var p in allPieces)
                    {
                        if (p != this && p != null && p.gameObject.activeSelf && p.currentGridPos.Equals(checkPos))
                        {
                            isBlocked = true;
                            break;
                        }
                    }

                    if (isBlocked) break; // 가로막혔으므로 더 이상 이 방향으로 전진 불가
                }
                else
                {
                    break; // 같은 면 내에서 모서리에 도달하면 직선 탐색 종료
                }
            }
            
            // 현재 기물이 해당 방향의 가장자리 끝칸에 위치해 있다면 (1칸 밖으로 나갈 때)
            if (turnStartGridPos.x + d[0] < 0 || turnStartGridPos.x + d[0] > 7 || 
                turnStartGridPos.y + d[1] < 0 || turnStartGridPos.y + d[1] > 7)
            {
                GridPos crossFace = GridManager.Instance.GetNeighbor(turnStartGridPos, d[0], d[1]);
                moves.Add(crossFace);
            }
        }
        return moves;
    }

    public void TakeDamage(int damage, Vector3 attackSourceLocalPos)
    {
        currentHP -= damage;

        // 피격 이펙트 스폰 (자신의 월드 위치)
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }

        // 넉백 방향 계산 (공격 출처에서 자신을 향하는 벡터, 로컬 평면 상에 투영)
        Vector3 pushDir = (transform.localPosition - attackSourceLocalPos).normalized;
        pushDir -= Vector3.Project(pushDir, currentLocalNormal); // 수직 성분 제거
        pushDir.Normalize();

        Vector3 punchPos = baseLocalPosition + pushDir * 0.3f; // 0.3f 만큼 밀려남

        isSpawning = true; // Update 루프의 보간 로직 멈춤
        transform.DOKill();

        if (currentHP <= 0)
        {
            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOLocalMove(punchPos, 0.2f).SetEase(Ease.OutExpo));

            if (isPlayerPiece)
            {
                // 플레이어 사망: 넉백 후 게임 오버 전환
                seq.AppendInterval(0.3f);
                seq.OnComplete(() =>
                {
                    GameManager.Instance.ChangeState(GameState.GameOver);
                });
            }
            else
            {
                // 적 사망: 페이드 아웃 후 파괴
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (r.material.HasProperty("_Color"))
                        r.material.DOFade(0f, 0.5f);
                }
                seq.AppendInterval(0.5f);
                seq.OnComplete(() => Destroy(gameObject));
            }
        }
        else
        {
            // 생존 시: 팍! 밀렸다가 다시 제자리로 돌아옴
            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOLocalMove(punchPos, 0.2f).SetEase(Ease.OutExpo));
            seq.Append(transform.DOLocalMove(baseLocalPosition, 0.2f).SetEase(Ease.OutBack));
            seq.OnComplete(() => {
                isSpawning = false; // 보간 로직 복구
            });
        }
    }

    // 플레이어에게 정수리를 밟혔을 때 즉사하는 연출
    public void TakeFatalDamage()
    {
        currentHP = 0;
        isSpawning = true; // Update 루프의 보간 강제 정지
        transform.DOKill(); // 기존 모든 애니메이션 즉시 정지

        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }

        // 종잇장처럼 순식간에 납작해지며 즉사
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOScaleY(0f, 0.1f).SetEase(Ease.InExpo));
        seq.OnComplete(() => {
            Destroy(gameObject);
        });
    }

    public bool GetIsDragging()
    {
        return isDragging;
    }

    private void SetAlpha(float alpha)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (outlineObject != null && r.gameObject == outlineObject) continue;
            
            if (r.material.HasProperty("_Color"))
            {
                r.material.DOFade(alpha, 0.2f);
            }
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

    // 폰 박치기 공격 애니메이션 (예비동작 → 박치기 → 복귀)
    public void PerformAttackAnimation(Vector3 playerLocalPos)
    {
        isSpawning = true;
        transform.DOKill();

        Vector3 myPos = baseLocalPosition;

        // 플레이어 방향 벡터 (큐브 면 평면에 투영)
        Vector3 toPlayer = playerLocalPos - myPos;
        toPlayer -= Vector3.Project(toPlayer, currentLocalNormal);
        toPlayer.Normalize();

        // 각 단계별 목표 위치
        Vector3 liftPos    = myPos + currentLocalNormal * 0.35f;           // 1. 살짝 위로 뜸
        Vector3 windupPos  = liftPos - toPlayer * 0.3f;                    // 2. 반대 방향으로 예비 동작
        Vector3 strikePos  = myPos + toPlayer * 0.45f + currentLocalNormal * 0.1f; // 3. 박치기!

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOLocalMove(liftPos,   0.18f).SetEase(Ease.OutQuad));  // 뜸
        seq.Append(transform.DOLocalMove(windupPos, 0.15f).SetEase(Ease.OutQuad));  // 예비 동작
        seq.Append(transform.DOLocalMove(strikePos, 0.10f).SetEase(Ease.InExpo));   // 박치기!
        seq.AppendInterval(0.05f);
        seq.Append(transform.DOLocalMove(myPos,     0.22f).SetEase(Ease.OutBack));  // 원위치 복귀
        seq.OnComplete(() => isSpawning = false);
    }
}
