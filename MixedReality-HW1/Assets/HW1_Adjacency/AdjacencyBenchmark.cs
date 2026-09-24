using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 과제 1 실험 2~3: 딕셔너리 방식 vs brute-force 성능 비교.
///
/// 사용법: 빈 GameObject에 붙이고, 컴포넌트 ⋮ 메뉴 → "벤치마크 실행".
///   결과는 Console에 출력되고, 저장소 루트의 Report/hw1_benchmark.csv 로 저장된다.
///   (큰 메쉬의 brute-force 때문에 에디터가 수십 초 멈출 수 있음 — 정상)
/// </summary>
public class AdjacencyBenchmark : MonoBehaviour
{
    [Tooltip("코드로 생성할 UV 구의 경도 분할 수 (위도 분할 = 절반). 클수록 삼각형 증가")]
    public int[] sphereLongitudeSegments = { 8, 16, 32, 64, 128 };

    [Tooltip("추가로 측정할 메쉬 (Unity 기본 Sphere, Cylinder 메쉬를 드래그)")]
    public Mesh[] extraMeshes;

    [Tooltip("같은 위치의 정점을 하나로 보고 측정할지 여부")]
    public bool weldByPosition = true;

    [Tooltip("측정 반복 횟수 (결과는 중앙값 사용)")]
    public int repeats = 5;

    [Tooltip("삼각형이 이보다 많으면 brute-force는 너무 오래 걸려 생략")]
    public int bruteForceMaxTriangles = 20000;

    public string csvFileName = "hw1_benchmark.csv";

    /// <summary>메쉬 하나에 대한 측정 결과 한 줄</summary>
    private class Row
    {
        public string meshName;
        public int vertexCount;
        public int triangleCount;
        public float indexMatchRate;
        public double dictBuildMs;
        public double dictQueryMs;
        public double bruteQueryMs = double.NaN;
        public string resultsEqual = "skipped";
        public long checksum; // 조회 결과를 실제로 "사용"해서 컴파일러가 코드를 생략하지 못하게 함

        public const string CsvHeader =
            "mesh,vertices,triangles,index_match_rate_pct,dict_build_ms,dict_query_ms,dict_total_ms,brute_query_ms,results_equal";

        public string ToCsv()
        {
            return string.Join(",",
                meshName, vertexCount, triangleCount,
                F(indexMatchRate), F(dictBuildMs), F(dictQueryMs), F(dictBuildMs + dictQueryMs),
                double.IsNaN(bruteQueryMs) ? "" : F(bruteQueryMs),
                resultsEqual);
        }

        public string ToLog()
        {
            string brute = double.IsNaN(bruteQueryMs) ? "생략" : $"{bruteQueryMs:F2} ms";
            return $"[HW1 벤치마크] {meshName}  V={vertexCount}  N={triangleCount}\n" +
                   $"  딕셔너리: 구성 {dictBuildMs:F2} ms + 전체 조회 {dictQueryMs:F2} ms\n" +
                   $"  Brute-force 전체 조회: {brute}   결과 일치: {resultsEqual}";
        }

        private static string F(double x) => x.ToString("F3", CultureInfo.InvariantCulture);
    }

    [ContextMenu("벤치마크 실행")]
    public void RunBenchmark()
    {
        // 1) 테스트 메쉬 준비
        var generated = new List<Mesh>();
        foreach (int lon in sphereLongitudeSegments)
            generated.Add(SphereMeshFactory.CreateUVSphere(lon, Mathf.Max(2, lon / 2)));

        var targets = new List<Mesh>(generated);
        if (extraMeshes != null)
            foreach (Mesh m in extraMeshes) if (m != null) targets.Add(m);

        // 2) 워밍업: 처음 실행되는 코드는 JIT 컴파일 시간이 섞이므로 작은 메쉬로 한 번 버린다
        Mesh warmup = SphereMeshFactory.CreateUVSphere(8, 4);
        Measure(warmup, 1);
        DestroyImmediate(warmup);

        // 3) 측정
        var csv = new StringBuilder();
        csv.AppendLine(Row.CsvHeader);
        foreach (Mesh mesh in targets)
        {
            Row row = Measure(mesh, Mathf.Max(1, repeats));
            csv.AppendLine(row.ToCsv());
            Debug.Log(row.ToLog());
        }
        foreach (Mesh m in generated) DestroyImmediate(m);

        // 4) CSV 저장: Assets의 두 단계 위 = 저장소 루트
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Report"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, csvFileName);
        File.WriteAllText(path, csv.ToString());
        Debug.Log($"[HW1] CSV 저장 완료: {path}");
    }

