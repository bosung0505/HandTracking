using UnityEngine;
using System.Collections.Generic;

public enum CubeFace { Top, Bottom, Right, Left, Front, Back, None }

[System.Serializable]
public struct GridPos
{
    public CubeFace face;
    public int x;
    public int y;
    public GridPos(CubeFace f, int _x, int _y) { face = f; x = _x; y = _y; }
    public override bool Equals(object obj) => obj is GridPos p && face == p.face && x == p.x && y == p.y;
    public override int GetHashCode() => (int)face * 1000 + x * 10 + y;
    public override string ToString() => $"({face}, {x}, {y})";

    // "없음" 상태를 나타내는 센티널 값 (비숍 공격 스캔에서 사용)
    public static readonly GridPos Invalid = new GridPos(CubeFace.None, -1, -1);
}


public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    public float cellSize = 0.48f;
    public float gridLimit = 1.68f;
    public float faceDistance = 1.92f; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public Vector3 GetNormalFromFace(CubeFace face)
    {
        switch (face)
        {
            case CubeFace.Top: return Vector3.up;
            case CubeFace.Bottom: return Vector3.down;
            case CubeFace.Right: return Vector3.right;
            case CubeFace.Left: return Vector3.left;
            case CubeFace.Front: return Vector3.forward;
            case CubeFace.Back: return Vector3.back;
            default: return Vector3.up;
        }
    }

    public CubeFace GetFaceFromNormal(Vector3 normal)
    {
        if (normal.y > 0.5f) return CubeFace.Top;
        if (normal.y < -0.5f) return CubeFace.Bottom;
        if (normal.x > 0.5f) return CubeFace.Right;
        if (normal.x < -0.5f) return CubeFace.Left;
        if (normal.z > 0.5f) return CubeFace.Front;
        if (normal.z < -0.5f) return CubeFace.Back;
        return CubeFace.None;
    }

    // 3D 로컬 좌표를 (면, X, Y) 논리 그리드 좌표로 변환
    public void GetGridCoordinate(Vector3 localNormal, Vector3 localPoint, out CubeFace face, out int x, out int y)
    {
        face = GetFaceFromNormal(localNormal);
        float posX = 0, posY = 0;

        if (Mathf.Abs(localNormal.x) > 0.5f) { posX = localPoint.y; posY = localPoint.z; }
        else if (Mathf.Abs(localNormal.y) > 0.5f) { posX = localPoint.x; posY = localPoint.z; }
        else { posX = localPoint.x; posY = localPoint.y; }

        x = Mathf.RoundToInt((posX / cellSize) + 3.5f);
        y = Mathf.RoundToInt((posY / cellSize) + 3.5f);
        x = Mathf.Clamp(x, 0, 7);
        y = Mathf.Clamp(y, 0, 7);
    }

    // 논리 그리드 좌표(면, X, Y)를 3D 로컬 좌표로 변환
    public Vector3 GetLocalPosition(Vector3 localNormal, int gridX, int gridY, Transform boardTransform = null)
    {
        float currentFaceDistance = faceDistance;

        if (boardTransform != null)
        {
            BoxCollider col = boardTransform.GetComponent<BoxCollider>();
            if (col != null)
            {
                if (Mathf.Abs(localNormal.x) > 0.5f) currentFaceDistance = col.size.x / 2f;
                else if (Mathf.Abs(localNormal.y) > 0.5f) currentFaceDistance = col.size.y / 2f;
                else currentFaceDistance = col.size.z / 2f;
            }
        }

        float posX = (gridX - 3.5f) * cellSize;
        float posY = (gridY - 3.5f) * cellSize;

        if (Mathf.Abs(localNormal.x) > 0.5f) return new Vector3(Mathf.Sign(localNormal.x) * currentFaceDistance, posX, posY);
        else if (Mathf.Abs(localNormal.y) > 0.5f) return new Vector3(posX, Mathf.Sign(localNormal.y) * currentFaceDistance, posY);
        else return new Vector3(posX, posY, Mathf.Sign(localNormal.z) * currentFaceDistance);
    }

    // 큐브 전개도 위상(Topology) 매핑 - 모서리를 넘었을 때 맞닿은 옆면의 좌표를 반환
    public GridPos GetNeighbor(GridPos pos, int dx, int dy)
    {
        int nx = pos.x + dx;
        int ny = pos.y + dy;
        
        if (nx >= 0 && nx <= 7 && ny >= 0 && ny <= 7) 
            return new GridPos(pos.face, nx, ny); // 같은 면 내의 이동

        CubeFace face = pos.face;
        
        if (face == CubeFace.Top) {
            if (ny > 7) return new GridPos(CubeFace.Front, pos.x, 7);
            if (ny < 0) return new GridPos(CubeFace.Back, pos.x, 7);
            if (nx > 7) return new GridPos(CubeFace.Right, 7, pos.y);
            if (nx < 0) return new GridPos(CubeFace.Left, 7, pos.y);
        } else if (face == CubeFace.Bottom) {
            if (ny > 7) return new GridPos(CubeFace.Front, pos.x, 0);
            if (ny < 0) return new GridPos(CubeFace.Back, pos.x, 0);
            if (nx > 7) return new GridPos(CubeFace.Right, 0, pos.y);
            if (nx < 0) return new GridPos(CubeFace.Left, 0, pos.y);
        } else if (face == CubeFace.Right) {
            if (nx > 7) return new GridPos(CubeFace.Top, 7, pos.y);
            if (nx < 0) return new GridPos(CubeFace.Bottom, 7, pos.y);
            if (ny > 7) return new GridPos(CubeFace.Front, 7, pos.x);
            if (ny < 0) return new GridPos(CubeFace.Back, 7, pos.x);
        } else if (face == CubeFace.Left) {
            if (nx > 7) return new GridPos(CubeFace.Top, 0, pos.y);
            if (nx < 0) return new GridPos(CubeFace.Bottom, 0, pos.y);
            if (ny > 7) return new GridPos(CubeFace.Front, 0, pos.x);
            if (ny < 0) return new GridPos(CubeFace.Back, 0, pos.x);
        } else if (face == CubeFace.Front) {
            if (ny > 7) return new GridPos(CubeFace.Top, pos.x, 7);
            if (ny < 0) return new GridPos(CubeFace.Bottom, pos.x, 7);
            if (nx > 7) return new GridPos(CubeFace.Right, pos.y, 7); // 축 회전 반영
            if (nx < 0) return new GridPos(CubeFace.Left, pos.y, 7);
        } else if (face == CubeFace.Back) {
            if (ny > 7) return new GridPos(CubeFace.Top, pos.x, 0);
            if (ny < 0) return new GridPos(CubeFace.Bottom, pos.x, 0);
            if (nx > 7) return new GridPos(CubeFace.Right, pos.y, 0); // 축 회전 반영
            if (nx < 0) return new GridPos(CubeFace.Left, pos.y, 0);
        }
        return pos;
    }

    // BFS 기반 플로우 필드 (목표 지점까지의 최단 거리 맵 생성)
    public Dictionary<GridPos, int> GenerateFlowField(GridPos targetPos)
    {
        Dictionary<GridPos, int> distances = new Dictionary<GridPos, int>();
        Queue<GridPos> queue = new Queue<GridPos>();

        queue.Enqueue(targetPos);
        distances[targetPos] = 0;

        int[][] dirs = new int[][] { new int[]{1,0}, new int[]{-1,0}, new int[]{0,1}, new int[]{0,-1} };

        while (queue.Count > 0)
        {
            GridPos curr = queue.Dequeue();
            int currDist = distances[curr];

            foreach (var d in dirs)
            {
                GridPos neighbor = GetNeighbor(curr, d[0], d[1]);
                if (!distances.ContainsKey(neighbor))
                {
                    distances[neighbor] = currDist + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }
        return distances;
    }
}
