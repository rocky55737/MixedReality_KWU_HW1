using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정점 "용접(weld)" 도우미.
///
/// Unity 기본 Sphere 같은 메쉬는 UV 이음매(seam)와 극점에서
/// "위치는 같지만 인덱스가 다른" 정점을 여러 개 가지고 있다.
/// 인덱스만으로 edge를 비교하면 이런 곳에서 인접 관계가 끊겨 보인다.
///
/// 이 클래스는 "원래 정점 인덱스 → 대표 정점 인덱스" 표(vertexMap)를 만든다.
///   - Identity    : 용접 안 함. map[i] = i  (명세 그대로, 인덱스 기준)
///   - BuildWeldMap: 같은 위치의 정점들은 전부 같은 대표 인덱스로 매핑
/// </summary>
public static class VertexWelder
{
    /// <summary>용접하지 않는 매핑: 각 정점이 자기 자신을 가리킨다.</summary>
    public static int[] Identity(int vertexCount)
    {
        var map = new int[vertexCount];
        for (int i = 0; i < vertexCount; i++) map[i] = i;
        return map;
    }

    /// <summary>
    /// 위치가 epsilon 이내로 같은 정점들을 하나로 묶는 매핑.
    /// 좌표를 epsilon 단위 격자로 반올림한 값을 딕셔너리 key로 사용한다.
    /// </summary>
    public static int[] BuildWeldMap(Vector3[] vertices, float epsilon = 1e-5f)
    {
        var map = new int[vertices.Length];
        var firstIndexAtPosition = new Dictionary<Vector3Int, int>();
        float scale = 1f / epsilon;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            var key = new Vector3Int(
                Mathf.RoundToInt(v.x * scale),
                Mathf.RoundToInt(v.y * scale),
                Mathf.RoundToInt(v.z * scale));

            if (firstIndexAtPosition.TryGetValue(key, out int representative))
            {
                map[i] = representative;          // 이미 본 위치 → 그 정점을 대표로 사용
            }
            else
            {
                firstIndexAtPosition.Add(key, i); // 처음 본 위치 → 내가 대표
                map[i] = i;
            }
        }
        return map;
    }
}
