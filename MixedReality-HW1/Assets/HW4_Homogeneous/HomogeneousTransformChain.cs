using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 과제 4: 계층 구조의 world 변환을 직접 곱해서 계산하고, 역변환까지 검증한다.
///
/// 사용법
///   1) 빈 GameObject에 붙이고 ⋮ 메뉴 → "계층 생성 (Root-Shoulder-Elbow-Wrist-Hand)"
///   2) 각 관절의 local 위치/회전을 직접 바꿔 보거나 ⋮ 메뉴 → "로컬 값 랜덤화"
///      Scene 뷰: 초록 선 = 직접 계산한 관절 위치, 라벨 = Hand 오차
///   3) ⋮ 메뉴의 실험 1~4 실행 → Console + Report 폴더 CSV
///
/// 주의: 이 과제의 행렬은 회전+이동만 다루므로 모든 관절의 scale은 1이어야 한다.
/// </summary>
[ExecuteAlways]
public class HomogeneousTransformChain : MonoBehaviour
{
    [Tooltip("Root부터 Hand까지 순서대로 (부모 → 자식)")]
    public Transform[] chain;

    [Header("실험 설정")]
    public int randomSets = 10;
    public int[] depthValues = { 2, 4, 8, 16, 32 };
    [Tooltip("랜덤 local position 범위 (m)")]
    public float positionRange = 0.5f;
    public int speedTestIterations = 1000000;

    // =================================================================
    // Step 3. World Transform 체인 계산
    // =================================================================

    /// <summary>T_world = T_root * T_shoulder * T_elbow * T_wrist * T_hand</summary>
    public static Matrix4x4 ComputeWorldMatrix(IList<Transform> joints)
    {
        Matrix4x4 world = Matrix4x4.identity;
        foreach (Transform joint in joints)
            world = world * HomogeneousMath.LocalMatrix(joint); // 부모 쪽 행렬이 왼쪽
        return world;
    }

    /// <summary>원점 (0,0,0,1)을 T_world로 변환한 것 = 마지막 관절의 world position</summary>
    public static Vector3 ComputeWorldPosition(IList<Transform> joints)
    {
        return HomogeneousMath.TransformPoint(ComputeWorldMatrix(joints), Vector3.zero);
    }

    // =================================================================
    // 한 세트 측정 (실험 1, 2, 3에서 공통 사용)
    // =================================================================
    private struct Measurement
    {
        public float forwardErrorMm;        // 직접 계산 world position vs transform.position
        public float inverseFrobenius;      // ‖T⁻¹·T − I‖ (직접 유도한 역행렬)
        public float generalInverseFrobenius; // ‖inverse(T)·T − I‖ (Matrix4x4.inverse)
        public float localPointErrorMm;     // 임의 world 점 → local: 직접 vs InverseTransformPoint
    }

    private static Measurement Measure(IList<Transform> joints)
    {
        var m = new Measurement();
        Transform last = joints[joints.Count - 1];

        // 1) Forward 검증
        Matrix4x4 world = ComputeWorldMatrix(joints);
        Vector3 mine = HomogeneousMath.TransformPoint(world, Vector3.zero);
        m.forwardErrorMm = (mine - last.position).magnitude * 1000f; // m → mm

        // 2) 역변환 검증: T⁻¹·T 가 단위행렬에 얼마나 가까운가
        Matrix4x4 inv = HomogeneousMath.InverseRigid(world);
        m.inverseFrobenius = HomogeneousMath.FrobeniusDiff(inv * world, Matrix4x4.identity);
        m.generalInverseFrobenius = HomogeneousMath.FrobeniusDiff(world.inverse * world, Matrix4x4.identity);

        // 3) world의 임의 점을 마지막 관절의 local 좌표로 되돌리기
        //    T_world는 (마지막 관절 local → world) 이므로 T_world⁻¹는 (world → 마지막 관절 local)
        Vector3 worldPoint = Random.insideUnitSphere * 2f;
        Vector3 localMine = HomogeneousMath.TransformPoint(inv, worldPoint);
        Vector3 localUnity = last.InverseTransformPoint(worldPoint);
        m.localPointErrorMm = (localMine - localUnity).magnitude * 1000f;
        return m;
    }

    private void RandomizeLocals(IList<Transform> joints)
    {
        foreach (Transform j in joints)
        {
            j.localRotation = Random.rotation;
            j.localPosition = Random.insideUnitSphere * positionRange;
            j.localScale = Vector3.one;
        }
    }

