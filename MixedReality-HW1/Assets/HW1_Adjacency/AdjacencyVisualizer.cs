using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 과제 1 Step 4: Scene 뷰 시각화 + 실험 1(정확성 검증) 실행 버튼.
///
/// 사용법: Sphere/Cylinder 오브젝트에 붙이고 Inspector에서 selectedTriangle 값을 바꾸면
///   선택 삼각형 = 빨강, 인접 삼각형 = 초록 으로 표시된다.
///   컴포넌트 우측 ⋮ 메뉴 → "정확성 검증 실행" 으로 Console에 결과 출력.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class AdjacencyVisualizer : MonoBehaviour
{
    [Tooltip("선택할 삼각형 번호")]
    public int selectedTriangle = 0;

    [Tooltip("체크: 같은 위치의 정점을 하나로 보고 계산 (UV 이음매 문제 해결)\n해제: 명세대로 정점 인덱스만으로 계산")]
    public bool weldByPosition = false;

    [Tooltip("표면과 겹쳐서 깜빡이지 않도록 법선 방향으로 살짝 띄우는 거리")]
    public float surfaceOffset = 0.002f;

    // 메쉬나 옵션이 바뀔 때만 다시 계산하기 위한 캐시
    private Mesh cachedMesh;
    private bool cachedWeld;
    private Vector3[] vertices;
    private int[] triangles;
    private AdjacencyBuilder adjacency;

    private void EnsureBuilt()
    {
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        if (mesh == null) { adjacency = null; return; }
        if (adjacency != null && mesh == cachedMesh && weldByPosition == cachedWeld) return;

        vertices = mesh.vertices;
        triangles = mesh.triangles;
        int[] map = weldByPosition
            ? VertexWelder.BuildWeldMap(vertices)
            : VertexWelder.Identity(vertices.Length);

        adjacency = new AdjacencyBuilder(triangles, map);
        cachedMesh = mesh;
        cachedWeld = weldByPosition;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        EnsureBuilt();
        if (adjacency == null || adjacency.TriangleCount == 0) return;

        int selected = Mathf.Clamp(selectedTriangle, 0, adjacency.TriangleCount - 1);
        List<int> neighbors = adjacency.GetAdjacentTriangles(selected);

        Handles.matrix = transform.localToWorldMatrix; // 이후 좌표는 메쉬 local 좌표로 그리면 됨
        DrawTriangle(selected, Color.red);
        foreach (int n in neighbors) DrawTriangle(n, Color.green);

        Handles.color = Color.white;
        Handles.Label(Centroid(selected), $"T{selected} / 인접 {neighbors.Count}개: [{string.Join(", ", neighbors)}]");
    }

    private void DrawTriangle(int t, Color color)
    {
        Vector3 a = vertices[triangles[t * 3]];
        Vector3 b = vertices[triangles[t * 3 + 1]];
        Vector3 c = vertices[triangles[t * 3 + 2]];
        Vector3 offset = Vector3.Cross(b - a, c - a).normalized * surfaceOffset;
        a += offset; b += offset; c += offset;

        Handles.color = new Color(color.r, color.g, color.b, 0.45f);
        Handles.DrawAAConvexPolygon(a, b, c);            // 반투명 면
        Handles.color = color;
        Handles.DrawAAPolyLine(4f, a, b, c, a);          // 두꺼운 외곽선
    }

    private Vector3 Centroid(int t)
    {
        return (vertices[triangles[t * 3]] + vertices[triangles[t * 3 + 1]] + vertices[triangles[t * 3 + 2]]) / 3f;
    }
#endif

    [ContextMenu("정확성 검증 실행")]
    private void RunValidation()
    {
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        if (mesh == null) { Debug.LogWarning("MeshFilter에 메쉬가 없습니다."); return; }

        ValidationResult r = TopologyValidator.Validate(mesh.vertices, mesh.triangles);
        Debug.Log(TopologyValidator.Format(mesh.name, r));
    }
}
