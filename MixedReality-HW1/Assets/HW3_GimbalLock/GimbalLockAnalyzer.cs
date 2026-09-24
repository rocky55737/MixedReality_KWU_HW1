using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 과제 3: Gimbal Lock 재현 및 자유도 손실 측정.
///
/// 사용법
///   1) 빈 GameObject에 붙이고 ⋮ 메뉴 → "모델 생성" (비행기 모양, roll이 눈에 보이도록)
///   2) Inspector 슬라이더로 yaw / pitch / roll 조절 → Quaternion.Euler 로 회전 적용 (Step 1)
///      Scene 뷰: 노랑 = yaw 회전축(월드 Y), 청록 = roll 회전축(물체 forward)
///                노랑/청록 곡선 = yaw, roll을 각각 ±sweep 만큼 돌릴 때 날개 끝이 그리는 궤적
///      pitch를 90°로 올리면 두 축과 두 궤적이 겹치는 것이 보인다 (스크린샷용).
///   3) ⋮ 메뉴의 실험 1~3 실행 → Console 출력 + Report 폴더에 CSV 저장
/// </summary>
[ExecuteAlways]
public class GimbalLockAnalyzer : MonoBehaviour
{
    [Header("Step 1. 오일러 각 (도)")]
    [Range(-180, 180)] public float yaw = 0f;
    [Range(-180, 180)] public float pitch = 0f;
    [Range(-180, 180)] public float roll = 0f;

    [Header("측정 설정")]
    [Tooltip("yaw / roll 에 더할 변화량 Δ (도)")]
    public float delta = 10f;
    public float[] pitchValues = { 0f, 30f, 60f, 80f, 89f, 90f, 91f };
    public float[] deltaValues = { 1f, 5f, 10f, 30f };

    [Header("실험 3 (pitch 90° 근처 관찰) 설정")]
    public float observeYaw = 30f;
    public float observeRoll = 20f;
    public float[] nearNinetyPitches = { 85f, 89f, 89.9f, 89.99f, 89.999f, 90f, 90.001f, 90.01f, 90.1f, 91f, 95f };

    [Header("시각화")]
    [Tooltip("궤적 곡선을 그릴 때 yaw/roll을 ± 몇 도까지 돌려볼지")]
    public float sweepRange = 40f;

    private void Update()
    {
        // Step 1: 내장 함수로 회전 적용 (이 과제의 핵심은 특이점 관찰)
        transform.rotation = Quaternion.Euler(pitch, yaw, roll);
    }

    /// <summary>
    /// 명세 Step 2 절차를 실제 Transform으로 그대로 수행하는 버전.
    /// (값을 바꿔 forward를 기록하고, 원래대로 복구)
    /// </summary>
    public float MeasureOnTransform(float p, float y, float r, float d, out float rollChangeLength)
    {
        Quaternion original = transform.rotation;

        transform.rotation = Quaternion.Euler(p, y, r);
        Vector3 f0 = transform.forward;                  // 1. f0 기록

        transform.rotation = Quaternion.Euler(p, y + d, r);
        Vector3 fYaw = transform.forward;                // 2. yaw만 +Δ
        transform.rotation = Quaternion.Euler(p, y, r);  //    복구

        transform.rotation = Quaternion.Euler(p, y, r + d);
        Vector3 fRoll = transform.forward;               // 3. roll만 +Δ

        transform.rotation = original;                   //    복구

        Vector3 dYaw = fYaw - f0, dRoll = fRoll - f0;    // 4. 변화 벡터
        rollChangeLength = dRoll.magnitude;
        return GimbalLockMetrics.CosineSimilarity(dYaw, dRoll); // 5. 코사인 유사도
    }