    // =================================================================
    [ContextMenu("실험 1+2. Forward / 역변환 검증 (현재 계층, 랜덤 N세트)")]
    private void RunForwardAndInverse()
    {
        if (!CheckChain()) return;
        var saved = SaveLocals(chain);

        var csv = new StringBuilder("set,depth,forward_error_mm,inverse_frobenius_rigid,inverse_frobenius_general,local_point_error_mm\n");
        var log = new StringBuilder($"[HW4 실험 1+2] 깊이 {chain.Length}, {randomSets}세트\n  set | 위치 오차(mm) | ‖T⁻¹T−I‖ 직접 | ‖T⁻¹T−I‖ 일반 | 역변환 점 오차(mm)\n");

        for (int s = 0; s < randomSets; s++)
        {
            RandomizeLocals(chain);
            Measurement m = Measure(chain);
            csv.AppendLine(string.Join(",", s + 1, chain.Length, F(m.forwardErrorMm), F(m.inverseFrobenius), F(m.generalInverseFrobenius), F(m.localPointErrorMm)));
            log.AppendLine($"  {s + 1,3} | {m.forwardErrorMm,12:E2} | {m.inverseFrobenius,13:E2} | {m.generalInverseFrobenius,13:E2} | {m.localPointErrorMm,10:E2}");
        }

        RestoreLocals(chain, saved); // 씬의 원래 자세로 되돌림
        Debug.Log(log.ToString());
        SaveCsv("hw4_forward_inverse.csv", csv.ToString());
    }

    // =================================================================
    [ContextMenu("실험 3. 계층 깊이별 누적 오차")]
    private void RunDepthSweep()
    {
        var csv = new StringBuilder("depth,forward_error_mm_mean,forward_error_mm_max,inverse_frobenius_rigid_mean,inverse_frobenius_general_mean,local_point_error_mm_mean\n");
        var log = new StringBuilder($"[HW4 실험 3] 깊이별 평균 ({randomSets}세트)\n  깊이 | 위치 오차 평균/최대(mm) | ‖T⁻¹T−I‖ 직접 | 일반 | 역변환 점 오차(mm)\n");

        foreach (int depth in depthValues)
        {
            // 임시 계층 생성 (측정 후 삭제)
            List<Transform> temp = CreateHierarchy(null, depth, "Temp");
            temp[0].gameObject.hideFlags = HideFlags.HideAndDontSave;

            float fwdSum = 0f, fwdMax = 0f, invSum = 0f, genSum = 0f, locSum = 0f;
            for (int s = 0; s < randomSets; s++)
            {
                RandomizeLocals(temp);
                Measurement m = Measure(temp);
                fwdSum += m.forwardErrorMm; fwdMax = Mathf.Max(fwdMax, m.forwardErrorMm);
                invSum += m.inverseFrobenius; genSum += m.generalInverseFrobenius; locSum += m.localPointErrorMm;
            }
            DestroyImmediate(temp[0].gameObject);

            float n = randomSets;
            csv.AppendLine(string.Join(",", depth, F(fwdSum / n), F(fwdMax), F(invSum / n), F(genSum / n), F(locSum / n)));
            log.AppendLine($"  {depth,4} | {fwdSum / n,9:E2} / {fwdMax,9:E2} | {invSum / n,12:E2} | {genSum / n,9:E2} | {locSum / n,9:E2}");
        }
        Debug.Log(log.ToString());
        SaveCsv("hw4_depth_sweep.csv", csv.ToString());
    }

    // =================================================================
    [ContextMenu("실험 4 (선택). 직접 역행렬 vs Matrix4x4.inverse 속도")]
    private void RunInverseSpeed()
    {
        if (!CheckChain()) return;
        Matrix4x4 world = ComputeWorldMatrix(chain);
        Matrix4x4 sink = Matrix4x4.zero; // 결과를 사용해 컴파일러 최적화로 생략되지 않게

        // 워밍업 (JIT 컴파일 시간 제외)
        for (int i = 0; i < 1000; i++) { sink = HomogeneousMath.InverseRigid(world); sink = world.inverse; }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < speedTestIterations; i++) sink = HomogeneousMath.InverseRigid(world);
        sw.Stop();
        double rigidMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        for (int i = 0; i < speedTestIterations; i++) sink = world.inverse;
        sw.Stop();
        double generalMs = sw.Elapsed.TotalMilliseconds;

        float rigidErr = HomogeneousMath.FrobeniusDiff(HomogeneousMath.InverseRigid(world) * world, Matrix4x4.identity);
        float generalErr = HomogeneousMath.FrobeniusDiff(world.inverse * world, Matrix4x4.identity);
        float methodDiff = HomogeneousMath.FrobeniusDiff(HomogeneousMath.InverseRigid(world), world.inverse);

