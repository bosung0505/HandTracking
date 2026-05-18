using UnityEngine;
using DG.Tweening;

public class BoardRotator : MonoBehaviour
{
    [Header("Hand Tracking Integration")]
    public UDPReceiver udpReceiver;
    public bool useHandTracking = true;
    public float handSensitivity = 50f;
    public float handZoomSensitivity = 30f;

    [Header("Anti-Jitter Settings")]
    public float positionFilterSpeed = 15f;
    public float deadzone = 0.002f;

    [Header("Rotation Settings")]
    public float rotationSpeed = 10f;
    public float smoothFactor = 10f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 20f;
    public float zoomSmoothFactor = 10f;
    public float minScale = 5f;
    public float maxScale = 50f;

    private bool isDragging = false;
    private Quaternion targetRotation;
    private float targetScale;
    private bool isZooming = false;
    private float lastZoomDistance = 0f;

    private Vector2 smoothedHandPos;
    private Vector2 lastHandPos;

    private Quaternion initialRotation;

    void Start()
    {
        initialRotation = transform.rotation;
        targetRotation = transform.rotation;
        targetScale = transform.localScale.x;
        if (udpReceiver == null) udpReceiver = FindObjectOfType<UDPReceiver>();
    }

    void Update()
    {
        // 스테이지 클리어 연출 중에는 조작 잠금
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.StageClear) return;

        float inputX = 0f;
        float inputY = 0f;
        float zoomInput = 0f;

        if (useHandTracking && udpReceiver != null)
        {
            // 데이터 수신 (안정성을 위해 임시 변수에 저장)
            float h1_x = udpReceiver.data[0];
            float h1_y = udpReceiver.data[1];
            bool h1_fist = udpReceiver.data[3] > 0.5f;

            float h2_x = udpReceiver.data[5];
            float h2_y = udpReceiver.data[6];
            bool h2_fist = udpReceiver.data[8] > 0.5f;

            bool bothFists = h1_fist && h2_fist;

            if (bothFists)
            {
                // [줌 모드]
                if (isDragging) isDragging = false;

                Vector2 pos1 = new Vector2(h1_x, h1_y);
                Vector2 pos2 = new Vector2(h2_x, h2_y);
                float currentDistance = Vector2.Distance(pos1, pos2);

                if (!isZooming)
                {
                    isZooming = true;
                    lastZoomDistance = currentDistance; // 줌 시작 시 거리 초기화
                }

                float deltaDist = currentDistance - lastZoomDistance;
                if (Mathf.Abs(deltaDist) > 0.005f)
                {
                    zoomInput = deltaDist * handZoomSensitivity;
                    lastZoomDistance = currentDistance;
                }
            }
            else if (h1_fist || h2_fist)
            {
                // [회전 모드]
                if (isZooming) isZooming = false;

                // 주먹 쥔 손 선택 (1번 우선)
                Vector2 rawHandPos = h1_fist ? new Vector2(h1_x, h1_y) : new Vector2(h2_x, h2_y);

                // 🔥 [핵심 수정] 주먹을 막 쥔 순간(첫 프레임) 처리
                if (!isDragging)
                {
                    isDragging = true;
                    // 필터링된 좌표와 마지막 좌표를 '현재 실제 손 위치'로 즉시 강제 워프!
                    smoothedHandPos = rawHandPos;
                    lastHandPos = rawHandPos;
                }

                // 부드러운 필터링 적용
                smoothedHandPos = Vector2.Lerp(smoothedHandPos, rawHandPos, Time.deltaTime * positionFilterSpeed);

                // 변화량 계산
                Vector2 delta = smoothedHandPos - lastHandPos;

                if (delta.magnitude >= deadzone)
                {
                    inputX = delta.x * handSensitivity * rotationSpeed;
                    inputY = (lastHandPos.y - smoothedHandPos.y) * handSensitivity * rotationSpeed;
                    lastHandPos = smoothedHandPos;
                }
            }
            else
            {
                // [대기 모드] 주먹을 모두 풀었을 때
                isDragging = false;
                isZooming = false;
            }
        }
        else
        {
            // --- 기존 마우스 로직 ---
            if (Input.GetMouseButtonDown(1)) isDragging = true;
            else if (Input.GetMouseButtonUp(1)) isDragging = false;

            if (isDragging)
            {
                inputX = Input.GetAxis("Mouse X") * rotationSpeed;
                inputY = Input.GetAxis("Mouse Y") * rotationSpeed;
            }
            zoomInput = Input.GetAxis("Mouse ScrollWheel");
        }

        // 회전/줌 적용 로직 (기존과 동일)
        ApplyTransformation(inputX, inputY, zoomInput);
    }

    void ApplyTransformation(float x, float y, float zoom)
    {
        if (isDragging && (x != 0 || y != 0))
        {
            Vector3 upAxis = Camera.main != null ? Camera.main.transform.up : Vector3.up;
            Vector3 rightAxis = Camera.main != null ? Camera.main.transform.right : Vector3.right;

            Quaternion yRot = Quaternion.AngleAxis(-x, upAxis);
            Quaternion xRot = Quaternion.AngleAxis(y, rightAxis);
            targetRotation = xRot * yRot * targetRotation;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * smoothFactor);

        if (Mathf.Abs(zoom) > 0.001f)
        {
            targetScale += zoom * zoomSpeed;
            targetScale = Mathf.Clamp(targetScale, minScale, maxScale);
        }
        float currentScale = Mathf.Lerp(transform.localScale.x, targetScale, Time.deltaTime * zoomSmoothFactor);
        transform.localScale = new Vector3(currentScale, currentScale, currentScale);
    }

    public void ResetRotation(float duration)
    {
        isDragging = false;
        isZooming = false;
        targetRotation = initialRotation;
        
        transform.DOKill();
        transform.DORotateQuaternion(initialRotation, duration).SetEase(Ease.InOutQuad);
    }
}