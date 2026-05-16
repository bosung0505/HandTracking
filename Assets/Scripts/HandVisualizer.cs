using UnityEngine;

public class HandVisualizer : MonoBehaviour
{
    [Header("참조")]
    public UDPReceiver udpReceiver;
    public Animator handAnimator;

    [Tooltip("체크하면 두 번째 손(index 5~9) 데이터를 사용합니다.")]
    public bool isSecondHand = false;

    [Header("조작감 - 가상 바운딩 박스")]
    [Tooltip("손의 이동 범위를 증폭시킵니다. (예: 1.5 ~ 2.0)")]
    public float handSensitivityX = 2.0f;
    public float handSensitivityY = 2.0f;

    [Header("조작감 - 동적 스무딩 (에임 안정화)")]
    [Tooltip("에임 정밀도를 위해 움직임이 적을 때는 부드럽게, 클 때는 빠르게 따라옵니다.")]
    public float slowSmoothSpeed = 20f;
    public float fastSmoothSpeed = 10f;
    public float distanceFromCamera = 5f;

    [Header("스케일(나타남/사라짐) 설정")]
    public float scaleSmoothSpeed = 10f;
    private Vector3 initialScale;

    [Header("애니메이션 민감도 (Debounce)")]
    public float poseDebounceTime = 0.15f;

    private bool currentPinch;
    private bool currentFist;
    private bool currentPinchReady;

    private float pinchTimer;
    private float fistTimer;
    private float pinchReadyTimer;

    [Header("레이저 포인터 및 상호작용")]
    public bool useLaserPointer = true;
    private LineRenderer laserPointer;

    // 상호작용 관리
    private ChessPieceController hoveredPiece;
    private ChessPieceController grabbedPiece;

    void Start()
    {
        if (udpReceiver == null) udpReceiver = FindObjectOfType<UDPReceiver>();
        if (handAnimator == null) handAnimator = GetComponent<Animator>();

        initialScale = transform.localScale;
        transform.localScale = Vector3.zero;

        if (useLaserPointer)
        {
            laserPointer = GetComponent<LineRenderer>();
            if (laserPointer == null) laserPointer = gameObject.AddComponent<LineRenderer>();

            laserPointer.startWidth = 0.02f;
            laserPointer.endWidth = 0.01f;
            laserPointer.material = new Material(Shader.Find("Sprites/Default"));
            laserPointer.startColor = Color.cyan;
            laserPointer.endColor = new Color(0, 1, 1, 0);
            laserPointer.enabled = false;
        }
    }

    void Update()
    {
        if (udpReceiver == null || udpReceiver.data == null || udpReceiver.data.Length < 10) return;

        int offset = isSecondHand ? 5 : 0;

        float handX = udpReceiver.data[offset + 0];
        float handY = udpReceiver.data[offset + 1];
        
        bool rawPinching = udpReceiver.data[offset + 2] > 0.5f;
        bool rawFist = udpReceiver.data[offset + 3] > 0.5f;
        bool rawPinchReady = udpReceiver.data[offset + 4] > 0.5f;

        // --- 포즈 민감도 (Debounce) ---
        if (rawPinching != currentPinch)
        {
            pinchTimer += Time.deltaTime;
            if (pinchTimer >= poseDebounceTime) { currentPinch = rawPinching; pinchTimer = 0f; }
        }
        else pinchTimer = 0f;

        if (rawFist != currentFist)
        {
            fistTimer += Time.deltaTime;
            if (fistTimer >= poseDebounceTime) { currentFist = rawFist; fistTimer = 0f; }
        }
        else fistTimer = 0f;

        if (rawPinchReady != currentPinchReady)
        {
            pinchReadyTimer += Time.deltaTime;
            if (pinchReadyTimer >= poseDebounceTime) { currentPinchReady = rawPinchReady; pinchReadyTimer = 0f; }
        }
        else pinchReadyTimer = 0f;

        // 화면 안에 손이 있는지 판별
        bool isVisible = (handX != 0 || handY != 0);
        Vector3 targetScale = isVisible ? initialScale : Vector3.zero;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSmoothSpeed);

