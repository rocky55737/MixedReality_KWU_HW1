using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 과제 5 실험 및 평가. 빈 GameObject에 붙이고 ⋮ 메뉴에서 실행.
///   실험 1: 정상 케이스 N세트 → 직접 만든 view 행렬 vs Unity (Frobenius norm)
///   실험 2: 특이 케이스 → forward와 worldUp 사이 각도를 좁혀가며 right 벡터 관찰
/// 측정용 Unity 카메라는 임시로 만들었다가 삭제한다. CSV는 Report 폴더에 저장.
/// </summary>
public class LookAtExperiment : MonoBehaviour
{
    [Header("실험 1")]
    public int normalSets = 10;
    public int testPointsPerSet = 20;

    [Header("실험 2")]
    public float[] anglesToUp = { 90f, 45f, 20f, 10f, 5f, 1f, 0.1f, 0.01f, 0.001f, 0f };
    [Tooltip("민감도 측정: forward를 이만큼(도) 살짝 흔들었을 때 right가 몇 도 바뀌는가")]
    public float perturbationDeg = 0.001f;
    public int perturbationSamples = 16;
    [Tooltip("수평 방향 각도(도). 축에 딱 맞는 특수한 경우를 피하기 위함")]
    public float azimuthDeg = 30f;

    private static readonly Vector3 WorldUp = Vector3.up;

    // =================================================================
    [ContextMenu("실험 1. 정상 케이스 (N세트)")]
    private void RunNormalCases()
    {
        Camera cam = CreateTempCamera();
        var csv = new StringBuilder("set,eye_x,eye_y,eye_z,target_x,target_y,target_z,frob_vs_worldToLocal,frob_vs_worldToCamera,max_point_error\n");
        var log = new StringBuilder($"[HW5 실험 1] 정상 케이스 {normalSets}세트 (worldUp = (0,1,0))\n  set | ‖vs worldToLocal‖ | ‖vs worldToCamera‖ | 점 최대 오차\n");

        for (int s = 0; s < normalSets; s++)
        {
            // eye: 대각선 위쪽 / target: 원점 근처
            Vector2 h = Random.insideUnitCircle.normalized * Random.Range(3f, 8f);
            Vector3 eye = new Vector3(h.x, Random.Range(1.5f, 6f), h.y);
            Vector3 target = Random.insideUnitSphere * 0.5f;

            Matrix4x4 mine = LookAtBuilder.BuildViewMatrix(eye, target, WorldUp);

            cam.transform.position = eye;
            cam.transform.LookAt(target, WorldUp);
            float frobLocal = LookAtBuilder.FrobeniusDiff(mine, cam.transform.worldToLocalMatrix);
            float frobCamera = LookAtBuilder.FrobeniusDiff(LookAtBuilder.ToUnityCameraConvention(mine), cam.worldToCameraMatrix);

            float maxPointErr = 0f;
            for (int k = 0; k < testPointsPerSet; k++)
            {
                Vector3 p = Random.insideUnitSphere * 5f;
                Vector3 a = mine.MultiplyPoint3x4(p);
                Vector3 b = cam.transform.worldToLocalMatrix.MultiplyPoint3x4(p);
                maxPointErr = Mathf.Max(maxPointErr, (a - b).magnitude);
            }

            csv.AppendLine(string.Join(",", s + 1, F(eye.x), F(eye.y), F(eye.z), F(target.x), F(target.y), F(target.z), F(frobLocal), F(frobCamera), F(maxPointErr)));
            log.AppendLine($"  {s + 1,3} | {frobLocal,15:E2} | {frobCamera,16:E2} | {maxPointErr:E2}");
        }

        DestroyImmediate(cam.gameObject);
        Debug.Log(log.ToString());
        SaveCsv("hw5_normal_cases.csv", csv.ToString());
    }

