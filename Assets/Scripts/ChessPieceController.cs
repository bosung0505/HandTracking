using UnityEngine;

public class ChessPieceController : MonoBehaviour
{
    [Header("Control Settings")]
    [Tooltip("체크 해제 시 기존 마우스 방식으로 조작할 수 있습니다.")]
    public bool useHandTracking = true;

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

    void Start()
    {
        pieceCollider = GetComponent<Collider>();
        originalScale = transform.localScale;

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

    // HandVisualizer 또는 마우스 이벤트에서 호출
    public void Hover()
    {
        isHovered = true;
        if (outlineObject != null) outlineObject.SetActive(true);
    }

    public void Unhover()
    {
        isHovered = false;
        if (outlineObject != null) outlineObject.SetActive(false);
    }

    public void Grab()
    {
        isDragging = true;
        if(pieceCollider != null) pieceCollider.enabled = false;
    }

    public void Drop()
    {
        isDragging = false;
        if(pieceCollider != null) pieceCollider.enabled = true;
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

        // 3. 부드러운 스케일링 로직 (hoverScaleMultiplier 적용)
        float targetScaleMultiplier = (isHovered || isDragging) ? hoverScaleMultiplier : 1.0f;
        Vector3 targetScale = originalScale * targetScaleMultiplier;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);

        // 3. 이동 및 회전 로직
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
}