    // =================================================================
    [ContextMenu("1. pitch별 코사인 유사도 측정")]
    private void RunPitchSweep()
    {
        var csv = new StringBuilder("pitch_deg,delta_deg,forward_cos_spec,forward_yaw_change_len,forward_roll_change_len,basis_cos,axis_cos,theory_minus_sin_pitch\n");
        var log = new StringBuilder($"[HW3 실험 1] 기준 자세 yaw=0, roll=0, Δ={delta}°\n" +
                                    "  pitch | forward(명세) | |Δf_roll|  | 세 축 기준 | 회전축 기준 | 이론 -sin(p)\n");

        foreach (float p in pitchValues)
        {
            float specCos = MeasureOnTransform(p, 0f, 0f, delta, out _);
            GimbalLockMetrics.ForwardCosine(p, 0f, 0f, delta, out float yawLen, out float rollLen);
            float basisCos = GimbalLockMetrics.BasisCosine(p, 0f, 0f, delta);
            float axisCos = GimbalLockMetrics.AxisCosine(p, 0f, 0f, delta);
            float theory = -Mathf.Sin(p * Mathf.Deg2Rad);

            csv.AppendLine(string.Join(",", F(p), F(delta), F(specCos), F(yawLen), F(rollLen), F(basisCos), F(axisCos), F(theory)));
            log.AppendLine($"  {p,5:F0} | {S(specCos),12} | {rollLen,9:E1} | {S(basisCos),9} | {S(axisCos),10} | {theory,8:F4}");
        }
        Debug.Log(log.ToString());
        SaveCsv("hw3_pitch_sweep.csv", csv.ToString());
    }

    // =================================================================
    [ContextMenu("2. Δ 크기 민감도 측정")]
    private void RunDeltaSensitivity()
    {
        var csv = new StringBuilder("delta_deg,pitch_deg,basis_cos,axis_cos,theory_minus_sin_pitch\n");
        var log = new StringBuilder("[HW3 실험 2] Δ별 '세 축 기준' 코사인 유사도 (괄호: 회전축 기준)\n");

        foreach (float d in deltaValues)
        {
            log.Append($"  Δ={d,4:F0}° :");
            foreach (float p in pitchValues)
            {
                float basisCos = GimbalLockMetrics.BasisCosine(p, 0f, 0f, d);
                float axisCos = GimbalLockMetrics.AxisCosine(p, 0f, 0f, d);
                csv.AppendLine(string.Join(",", F(d), F(p), F(basisCos), F(axisCos), F(-Mathf.Sin(p * Mathf.Deg2Rad))));
                log.Append($"  p{p:F0}={S(basisCos)}({S(axisCos)})");
            }
            log.AppendLine();
        }
        Debug.Log(log.ToString());
        SaveCsv("hw3_delta_sensitivity.csv", csv.ToString());
    }

    // =================================================================
    [ContextMenu("3. pitch 90° 근처 Quaternion.Euler 거동 관찰")]
    private void RunNearNinetyObservation()
    {
        var csv = new StringBuilder("pitch_in,yaw_in,roll_in,euler_out_x,euler_out_y,euler_out_z,roundtrip_error_deg,axis_cos\n");
        var log = new StringBuilder($"[HW3 실험 3] 입력 (pitch, yaw={observeYaw}, roll={observeRoll}) → Quaternion.Euler → .eulerAngles 로 되읽기\n");

        foreach (float p in nearNinetyPitches)
        {
            Quaternion q = Quaternion.Euler(p, observeYaw, observeRoll);
            Vector3 back = q.eulerAngles;                                     // Unity가 되돌려주는 오일러 각
            float roundTrip = Quaternion.Angle(q, Quaternion.Euler(back));    // 같은 자세인가?
            float axisCos = GimbalLockMetrics.AxisCosine(p, observeYaw, observeRoll, 1f);

            csv.AppendLine(string.Join(",", F(p), F(observeYaw), F(observeRoll), F(back.x), F(back.y), F(back.z), F(roundTrip), F(axisCos)));
            log.AppendLine($"  pitch {p,8:F3} → eulerAngles ({back.x,8:F3}, {back.y,8:F3}, {back.z,8:F3})  자세 오차 {roundTrip:F4}°");
        }

        // pitch = 90° 에서는 yaw와 roll을 같은 양만큼 함께 바꿔도 자세가 그대로다 → 자유도 1개가 사라졌다는 직접 증거
        log.AppendLine("  [자유도 손실 직접 확인] yaw와 roll에 같은 k를 더했을 때 자세 변화량:");
        foreach (float p in new[] { 0f, 60f, 89f, 90f })
        {
            Quaternion baseQ = Quaternion.Euler(p, observeYaw, observeRoll);
            float k10 = Quaternion.Angle(baseQ, Quaternion.Euler(p, observeYaw + 10f, observeRoll + 10f));
            float k45 = Quaternion.Angle(baseQ, Quaternion.Euler(p, observeYaw + 45f, observeRoll + 45f));
            log.AppendLine($"    pitch {p,3:F0}° : k=10 → {k10:F4}°,  k=45 → {k45:F4}°");
        }

        Debug.Log(log.ToString());
        SaveCsv("hw3_near90.csv", csv.ToString());
    }

