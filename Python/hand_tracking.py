import cv2
import mediapipe as mp
import socket
import math

print("🚨 파이썬이 읽고 있는 미디어파이프 위치:", mp.__file__)

# 1. UDP 통신 세팅
host, port = "127.0.0.1", 5052
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
serverAddressPort = (host, port)

# 2. 미디어파이프 세팅
mp_hands = mp.solutions.hands
hands = mp_hands.Hands(max_num_hands=2, min_detection_confidence=0.7)
mp_draw = mp.solutions.drawing_utils 

cap = cv2.VideoCapture(0)
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280) # 시야각 확장을 위한 HD 해상도
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
print("웹캠이 켜졌습니다. (종료하려면 화면 클릭 후 'q')")

def get_dist(lm1, lm2):
    return math.hypot(lm1.x - lm2.x, lm1.y - lm2.y)

# 💡 [현업 표준 방법론] 손가락 말림 비율(Curl Ratio) 계산 함수
# VR/AR 기기(Meta Quest 등)에서 주먹을 판별할 때 사용하는 방식입니다.
# 손목에서 손가락 끝(Tip)까지의 거리를, 손목에서 손가락 밑동(MCP)까지의 거리로 나눕니다.
# 쫙 펴면 비율이 1.5 이상으로 커지고, 꽉 쥐면 0.8 미만으로 확 줄어듭니다.
def get_curl_ratio(handLms, tip_idx, mcp_idx):
    wrist = handLms.landmark[0]
    tip_dist = get_dist(handLms.landmark[tip_idx], wrist)
    mcp_dist = get_dist(handLms.landmark[mcp_idx], wrist)
    return tip_dist / (mcp_dist + 1e-6) # 0 나누기 방지

while True:
    success, img = cap.read()
    if not success:
        break

    img = cv2.flip(img, 1)
    imgRGB = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    results = hands.process(imgRGB)

    payload = ["0,0,0,0,0", "0,0,0,0,0"]

    if results.multi_hand_landmarks:
        for idx, handLms in enumerate(results.multi_hand_landmarks):
            handedness = results.multi_handedness[idx].classification[0].label

            # --- [로직 1] 커서 좌표 (손바닥 중앙) ---
            cursor_x = handLms.landmark[9].x
            cursor_y = handLms.landmark[9].y

            # --- [로직 2] 손가락별 곡률(Curl) 계산 ---
            index_curl = get_curl_ratio(handLms, 8, 5)
            middle_curl = get_curl_ratio(handLms, 12, 9)
            ring_curl = get_curl_ratio(handLms, 16, 13)
            pinky_curl = get_curl_ratio(handLms, 20, 17)

            # 임계값: 0.85 이하면 손가락이 손바닥 안으로 파고든(꽉 쥔) 상태
            CURL_THRESHOLD = 0.85

            # --- [로직 3] 핀치 거리 계산 (손 크기에 비례) ---
            hand_size = get_dist(handLms.landmark[0], handLms.landmark[9])
            pinch_dist = get_dist(handLms.landmark[4], handLms.landmark[8])
            # 엄지와 검지가 손바닥 크기의 25% 이내로 가까워지면 닿은 것으로 판별
            is_touching = pinch_dist < (hand_size * 0.25)

            # --- [로직 4] 완벽한 상태 머신 (Prioritized State Machine) ---
            # 1. 주먹: 검지, 중지, 약지, 소지가 모두 꽉 접혀있어야 함
            # (엄지는 주먹을 쥘 때 사람마다 위치가 다르므로 검사에서 제외)
            is_fist = 1 if (index_curl < CURL_THRESHOLD and 
                            middle_curl < CURL_THRESHOLD and 
                            ring_curl < CURL_THRESHOLD and 
                            pinky_curl < CURL_THRESHOLD) else 0

            # 2. 핀치: 주먹이 아니면서(검지가 펴져 있으면서) 엄지와 검지가 닿아야 함
            # 이렇게 하면 주먹을 쥐었을 때 엄지가 검지에 닿아도 핀치로 오작동하지 않음!
            is_pinching = 1 if (is_touching and is_fist == 0) else 0

            # 3. 핀치 준비: 주먹도 핀치도 아니고, 검지는 펴져있되 나머지 세 손가락은 접힌 "권총" 모양
            is_pinch_ready = 1 if (not is_pinching and not is_fist and 
                                   index_curl > CURL_THRESHOLD and 
                                   middle_curl < CURL_THRESHOLD and 
                                   ring_curl < CURL_THRESHOLD and 
                                   pinky_curl < CURL_THRESHOLD) else 0

            data_str = f"{cursor_x:.4f},{cursor_y:.4f},{is_pinching},{is_fist},{is_pinch_ready}"
            
            # 💡 [해부학적 근본 판별법] 엄지(Thumb)와 새끼손가락(Pinky)의 좌우 위치 비교
            # 사용자가 손바닥을 카메라로 향하고 있을 때, 거울 모드(좌우 반전) 화면을 기준으로:
            # - 실제 '왼손'은 엄지가 새끼손가락보다 '오른쪽(X값이 더 큼)'에 있습니다.
            # - 실제 '오른손'은 엄지가 새끼손가락보다 '왼쪽(X값이 더 작음)'에 있습니다.
            thumb_mcp_x = handLms.landmark[2].x
            pinky_mcp_x = handLms.landmark[17].x
            
            if thumb_mcp_x > pinky_mcp_x:
                payload[0] = data_str # 왼손 데이터 (Unity isSecondHand = false)
                hand_type = "Left Hand"
            else:
                payload[1] = data_str # 오른손 데이터 (Unity isSecondHand = true)
                hand_type = "Right Hand"
            
            mp_draw.draw_landmarks(img, handLms, mp_hands.HAND_CONNECTIONS)

            # 콘솔 출력 (디버깅용)
            print(f"[{hand_type}] 좌표: ({cursor_x:.2f}, {cursor_y:.2f}) | 핀치: {is_pinching} | 주먹: {is_fist} | 핀치 준비: {is_pinch_ready}")

    data_string = ",".join(payload)
    sock.sendto(str.encode(data_string), serverAddressPort)

    cv2.imshow("Hand Tracker (Python)", img)

    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()
