using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 과제 2 실험 및 평가.
///
/// 컴포넌트 ⋮ 메뉴에서 실행:
///   0) "0. 행렬 구현 검증"        : 직접 만든 Rx/Ry/Rz가 Unity 내장 회전과 같은지 확인
///   1) "1. 고정 케이스 실험"      : yaw=30, roll=30, pitch 0~90 → CSV (표/그래프용)
///   2) "2. 심화: 최대 차이 탐색"  : yaw/pitch/roll 전 범위 격자 탐색
/// CSV는 저장소 루트의 Report 폴더에 저장된다.
/// </summary>
public class EulerOrderExperiment : MonoBehaviour
{
    [Header("실험 1 설정")]
    public float fixedYaw = 30f;
    public float fixedRoll = 30f;
    public float[] pitchValues = { 0f, 15f, 30f, 45f, 60f, 75f, 90f };

    [Header("실험 2 (심화) 설정")]
    [Tooltip("격자 간격(도). 5 → 73³ ≈ 39만 조합")]
    public float searchStep = 5f;

    // ---------------------------------------------------------------
    [ContextMenu("0. 행렬 구현 검증 (vs Unity 내장)")]
    private void VerifyAgainstUnity()
    {
        var sb = new StringBuilder("[HW2 검증] 직접 구현 vs Unity 내장 (3x3 Frobenius 차이)\n");
        float[] testAngles = { -170f, -90f, -45f, 0f, 30f, 60f, 90f, 135f };
        float worstAxis = 0f;
        foreach (float a in testAngles)
        {
            worstAxis = Mathf.Max(worstAxis,
                EulerComposer.FrobeniusDiff3x3(EulerComposer.RotationX(a), Matrix4x4.Rotate(Quaternion.AngleAxis(a, Vector3.right))),
                EulerComposer.FrobeniusDiff3x3(EulerComposer.RotationY(a), Matrix4x4.Rotate(Quaternion.AngleAxis(a, Vector3.up))),
                EulerComposer.FrobeniusDiff3x3(EulerComposer.RotationZ(a), Matrix4x4.Rotate(Quaternion.AngleAxis(a, Vector3.forward))));
        }
        sb.AppendLine($"  단일 축 Rx/Ry/Rz 최대 오차: {worstAxis:E2}");

        // 참고: Unity의 Quaternion.Euler(x,y,z)는 Z → X → Y 순서(= Ry*Rx*Rz)로 적용된다.
        // 즉 이 과제의 순서 A, B 어느 쪽과도 다른 제3의 순서다.
        float worstEuler = 0f;
        for (int i = 0; i < 20; i++)
        {
            float y = Random.Range(-180f, 180f), p = Random.Range(-180f, 180f), r = Random.Range(-180f, 180f);
            Matrix4x4 mine = EulerComposer.RotationY(y) * EulerComposer.RotationX(p) * EulerComposer.RotationZ(r);
            Matrix4x4 unity = Matrix4x4.Rotate(Quaternion.Euler(p, y, r));
            worstEuler = Mathf.Max(worstEuler, EulerComposer.FrobeniusDiff3x3(mine, unity));
        }
        sb.AppendLine($"  Ry*Rx*Rz vs Quaternion.Euler 최대 오차 (랜덤 20세트): {worstEuler:E2}");
        Debug.Log(sb.ToString());
    }

    // ---------------------------------------------------------------
    [ContextMenu("1. 고정 케이스 실험 (pitch sweep)")]
    private void RunPitchSweep()
    {
        var csv = new StringBuilder("pitch_deg,forward_angle_deg,up_angle_deg,total_rotation_angle_deg,forwardA_x,forwardA_y,forwardA_z,forwardB_x,forwardB_y,forwardB_z\n");
        var log = new StringBuilder($"[HW2 실험 1] yaw={fixedYaw}°, roll={fixedRoll}°\n  pitch | forward 차이 | up 차이 | 전체 자세 차이\n");

        foreach (float pitch in pitchValues)
        {
            Matrix4x4 rA = EulerComposer.ComposeOrderA(fixedYaw, pitch, fixedRoll);
            Matrix4x4 rB = EulerComposer.ComposeOrderB(fixedYaw, pitch, fixedRoll);

            Vector3 fA = rA.MultiplyVector(Vector3.forward), fB = rB.MultiplyVector(Vector3.forward);
            Vector3 uA = rA.MultiplyVector(Vector3.up),      uB = rB.MultiplyVector(Vector3.up);

            float forwardDiff = Vector3.Angle(fA, fB);                       // 명세가 요구한 지표
            float upDiff = Vector3.Angle(uA, uB);                            // 추가 지표
            float totalDiff = EulerComposer.RotationAngleBetween(rA, rB);    // 추가 지표

            csv.AppendLine(string.Join(",", F(pitch), F(forwardDiff), F(upDiff), F(totalDiff),
                F(fA.x), F(fA.y), F(fA.z), F(fB.x), F(fB.y), F(fB.z)));
            log.AppendLine($"  {pitch,5:F0} | {forwardDiff,10:F3}° | {upDiff,7:F3}° | {totalDiff,10:F3}°");
        }

        Debug.Log(log.ToString());
        SaveCsv("hw2_pitch_sweep.csv", csv.ToString());
    }

    // ---------------------------------------------------------------
    [ContextMenu("2. 심화: 최대 차이 탐색 (격자)")]
    private void RunGridSearch()
    {
        var bestForward = new List<(float diff, float y, float p, float r)>();
        var bestTotal = new List<(float diff, float y, float p, float r)>();
        int count = 0;

        for (float y = -180f; y <= 180f + 1e-3f; y += searchStep)
        for (float p = -180f; p <= 180f + 1e-3f; p += searchStep)
        for (float r = -180f; r <= 180f + 1e-3f; r += searchStep)
        {
            Matrix4x4 rA = EulerComposer.ComposeOrderA(y, p, r);
            Matrix4x4 rB = EulerComposer.ComposeOrderB(y, p, r);
            float fDiff = Vector3.Angle(rA.MultiplyVector(Vector3.forward), rB.MultiplyVector(Vector3.forward));
            float tDiff = EulerComposer.RotationAngleBetween(rA, rB);

            KeepTop(bestForward, (fDiff, y, p, r), 10);
            KeepTop(bestTotal, (tDiff, y, p, r), 10);
            count++;
        }

        var sb = new StringBuilder($"[HW2 심화] 격자 {searchStep}° 간격, {count}개 조합\n");
        sb.AppendLine("  forward 각도 차이 상위 10:");
        foreach (var b in bestForward) sb.AppendLine($"    {b.diff:F2}°  (yaw {b.y}, pitch {b.p}, roll {b.r})");
        sb.AppendLine("  전체 자세 차이 상위 10:");
        foreach (var b in bestTotal) sb.AppendLine($"    {b.diff:F2}°  (yaw {b.y}, pitch {b.p}, roll {b.r})");
        Debug.Log(sb.ToString());
    }

    // ---------------------------------------------------------------
    private static void KeepTop(List<(float diff, float y, float p, float r)> list,
                                (float diff, float y, float p, float r) item, int k)
    {
        if (list.Count == k && item.diff <= list[k - 1].diff) return;
        list.Add(item);
        list.Sort((a, b) => b.diff.CompareTo(a.diff));
        if (list.Count > k) list.RemoveAt(k);
    }

    private static string F(float x) => x.ToString("F4", CultureInfo.InvariantCulture);

    private static void SaveCsv(string fileName, string content)
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Report"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content);
        Debug.Log($"[HW2] CSV 저장 완료: {path}");
    }
}
