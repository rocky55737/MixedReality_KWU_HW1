using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 과제 2 Step 3: 순서 A / 순서 B 결과를 두 화살표로 나란히 보여준다.
///
/// 사용법
///   1) 빈 GameObject에 붙인다.
///   2) 컴포넌트 ⋮ 메뉴 → "화살표 오브젝트 생성" (빨강 = 순서 A, 파랑 = 순서 B)
///   3) Inspector의 yaw / pitch / roll 슬라이더를 움직인다.
///   화살표 몸통 = forward(로컬 +Z), 위쪽 작은 날개 = up(로컬 +Y) → roll 차이까지 보인다.
///   Scene 뷰 라벨에 forward 각도 차이와 전체 자세 차이가 표시된다.
/// </summary>
[ExecuteAlways]
public class EulerOrderVisualizer : MonoBehaviour
{
    [Range(-180, 180)] public float yaw = 30f;    // Y축
    [Range(-180, 180)] public float pitch = 0f;   // X축
    [Range(-180, 180)] public float roll = 30f;   // Z축

    [Tooltip("순서 A 결과를 표시할 오브젝트")] public Transform arrowA;
    [Tooltip("순서 B 결과를 표시할 오브젝트")] public Transform arrowB;

    [Tooltip("두 화살표 사이 간격")] public float spacing = 1.5f;

    // 계산 결과 (다른 스크립트나 디버깅에서 확인용)
    public Vector3 ForwardA { get; private set; }
    public Vector3 ForwardB { get; private set; }
    public float ForwardAngleDiff { get; private set; }
    public float TotalAngleDiff { get; private set; }

    private void Update()
    {
        Matrix4x4 rA = EulerComposer.ComposeOrderA(yaw, pitch, roll);
        Matrix4x4 rB = EulerComposer.ComposeOrderB(yaw, pitch, roll);

        // 명세: 결과 행렬을 local forward (0,0,1)에 적용
        ForwardA = rA.MultiplyVector(Vector3.forward);
        ForwardB = rB.MultiplyVector(Vector3.forward);
        ForwardAngleDiff = Vector3.Angle(ForwardA, ForwardB);
        TotalAngleDiff = EulerComposer.RotationAngleBetween(rA, rB);

        // 표시용: 직접 만든 행렬의 열벡터(= 회전된 forward, up)로 오브젝트 자세를 맞춘다.
        // (계산은 이미 끝났고, 여기서 LookRotation은 화면에 보여주기 위한 용도일 뿐)
        if (arrowA != null)
        {
            arrowA.position = transform.position + Vector3.left * spacing * 0.5f;
            arrowA.rotation = Quaternion.LookRotation(ForwardA, rA.MultiplyVector(Vector3.up));
        }
        if (arrowB != null)
        {
            arrowB.position = transform.position + Vector3.right * spacing * 0.5f;
            arrowB.rotation = Quaternion.LookRotation(ForwardB, rB.MultiplyVector(Vector3.up));
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 한 원점에서 두 forward 벡터를 겹쳐 그려 각도 차이를 직접 보여준다.
        Vector3 origin = transform.position + Vector3.down * 1.2f;
        Handles.color = Color.red;
        Handles.DrawAAPolyLine(4f, origin, origin + ForwardA);
        Handles.color = new Color(0.2f, 0.5f, 1f);
        Handles.DrawAAPolyLine(4f, origin, origin + ForwardB);

        Handles.color = Color.white;
        Handles.Label(transform.position + Vector3.up * 1.2f,
            $"yaw {yaw:F0}°, pitch {pitch:F0}°, roll {roll:F0}°\n" +
            $"forward 각도 차이: {ForwardAngleDiff:F2}°\n" +
            $"전체 자세 차이: {TotalAngleDiff:F2}°");
    }
#endif

    [ContextMenu("화살표 오브젝트 생성")]
    private void CreateArrows()
    {
        if (arrowA == null) arrowA = BuildArrow("Arrow_OrderA (Rz*Ry*Rx)", Color.red);
        if (arrowB == null) arrowB = BuildArrow("Arrow_OrderB (Rx*Ry*Rz)", new Color(0.2f, 0.5f, 1f));
        Update();
    }

    /// <summary>기본 도형으로 화살표를 조립: 몸통(+Z 방향) + 머리 + 위쪽 날개(+Y 방향)</summary>
    private Transform BuildArrow(string name, Color color)
    {
        var root = new GameObject(name).transform;
        root.SetParent(transform, false);

        // 몸통: Cylinder는 원래 Y축으로 서 있으므로 X축으로 90° 눕혀 +Z를 향하게 한다.
        MakePart(PrimitiveType.Cylinder, root, color,
            new Vector3(0f, 0f, 0.4f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.06f, 0.4f, 0.06f));
        // 머리
        MakePart(PrimitiveType.Sphere, root, color,
            new Vector3(0f, 0f, 0.85f), Quaternion.identity, Vector3.one * 0.16f);
        // 날개: up 방향 표시 (roll을 보기 위함), 흰색
        MakePart(PrimitiveType.Cube, root, Color.white,
            new Vector3(0f, 0.12f, 0.15f), Quaternion.identity, new Vector3(0.02f, 0.2f, 0.2f));
        return root;
    }

    private static void MakePart(PrimitiveType type, Transform parent, Color color,
                                 Vector3 localPos, Quaternion localRot, Vector3 localScale)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;
        DestroyImmediate(go.GetComponent<Collider>());

        var renderer = go.GetComponent<Renderer>();
        var mat = new Material(renderer.sharedMaterial) { color = color }; // 파이프라인(URP/Built-in) 무관
        renderer.sharedMaterial = mat;
    }
}
