using UnityEngine;

/// <summary>
/// 과제 5 Step 1~2: Look-At view 행렬을 직접 구성한다.
/// (Transform.LookAt / Matrix4x4.LookAt 사용 금지. Vector3.Cross / Dot / Normalize만 사용)
///
/// View 행렬 = world 좌표를 카메라 좌표로 바꾸는 행렬
///   1) 카메라(eye)를 원점으로 옮기고      : T(-eye)
///   2) 카메라의 축들을 x, y, z축에 맞춘다 : R (행 = right, up, forward)
///   View = R · T(-eye)
/// </summary>
public static class LookAtBuilder
{
    public struct Basis
    {
        public Vector3 right, up, forward;
        public float rawRightLength; // 정규화 전 cross(worldUp, forward)의 길이 (특이점 실험용)
    }

    /// <summary>Step 1: 기저 벡터 직접 계산</summary>
    public static Basis ComputeBasis(Vector3 eye, Vector3 target, Vector3 worldUp)
    {
        Basis b;
        b.forward = Vector3.Normalize(target - eye);

        Vector3 rawRight = Vector3.Cross(worldUp, b.forward);
        // |cross(a, b)| = |a||b| sinθ  → forward가 worldUp과 평행해질수록 0에 가까워진다
        b.rawRightLength = Mathf.Sqrt(Vector3.Dot(rawRight, rawRight));
        // 참고: Unity의 Vector3.Normalize는 길이가 1e-5보다 작으면 (0,0,0)을 반환한다
        b.right = Vector3.Normalize(rawRight);

        b.up = Vector3.Cross(b.forward, b.right); // 이미 서로 수직인 단위벡터라 정규화 불필요
        return b;
    }

    /// <summary>기저 벡터 세 개를 "행"으로 배치한 회전 행렬 R</summary>
    public static Matrix4x4 RotationFromBasisRows(Basis b)
    {
        Matrix4x4 r = Matrix4x4.identity;
        r.m00 = b.right.x;    r.m01 = b.right.y;    r.m02 = b.right.z;
        r.m10 = b.up.x;       r.m11 = b.up.y;       r.m12 = b.up.z;
        r.m20 = b.forward.x;  r.m21 = b.forward.y;  r.m22 = b.forward.z;
        return r;
    }

    /// <summary>이동 행렬 T(t)</summary>
    public static Matrix4x4 Translation(Vector3 t)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m03 = t.x;  m.m13 = t.y;  m.m23 = t.z;
        return m;
    }

    /// <summary>
    /// Step 2: View = R · T(-eye)
    /// 곱해 보면 이동 열은 -(right·eye, up·eye, forward·eye) 가 된다.
    /// </summary>
    public static Matrix4x4 BuildViewMatrix(Basis b, Vector3 eye)
    {
        return RotationFromBasisRows(b) * Translation(-eye);
    }

    public static Matrix4x4 BuildViewMatrix(Vector3 eye, Vector3 target, Vector3 worldUp)
    {
        return BuildViewMatrix(ComputeBasis(eye, target, worldUp), eye);
    }

    /// <summary>
    /// Unity Camera.worldToCameraMatrix 규약으로 변환.
    /// Unity의 Transform은 카메라가 +Z를 바라보지만, worldToCameraMatrix는 OpenGL 규약이라
    /// 카메라가 -Z를 바라본다. → view 행렬의 세 번째 행(forward 행) 부호를 뒤집으면 된다.
    /// </summary>
    public static Matrix4x4 ToUnityCameraConvention(Matrix4x4 view)
    {
        view.m20 = -view.m20;  view.m21 = -view.m21;  view.m22 = -view.m22;  view.m23 = -view.m23;
        return view;
    }

    /// <summary>표시용: view의 역(카메라 → world). 기저 벡터를 "열"로 배치하고 위치 = eye.</summary>
    public static Matrix4x4 CameraToWorld(Basis b, Vector3 eye)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = b.right.x;  m.m01 = b.up.x;  m.m02 = b.forward.x;  m.m03 = eye.x;
        m.m10 = b.right.y;  m.m11 = b.up.y;  m.m12 = b.forward.y;  m.m13 = eye.y;
        m.m20 = b.right.z;  m.m21 = b.up.z;  m.m22 = b.forward.z;  m.m23 = eye.z;
        return m;
    }

    public static float FrobeniusDiff(Matrix4x4 a, Matrix4x4 b)
    {
        float sum = 0f;
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 4; col++)
            {
                float d = a[row, col] - b[row, col];
                sum += d * d;
            }
        return Mathf.Sqrt(sum);
    }

    /// <summary>‖R·Rᵀ − I‖: 기저가 정규직교(서로 수직, 길이 1)에서 얼마나 벗어났는지</summary>
    public static float OrthonormalityError(Basis b)
    {
        Vector3[] rows = { b.right, b.up, b.forward };
        float sum = 0f;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                float d = Vector3.Dot(rows[i], rows[j]) - (i == j ? 1f : 0f);
                sum += d * d;
            }
        return Mathf.Sqrt(sum);
    }
}
