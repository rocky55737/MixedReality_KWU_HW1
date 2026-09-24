using System.Collections.Generic;

/// <summary>
/// 과제 1 Step 5: 비교용 brute-force 인접 탐색.
///
/// 딕셔너리 없이, 이웃을 찾을 때마다 "전체 삼각형"을 처음부터 끝까지 훑으며
/// 정점을 2개 이상 공유하는(= edge를 공유하는) 삼각형을 찾는다.
///
/// 비용: 한 번 조회 O(N) → 모든 삼각형 조회 O(N²)
/// </summary>
public class AdjacencyBruteForce
{
    private readonly int[] triangles;
    private readonly int[] vertexMap;

    public int TriangleCount => triangles.Length / 3;

    public AdjacencyBruteForce(int[] triangles, int[] vertexMap)
    {
        this.triangles = triangles;
        this.vertexMap = vertexMap;
    }

    public List<int> GetAdjacentTriangles(int triangleIndex)
    {
        var result = new List<int>(3);
        for (int other = 0; other < TriangleCount; other++)
        {
            if (other == triangleIndex) continue;
            if (SharesEdge(triangleIndex, other)) result.Add(other);
        }
        return result;
    }

    /// <summary>
    /// 삼각형에서는 어떤 두 정점을 골라도 그 둘을 잇는 edge가 존재하므로,
    /// "공유 정점이 2개 이상" == "edge를 공유".
    /// </summary>
    private bool SharesEdge(int t, int u)
    {
        int shared = 0;
        for (int i = 0; i < 3; i++)
        {
            int vi = GetVertex(t, i);
            for (int j = 0; j < 3; j++)
            {
                if (vi == GetVertex(u, j)) { shared++; break; }
            }
        }
        return shared >= 2;
    }

    private int GetVertex(int triangle, int corner)
    {
        return vertexMap[triangles[triangle * 3 + corner]];
    }
}