        if (isVisible)
        {
            if (handAnimator != null)
            {
                handAnimator.SetBool("isPinching", currentPinch);
                handAnimator.SetBool("isFist", currentFist);
                handAnimator.SetBool("isPinchReady", currentPinchReady);
            }

            if (Camera.main != null)
            {
                // 가상 바운딩 박스: 0.5를 중심으로 이동폭 증폭
                float mappedX = (handX - 0.5f) * handSensitivityX + 0.5f;
                float mappedY = (handY - 0.5f) * handSensitivityY + 0.5f;
                // 화면 밖으로 나가지 않도록 제한
                mappedX = Mathf.Clamp01(mappedX);
                mappedY = Mathf.Clamp01(mappedY);

                Vector3 screenPos = new Vector3(mappedX * Screen.width, (1f - mappedY) * Screen.height, distanceFromCamera);
                Vector3 targetWorldPos = Camera.main.ScreenToWorldPoint(screenPos);

                // 동적 스무딩 (에임 안정화)
                float dist = Vector3.Distance(transform.position, targetWorldPos);
                // 거리가 멀면 빠르게(fastSmoothSpeed), 거리가 가까우면 부드럽게(slowSmoothSpeed)
                float currentSmoothSpeed = Mathf.Lerp(slowSmoothSpeed, fastSmoothSpeed, Mathf.Clamp01(dist * 2f));

                transform.position = Vector3.Lerp(transform.position, targetWorldPos, Time.deltaTime * currentSmoothSpeed);

                // --- 상호작용 로직 ---
                HandleInteraction();
            }
        }
        else
        {
            // 리셋
            if (hoveredPiece != null) { hoveredPiece.Unhover(); hoveredPiece = null; }
            if (grabbedPiece != null) { grabbedPiece.Drop(); grabbedPiece = null; }
            if (useLaserPointer && laserPointer != null) laserPointer.enabled = false;
            
            currentPinch = false; currentFist = false; currentPinchReady = false;
        }
    }

    private void HandleInteraction()
    {
        Ray ray = new Ray(transform.position, Camera.main.transform.forward);
        ChessPieceController hitPiece = null;

        // 핀치나 핀치 레디 상태일 때만 레이캐스트 수행
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        
        if (currentPinchReady || currentPinch)
        {
            foreach (var hit in hits)
            {
                ChessPieceController piece = hit.collider.GetComponentInParent<ChessPieceController>();
                if (piece == null) piece = hit.collider.GetComponent<ChessPieceController>();
                if (piece != null) hitPiece = piece;
            }
        }

        // 1. 호버(조준) 로직
        if (currentPinchReady && !currentPinch && grabbedPiece == null)
        {
            if (hitPiece != hoveredPiece)
            {
                if (hoveredPiece != null) hoveredPiece.Unhover();
                hoveredPiece = hitPiece;
                if (hoveredPiece != null) hoveredPiece.Hover();
            }
        }
        else if (!currentPinchReady && !currentPinch)
        {
            if (hoveredPiece != null) { hoveredPiece.Unhover(); hoveredPiece = null; }
        }

        // 2. 잡기(Pinch) 및 이동 로직
        if (currentPinch)
        {
            // 잡는 순간
            if (grabbedPiece == null && hoveredPiece != null)
            {
                grabbedPiece = hoveredPiece;
                grabbedPiece.Grab();
            }

            // 들고 이동
            if (grabbedPiece != null)
            {
                grabbedPiece.UpdateBoardHit(hits);
            }
        }
        else
        {
            // 놓는 순간
            if (grabbedPiece != null)
            {
                grabbedPiece.Drop();
                grabbedPiece = null;
            }
        }

        // 3. 레이저 시각 효과
        if (useLaserPointer && laserPointer != null)
        {
            if (currentPinchReady || currentPinch)
            {
                laserPointer.enabled = true;
                laserPointer.SetPosition(0, transform.position);
                laserPointer.SetPosition(1, transform.position + Camera.main.transform.forward * 10f);
            }
            else
            {
                laserPointer.enabled = false;
            }
        }
    }
}