using UnityEngine;

/// <summary>
/// 과제 1 실험 1: 정확성 검증.
///
/// "위상적으로 올바른 인접 개수"를 무엇으로 정할까?
///   → 같은 위치의 정점을 하나로 용접한 뒤 계산한 인접 개수를 정답으로 본다.
///     (닫힌 표면의 내부 삼각형은 3개, 열린 경계에 걸친 삼각형은 3개 미만)
///
/// 그리고 명세대로 "정점 인덱스만으로" 계산한 인접 개수가
/// 이 정답과 몇 %의 삼각형에서 일치하는지 계산한다.
/// </summary>
public struct ValidationResult
{
    public int triangleCount;
    public int[] indexHistogram;    // 인덱스 기준: 인접 개수 0,1,2,3,4+ 인 삼각형 수
    public int[] weldedHistogram;   // 위치(위상) 기준: 인접 개수 0,1,2,3,4+ 인 삼각형 수
    public int matchCount;          // 두 기준의 인접 개수가 같은 삼각형 수

    public float MatchRate => triangleCount == 0 ? 0f : 100f * matchCount / triangleCount;
}

public static class TopologyValidator
{
    public static ValidationResult Validate(Vector3[] vertices, int[] triangles)
    {
        var byIndex = new AdjacencyBuilder(triangles, VertexWelder.Identity(vertices.Length));
        var byPosition = new AdjacencyBuilder(triangles, VertexWelder.BuildWeldMap(vertices));

        var result = new ValidationResult
        {
            triangleCount = byIndex.TriangleCount,
            indexHistogram = new int[5],
            weldedHistogram = new int[5],
        };

        for (int t = 0; t < result.triangleCount; t++)
        {
            int indexCount = byIndex.GetAdjacentTriangles(t).Count;
            int expected = byPosition.GetAdjacentTriangles(t).Count;

            result.indexHistogram[Mathf.Min(indexCount, 4)]++;
            result.weldedHistogram[Mathf.Min(expected, 4)]++;
            if (indexCount == expected) result.matchCount++;
        }
        return result;
    }

    public static string Format(string meshName, ValidationResult r)
    {
        return $"[HW1 검증] {meshName}: 삼각형 {r.triangleCount}개\n" +
               $"  인덱스 기준 인접 개수 분포 : {Hist(r.indexHistogram)}\n" +
               $"  위치(위상) 기준 분포       : {Hist(r.weldedHistogram)}\n" +
               $"  일치율 : {r.MatchRate:F2}% ({r.matchCount}/{r.triangleCount})";
    }

    private static string Hist(int[] h)
    {
        return $"0개:{h[0]}  1개:{h[1]}  2개:{h[2]}  3개:{h[3]}  4개 이상:{h[4]}";
    }
}