    // =================================================================
    [ContextMenu("실험 2. 특이 케이스 (forward ≈ worldUp)")]
    private void RunSingularCases()
    {
        Camera cam = CreateTempCamera();
        var csv = new StringBuilder("direction,angle_to_axis_deg,raw_right_length,theory_sin,normalized_right_length,orthonormality_error,frob_vs_unity,right_change_deg_per_perturb,amplification\n");
        var log = new StringBuilder($"[HW5 실험 2] forward를 worldUp 축에 가깝게 (민감도: forward를 {perturbationDeg}° 흔듦)\n" +
                                    "  방향 | 각도(°) | |cross| 정규화 전 | 이론 sinθ | 정규화 후 길이 | ‖RRᵀ−I‖ | ‖vs Unity‖ | right 변화(°) | 증폭 배율\n");
        Vector3 eye = Vector3.zero;
        float az = azimuthDeg * Mathf.Deg2Rad;

        foreach (int dir in new[] { +1, -1 }) // +1: 거의 바로 위를 봄, -1: 거의 바로 아래를 봄
        {
            string dirName = dir > 0 ? "up" : "down";
            foreach (float angle in anglesToUp)
            {
                // worldUp 축(위 또는 아래)과 angle 만큼 떨어진 forward 방향
                float t = angle * Mathf.Deg2Rad;
                Vector3 forward = new Vector3(Mathf.Sin(t) * Mathf.Cos(az), dir * Mathf.Cos(t), Mathf.Sin(t) * Mathf.Sin(az));
                Vector3 target = eye + forward * 5f;

                LookAtBuilder.Basis b = LookAtBuilder.ComputeBasis(eye, target, WorldUp);
                float normalizedLen = b.right.magnitude;
                float ortho = LookAtBuilder.OrthonormalityError(b);

                cam.transform.position = eye;
                cam.transform.LookAt(target, WorldUp);
                float frobUnity = LookAtBuilder.FrobeniusDiff(LookAtBuilder.BuildViewMatrix(b, eye), cam.transform.worldToLocalMatrix);

                // 민감도: forward를 아주 조금 흔들었을 때 right 방향이 얼마나 크게 바뀌는가
                float maxRightChange = float.NaN;
                if (normalizedLen > 0.5f)
                {
                    maxRightChange = 0f;
                    for (int k = 0; k < perturbationSamples; k++)
                    {
                        Vector3 axis = Vector3.Normalize(Vector3.Cross(forward, Random.onUnitSphere));
                        Vector3 f2 = Quaternion.AngleAxis(perturbationDeg, axis) * forward;
                        LookAtBuilder.Basis b2 = LookAtBuilder.ComputeBasis(eye, eye + f2 * 5f, WorldUp);
                        maxRightChange = Mathf.Max(maxRightChange, Vector3.Angle(b.right, b2.right));
                    }
                }
                float amplification = maxRightChange / perturbationDeg;

                csv.AppendLine(string.Join(",", dirName, F(angle), F(b.rawRightLength), F(Mathf.Sin(t)), F(normalizedLen), F(ortho), F(frobUnity), F(maxRightChange), F(amplification)));
                log.AppendLine($"  {dirName,4} | {angle,7:G4} | {b.rawRightLength,12:E3} | {Mathf.Sin(t),9:E3} | {normalizedLen,10:F4} | {ortho,8:E2} | {frobUnity,9:E2} | {S(maxRightChange),10} | {S(amplification)}");
            }
        }

        DestroyImmediate(cam.gameObject);
        Debug.Log(log.ToString());
        SaveCsv("hw5_singular_cases.csv", csv.ToString());
    }

    // =================================================================
    private static Camera CreateTempCamera()
    {
        var go = new GameObject("TempLookAtCamera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.enabled = false; // 렌더링은 필요 없고 행렬만 사용
        return cam;
    }

    private static string F(float x) => float.IsNaN(x) ? "NaN" : x.ToString("E6", CultureInfo.InvariantCulture);
    private static string S(float x) => float.IsNaN(x) ? "NaN" : x.ToString("G4");

    private static void SaveCsv(string fileName, string content)
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Report"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content);
        Debug.Log($"[HW5] CSV 저장 완료: {path}");
    }
}
