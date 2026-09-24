using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 과제 1 Step 2~3: Edge → Triangle 딕셔너리로 인접 삼각형을 찾는다.
///
/// 아이디어 (half-edge 단순화 버전)
///   1) 모든 삼각형을 한 번씩 돌면서, 삼각형의 3개 edge를 딕셔너리에 등록한다.
///      key   = 무방향 edge (작은 정점 인덱스, 큰 정점 인덱스)
///      value = 그 edge를 가진 삼각형 번호 목록
///   2) 어떤 삼각형의 이웃을 찾을 때는, 그 삼각형의 edge 3개로 딕셔너리를 조회해
///      "같은 edge를 가진 다른 삼각형"을 모으면 끝.
///
/// 비용: 구성 O(N), 한 번 조회 O(1) → 모든 삼각형 조회 O(N)
/// </summary>
public class AdjacencyBuilder
{
    private readonly int[] triangles;   // mesh.triangles: 삼각형 t의 정점은 [3t], [3t+1], [3t+2]
    private readonly int[] vertexMap;   // 정점 인덱스 → 비교에 사용할 대표 인덱스 (VertexWelder 참고)

    /// <summary>edge → 그 edge를 가진 삼각형 번호들</summary>
    public Dictionary<(int, int), List<int>> EdgeToTriangles { get; }

    public int TriangleCount => triangles.Length / 3;

    public AdjacencyBuilder(int[] triangles, int[] vertexMap)
    {
        this.triangles = triangles;
        this.vertexMap = vertexMap;

        // edge 개수는 대략 삼각형 수 × 1.5 이므로 미리 용량을 잡아 재할당을 줄인다.
        EdgeToTriangles = new Dictionary<(int, int), List<int>>(TriangleCount * 3 / 2 + 1);

        for (int t = 0; t < TriangleCount; t++)
        {
            for (int k = 0; k < 3; k++)
            {
                // 삼각형의 edge: (v0,v1), (v1,v2), (v2,v0)
                int a = GetVertex(t, k);
                int b = GetVertex(t, (k + 1) % 3);
                if (a == b) continue; // 길이 0짜리 퇴화 edge는 무시

                var key = MakeEdgeKey(a, b);
                if (!EdgeToTriangles.TryGetValue(key, out List<int> owners))
                {
                    owners = new List<int>(2); // 닫힌 메쉬라면 보통 2개
                    EdgeToTriangles.Add(key, owners);
                }
                owners.Add(t);
            }
        }
    }

    /// <summary>주어진 삼각형과 edge를 공유하는 삼각형들(자기 자신 제외)을 반환.</summary>
    public List<int> GetAdjacentTriangles(int triangleIndex)
    {
        var result = new List<int>(3);
        for (int k = 0; k < 3; k++)
        {
            int a = GetVertex(triangleIndex, k);
            int b = GetVertex(triangleIndex, (k + 1) % 3);
            if (a == b) continue;

            if (!EdgeToTriangles.TryGetValue(MakeEdgeKey(a, b), out List<int> owners)) continue;

            foreach (int other in owners)
            {
                if (other != triangleIndex && !result.Contains(other))
                    result.Add(other);
            }
        }
        return result;
    }

    /// <summary>(a,b)와 (b,a)를 같은 edge로 취급하기 위한 정규화.</summary>
    public static (int, int) MakeEdgeKey(int a, int b)
    {
        return (Mathf.Min(a, b), Mathf.Max(a, b));
    }

    private int GetVertex(int triangle, int corner)
    {
        return vertexMap[triangles[triangle * 3 + corner]];
    }
}
