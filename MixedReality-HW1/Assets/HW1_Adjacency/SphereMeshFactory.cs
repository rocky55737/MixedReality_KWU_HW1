using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 성능 실험용 UV 구 메쉬를 코드로 생성한다.
/// (Unity 기본 Sphere는 해상도가 하나뿐이라 "저/중/고해상도" 비교가 불가능하기 때문)
///
/// 구조: 위도 방향 latitude개 띠 × 경도 방향 longitude개 칸.
/// Unity 기본 Sphere와 마찬가지로 이음매(seam)와 극점에 위치가 같은 정점이 중복된다.
///   - 각 위도 링마다 정점 longitude+1개 (첫 정점과 마지막 정점이 같은 위치 = seam)
///   - 맨 위/아래 링은 정점들이 전부 극점 한 점에 모여 있음
/// 삼각형 수 = 2 × longitude × (latitude - 1)
/// </summary>
public static class SphereMeshFactory
{
    public static Mesh CreateUVSphere(int longitude, int latitude, float radius = 0.5f)
    {
        longitude = Mathf.Max(3, longitude);
        latitude = Mathf.Max(2, latitude);
        int ringSize = longitude + 1;

        // 1) 정점: 북극(i=0)부터 남극(i=latitude)까지 링 단위로 생성
        var vertices = new List<Vector3>();
        for (int i = 0; i <= latitude; i++)
        {
            float theta = Mathf.PI * i / latitude;         // 북극에서 내려온 각도
            float y = Mathf.Cos(theta);
            float ringRadius = Mathf.Sin(theta);
            for (int j = 0; j <= longitude; j++)
            {
                float phi = 2f * Mathf.PI * j / longitude; // 경도 각도
                vertices.Add(radius * new Vector3(ringRadius * Mathf.Cos(phi), y, ringRadius * Mathf.Sin(phi)));
            }
        }

        // 2) 삼각형: 사각형 칸 (a, a1 / b, b1) 하나를 삼각형 2개로 나눈다.
        //      a ---- a1      (위 링)
        //      |    / |
        //      |  /   |
        //      b ---- b1      (아래 링)
        //    극점 칸에서는 한쪽 삼각형이 넓이 0이 되므로 생략한다.
        var triangles = new List<int>();
        for (int i = 0; i < latitude; i++)
        {
            for (int j = 0; j < longitude; j++)
            {
                int a = i * ringSize + j;
                int a1 = a + 1;
                int b = a + ringSize;
                int b1 = b + 1;

                if (i != 0)            { triangles.Add(a);  triangles.Add(a1); triangles.Add(b); }
                if (i != latitude - 1) { triangles.Add(a1); triangles.Add(b1); triangles.Add(b); }
            }
        }

        var mesh = new Mesh
        {
            name = $"UVSphere_{longitude}x{latitude}",
            indexFormat = IndexFormat.UInt32, // 정점이 65535개를 넘어도 되도록
        };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