        string log = $"[HW4 실험 4] {speedTestIterations:N0}회 반복 (sink={sink.m33})\n" +
                     $"  직접 유도(Rᵀ, -Rᵀt) : {rigidMs:F2} ms,  ‖T⁻¹T−I‖ = {rigidErr:E2}\n" +
                     $"  Matrix4x4.inverse    : {generalMs:F2} ms,  ‖T⁻¹T−I‖ = {generalErr:E2}\n" +
                     $"  두 역행렬 사이 차이   : {methodDiff:E2}";
        Debug.Log(log);
        SaveCsv("hw4_inverse_speed.csv",
            "method,iterations,total_ms,frobenius_residual\n" +
            $"rigid_transpose,{speedTestIterations},{F((float)rigidMs)},{F(rigidErr)}\n" +
            $"matrix4x4_inverse,{speedTestIterations},{F((float)generalMs)},{F(generalErr)}\n");
    }

    // =================================================================
    // 계층 생성 / 편의 기능
    // =================================================================
    [ContextMenu("계층 생성 (Root-Shoulder-Elbow-Wrist-Hand)")]
    private void CreateDefaultHierarchy()
    {
        List<Transform> joints = CreateHierarchy(transform, 5, null);
        chain = joints.ToArray();
        RandomizeLocals(chain);
    }

    [ContextMenu("로컬 값 랜덤화")]
    private void RandomizeCurrent()
    {
        if (CheckChain()) RandomizeLocals(chain);
    }

    /// <summary>depth 단계의 부모-자식 계층을 만들어 Root → 끝 순서로 반환.</summary>
    private static List<Transform> CreateHierarchy(Transform parent, int depth, string prefix)
    {
        string[] names = { "Root", "Shoulder", "Elbow", "Wrist", "Hand" };
        var joints = new List<Transform>();
        Transform current = parent;
        for (int i = 0; i < depth; i++)
        {
            string jointName = (prefix == null && depth == names.Length) ? names[i] : $"{prefix ?? "Joint"}_{i}";
            // 관절 자체는 scale 1인 빈 오브젝트 (행렬 계산과 맞추기 위해)
            var go = new GameObject(jointName);
            go.transform.SetParent(current, false);
            if (prefix == null)
            {
                // 눈에 보이도록 작은 구를 "자식"으로 붙인다 (관절의 scale은 건드리지 않음)
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "Visual";
                DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(go.transform, false);
                visual.transform.localScale = Vector3.one * 0.08f;
            }
            joints.Add(go.transform);
            current = go.transform;
        }
        return joints;
    }

    private bool CheckChain()
    {
        if (chain == null || chain.Length < 2 || System.Array.Exists(chain, t => t == null))
        {
            Debug.LogWarning("[HW4] chain 배열을 채우거나 '계층 생성'을 먼저 실행하세요.");
            return false;
        }
        return true;
    }

    private static List<(Vector3, Quaternion, Vector3)> SaveLocals(IList<Transform> joints)
    {
        var list = new List<(Vector3, Quaternion, Vector3)>();
        foreach (Transform j in joints) list.Add((j.localPosition, j.localRotation, j.localScale));
        return list;
    }

    private static void RestoreLocals(IList<Transform> joints, List<(Vector3 p, Quaternion r, Vector3 s)> saved)
    {
        for (int i = 0; i < joints.Count; i++)
        {
            joints[i].localPosition = saved[i].p;
            joints[i].localRotation = saved[i].r;
            joints[i].localScale = saved[i].s;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (chain == null || chain.Length == 0 || System.Array.Exists(chain, t => t == null)) return;

        // 관절마다 누적 행렬로 world 위치를 직접 계산해 선으로 잇는다
        Matrix4x4 world = Matrix4x4.identity;
        Vector3 prev = Vector3.zero;
        Handles.color = Color.green;
        for (int i = 0; i < chain.Length; i++)
        {
            world = world * HomogeneousMath.LocalMatrix(chain[i]);
            Vector3 p = HomogeneousMath.TransformPoint(world, Vector3.zero);
            if (i > 0) Handles.DrawAAPolyLine(4f, prev, p);
            prev = p;
        }

        Transform last = chain[chain.Length - 1];
        Vector3 unity = last.position;
        float errMm = (prev - unity).magnitude * 1000f;
        Handles.color = Color.white;
        Handles.Label(prev + Vector3.up * 0.15f,
            $"{last.name}\n직접 계산: {prev.x:F4}, {prev.y:F4}, {prev.z:F4}\n" +
            $"Unity    : {unity.x:F4}, {unity.y:F4}, {unity.z:F4}\n오차: {errMm:E2} mm");
    }
#endif

    private static string F(float x) => x.ToString("E4", CultureInfo.InvariantCulture);

    private static void SaveCsv(string fileName, string content)
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Report"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content);
        Debug.Log($"[HW4] CSV 저장 완료: {path}");
    }
}
