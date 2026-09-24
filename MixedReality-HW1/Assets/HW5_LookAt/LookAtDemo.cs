using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 과제 5 Step 3: 직접 만든 view 행렬을 적용한 카메라 vs Unity LookAt 카메라를 나란히 배치.
///
/// 사용법
///   1) 빈 GameObject에 붙이고 ⋮ 메뉴 → "씬 구성 (카메라 2대 + 타겟 + 테스트 물체)"
///      이 GameObject의 위치 = eye. Scene 뷰에서 이 오브젝트나 Target을 움직여 본다.
///   2) Game 뷰: 왼쪽 = Unity 내장 LookAt 카메라, 오른쪽 = 직접 만든 view 행렬 카메라
///      두 화면이 똑같이 보이면 성공 (스크린샷용)
///   3) Scene 뷰: eye 위치에 직접 계산한 right(빨강) / up(초록) / forward(파랑) 표시, 라벨에 오차
/// </summary>
[ExecuteAlways]
public class LookAtDemo : MonoBehaviour
{
    public Transform target;
    public Vector3 worldUp = Vector3.up;

    [Tooltip("Transform.LookAt을 적용할 Unity 기준 카메라")] public Camera unityCamera;
    [Tooltip("직접 만든 view 행렬을 적용할 카메라")] public Camera myCamera;
    [Tooltip("view space 좌표를 비교할 점들")] public Transform[] testPoints;

    private LookAtBuilder.Basis basis;
    private Matrix4x4 view;
    private float errorVsLocal, errorVsCamera, maxPointError;

    private void Update()
    {
        if (target == null) return;
        Vector3 eye = transform.position;

        // 직접 계산
        basis = LookAtBuilder.ComputeBasis(eye, target.position, worldUp);
        view = LookAtBuilder.BuildViewMatrix(basis, eye);

        // Unity 내장 카메라
        if (unityCamera != null)
        {
            unityCamera.transform.position = eye;
            unityCamera.transform.LookAt(target, worldUp);

            errorVsLocal = LookAtBuilder.FrobeniusDiff(view, unityCamera.transform.worldToLocalMatrix);
            errorVsCamera = LookAtBuilder.FrobeniusDiff(LookAtBuilder.ToUnityCameraConvention(view), unityCamera.worldToCameraMatrix);

            maxPointError = 0f;
            if (testPoints != null)
                foreach (Transform p in testPoints)
                {
                    if (p == null) continue;
                    Vector3 mine = view.MultiplyPoint3x4(p.position);
                    Vector3 unity = unityCamera.transform.worldToLocalMatrix.MultiplyPoint3x4(p.position);
                    maxPointError = Mathf.Max(maxPointError, (mine - unity).magnitude);
                }
        }

        // 직접 만든 view 행렬을 실제 렌더링 카메라에 적용
        if (myCamera != null)
        {
            myCamera.worldToCameraMatrix = LookAtBuilder.ToUnityCameraConvention(view);
            // 아래는 Scene 뷰에서 카메라 아이콘 위치를 맞추기 위한 표시용 (렌더링은 위 행렬이 결정)
            myCamera.transform.SetPositionAndRotation(eye, LookAtBuilder.CameraToWorld(basis, eye).rotation);
        }
    }

    private void OnDisable()
    {
        if (myCamera != null) myCamera.ResetWorldToCameraMatrix();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (target == null) return;
        Vector3 eye = transform.position;

        Handles.color = Color.red;    Handles.DrawAAPolyLine(4f, eye, eye + basis.right);
        Handles.color = Color.green;  Handles.DrawAAPolyLine(4f, eye, eye + basis.up);
        Handles.color = Color.blue;   Handles.DrawAAPolyLine(4f, eye, eye + basis.forward);
        Handles.color = Color.gray;   Handles.DrawDottedLine(eye, target.position, 4f);

        float angleToUp = Vector3.Angle(target.position - eye, worldUp);
        Handles.color = Color.white;
        Handles.Label(eye + Vector3.up * 0.4f,
            $"forward-up 각도 {angleToUp:F2}°,  |cross(up,fwd)| = {basis.rawRightLength:F5}\n" +
            $"‖직접 − worldToLocalMatrix‖ = {errorVsLocal:E2}\n" +
            $"‖직접(Z반전) − worldToCameraMatrix‖ = {errorVsCamera:E2}\n" +
            $"테스트 점 최대 오차 = {maxPointError:E2}");
    }
#endif

    [ContextMenu("씬 구성 (카메라 2대 + 타겟 + 테스트 물체)")]
    private void SetupScene()
    {
        transform.position = new Vector3(4f, 3f, -5f); // eye: 대각선 위쪽

        if (target == null)
        {
            target = MakeObject("Target", PrimitiveType.Sphere, Vector3.zero, 0.4f, Color.yellow).transform;
        }
        if (unityCamera == null) unityCamera = MakeCamera("UnityLookAtCamera", new Rect(0f, 0f, 0.5f, 1f), true);
        if (myCamera == null) myCamera = MakeCamera("MyViewMatrixCamera", new Rect(0.5f, 0f, 0.5f, 1f), false);

        if (testPoints == null || testPoints.Length == 0)
        {
            Color[] colors = { Color.red, Color.green, Color.blue, Color.magenta, Color.cyan };
            testPoints = new Transform[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                float a = i * Mathf.PI * 2f / colors.Length;
                Vector3 pos = new Vector3(Mathf.Cos(a) * 1.5f, (i % 2) * 0.8f, Mathf.Sin(a) * 1.5f);
                testPoints[i] = MakeObject($"TestPoint_{i}", PrimitiveType.Cube, pos, 0.4f, colors[i]).transform;
            }
        }
        Update();
    }

    private static Camera MakeCamera(string cameraName, Rect viewport, bool keepListener)
    {
        var go = new GameObject(cameraName);
        var cam = go.AddComponent<Camera>();
        cam.rect = viewport; // 화면을 좌우로 나눠서 표시
        if (keepListener) go.AddComponent<AudioListener>();
        return cam;
    }

    private static GameObject MakeObject(string objName, PrimitiveType type, Vector3 pos, float size, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = objName;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = new Material(renderer.sharedMaterial) { color = color };
        return go;
    }
}