    private Row Measure(Mesh mesh, int repeatCount)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        int n = triangles.Length / 3;
        int[] map = weldByPosition
            ? VertexWelder.BuildWeldMap(vertices)
            : VertexWelder.Identity(vertices.Length);

        var row = new Row
        {
            meshName = mesh.name,
            vertexCount = vertices.Length,
            triangleCount = n,
            indexMatchRate = TopologyValidator.Validate(vertices, triangles).MatchRate,
        };

        // --- 딕셔너리 방식: 구성 시간과 전체 조회 시간을 따로 잰다 ---
        var buildTimes = new List<double>();
        var queryTimes = new List<double>();
        AdjacencyBuilder dict = null;
        for (int r = 0; r < repeatCount; r++)
        {
            GC.Collect(); // 측정 도중 가비지 컬렉션이 끼어드는 것을 줄임

            var sw = Stopwatch.StartNew();
            dict = new AdjacencyBuilder(triangles, map);
            sw.Stop();
            buildTimes.Add(sw.Elapsed.TotalMilliseconds);

            sw.Restart();
            row.checksum = QueryAll(dict.GetAdjacentTriangles, n);
            sw.Stop();
            queryTimes.Add(sw.Elapsed.TotalMilliseconds);
        }
        row.dictBuildMs = Median(buildTimes);
        row.dictQueryMs = Median(queryTimes);

        // --- Brute-force 방식: O(N²)이라 큰 메쉬는 생략, 반복도 최대 3회 ---
        if (n <= bruteForceMaxTriangles)
        {
            var brute = new AdjacencyBruteForce(triangles, map);
            var bruteTimes = new List<double>();
            int bruteRepeats = Mathf.Min(repeatCount, 3);
            for (int r = 0; r < bruteRepeats; r++)
            {
                GC.Collect();
                var sw = Stopwatch.StartNew();
                row.checksum += QueryAll(brute.GetAdjacentTriangles, n);
                sw.Stop();
                bruteTimes.Add(sw.Elapsed.TotalMilliseconds);
            }
            row.bruteQueryMs = Median(bruteTimes);
            row.resultsEqual = SameResults(dict, brute, n) ? "yes" : "NO";
        }
        return row;
    }

    /// <summary>"모든 삼각형에 대해 인접 삼각형 전부 조회". 반환값은 인접 개수 합계.</summary>
    private static long QueryAll(Func<int, List<int>> getAdjacent, int triangleCount)
    {
        long sum = 0;
        for (int t = 0; t < triangleCount; t++) sum += getAdjacent(t).Count;
        return sum;
    }

    /// <summary>두 방식이 모든 삼각형에서 같은 이웃 집합을 돌려주는지 확인 (시간 측정과 별개)</summary>
    private static bool SameResults(AdjacencyBuilder dict, AdjacencyBruteForce brute, int triangleCount)
    {
        for (int t = 0; t < triangleCount; t++)
        {
            List<int> a = dict.GetAdjacentTriangles(t);
            List<int> b = brute.GetAdjacentTriangles(t);
            if (a.Count != b.Count) return false;
            a.Sort(); b.Sort();
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        }
        return true;
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        int mid = values.Count / 2;
        return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2.0;
    }
}
