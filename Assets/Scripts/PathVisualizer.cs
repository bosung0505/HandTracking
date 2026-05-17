using UnityEngine;
using System.Collections.Generic;

public class PathVisualizer : MonoBehaviour
{
    public static PathVisualizer Instance { get; private set; }

    [Tooltip("이동 가능 영역에 표시할 1칸짜리 하이라이트 패널(Plane) 프리팹")]
    public GameObject highlightPrefab;
    
    private List<GameObject> activeHighlights = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 이동 가능한 좌표 리스트를 받아 보드 위에 하이라이트를 생성합니다.
    public void ShowPath(List<GridPos> validMoves, Transform board)
    {
        ClearPath(); // 기존 하이라이트 삭제
        if (highlightPrefab == null) return;

        foreach (var pos in validMoves)
        {
            GameObject hl = Instantiate(highlightPrefab, board);
            Vector3 localNormal = GridManager.Instance.GetNormalFromFace(pos.face);
            
            // 표면보다 아주 약간(0.01f) 띄워서 렌더링 겹침(Z-fighting) 방지
            hl.transform.localPosition = GridManager.Instance.GetLocalPosition(localNormal, pos.x, pos.y, board) + localNormal * 0.01f;
            hl.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localNormal);
            
            activeHighlights.Add(hl);
        }
    }

    public void ClearPath()
    {
        foreach (var hl in activeHighlights)
        {
            if (hl != null) Destroy(hl);
        }
        activeHighlights.Clear();
    }
}