    // =================================================================
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position;
        Quaternion q0 = Quaternion.Euler(pitch, yaw, roll);

        // 두 회전축
        Vector3 yawAxis = Vector3.up;             // yaw: 항상 월드 Y
        Vector3 rollAxis = q0 * Vector3.forward;  // roll: 물체 자신의 forward
        Handles.color = Color.yellow;
        Handles.DrawAAPolyLine(3f, origin - yawAxis * 1.5f, origin + yawAxis * 1.5f);
        Handles.color = Color.cyan;
        Handles.DrawAAPolyLine(3f, origin - rollAxis * 1.5f, origin + rollAxis * 1.5f);

        // 날개 끝 점이 yaw / roll 변화에 따라 그리는 궤적
        Vector3 localTip = new Vector3(0.7f, 0.2f, 0.3f);
        DrawSweep(origin, localTip, true, Color.yellow);
        DrawSweep(origin, localTip, false, Color.cyan);

        float axisCos = GimbalLockMetrics.AxisCosine(pitch, yaw, roll, 1f);
        Handles.color = Color.white;
        Handles.Label(origin + Vector3.up * 1.7f,
            $"pitch {pitch:F1}°\n회전축 코사인 {axisCos:F4}  (두 축 사이 각 {Mathf.Acos(Mathf.Abs(axisCos)) * Mathf.Rad2Deg:F1}°)");
    }

    private void DrawSweep(Vector3 origin, Vector3 localPoint, bool sweepYaw, Color color)
    {
        const int steps = 40;
        var points = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float k = Mathf.Lerp(-sweepRange, sweepRange, i / (float)steps);
            Quaternion q = sweepYaw ? Quaternion.Euler(pitch, yaw + k, roll)
                                    : Quaternion.Euler(pitch, yaw, roll + k);
            points[i] = origin + q * localPoint;
        }
        Handles.color = color;
        Handles.DrawAAPolyLine(3f, points);
    }
#endif

    [ContextMenu("모델 생성 (비행기 모양)")]
    private void CreateModel()
    {
        MakePart("Body", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.25f, 0.15f, 1f), new Color(0.8f, 0.8f, 0.85f));
        MakePart("Nose", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.5f), Vector3.one * 0.2f, Color.red);
        MakePart("Wing", PrimitiveType.Cube, new Vector3(0f, 0f, 0.05f), new Vector3(1.4f, 0.03f, 0.3f), new Color(0.3f, 0.5f, 1f));
        MakePart("Tail", PrimitiveType.Cube, new Vector3(0f, 0.15f, -0.4f), new Vector3(0.03f, 0.3f, 0.2f), Color.green);
    }

    private void MakePart(string partName, PrimitiveType type, Vector3 localPos, Vector3 localScale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = partName;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        DestroyImmediate(go.GetComponent<Collider>());
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = new Material(renderer.sharedMaterial) { color = color };
    }

    // =================================================================
    private static string F(float x) => float.IsNaN(x) ? "NaN" : x.ToString("F6", CultureInfo.InvariantCulture);
    private static string S(float x) => float.IsNaN(x) ? "NaN" : x.ToString("F4");

    private static void SaveCsv(string fileName, string content)
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Report"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content);
        Debug.Log($"[HW3] CSV 저장 완료: {path}");
    }
}
