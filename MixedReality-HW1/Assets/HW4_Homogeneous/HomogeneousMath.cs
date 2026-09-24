using UnityEngine;

/// <summary>
/// 과제 4 Step 1, Step 4: 4x4 동차 변환 행렬을 원소 단위로 직접 만들고, 직접 역변환한다.
/// (Matrix4x4.TRS 사용 금지 → 모든 원소를 손으로 대입)
///
/// 동차 변환 행렬 모양
///   | R11 R12 R13 tx |
///   | R21 R22 R23 ty |      p_world = T · p_local   (p = (x, y, z, 1))
///   | R31 R32 R33 tz |      → 회전 먼저, 그다음 이동
///   |  0   0   0   1 |
/// </summary>
public static class HomogeneousMath
{
    /// <summary>
    /// 쿼터니언 → 3x3 회전 행렬 (표준 공식). 결과는 Matrix4x4의 왼쪽 위 3x3에 담는다.
    /// Transform은 회전을 쿼터니언으로 저장하므로, 행렬로 쓰려면 이 변환이 필요하다.
    /// </summary>
    public static Matrix4x4 RotationFromQuaternion(Quaternion q)
    {
        // 단위 쿼터니언이어야 순수 회전이 된다
        float len = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        float x = q.x / len, y = q.y / len, z = q.z / len, w = q.w / len;

        Matrix4x4 r = Matrix4x4.identity;
        r.m00 = 1f - 2f * (y * y + z * z);  r.m01 = 2f * (x * y - w * z);       r.m02 = 2f * (x * z + w * y);
        r.m10 = 2f * (x * y + w * z);       r.m11 = 1f - 2f * (x * x + z * z);  r.m12 = 2f * (y * z - w * x);
        r.m20 = 2f * (x * z - w * y);       r.m21 = 2f * (y * z + w * x);       r.m22 = 1f - 2f * (x * x + y * y);
        return r;
    }

    /// <summary>Step 1: 회전 R(3x3)과 이동 t로 4x4 행렬을 원소 하나하나 직접 채운다.</summary>
    public static Matrix4x4 BuildTransform(Matrix4x4 rotation, Vector3 translation)
    {
        Matrix4x4 m = new Matrix4x4(); // 전부 0에서 시작

        // 회전 부분 (3x3)
        m.m00 = rotation.m00;  m.m01 = rotation.m01;  m.m02 = rotation.m02;
        m.m10 = rotation.m10;  m.m11 = rotation.m11;  m.m12 = rotation.m12;
        m.m20 = rotation.m20;  m.m21 = rotation.m21;  m.m22 = rotation.m22;

        // 이동 부분 (4번째 열)
        m.m03 = translation.x;
        m.m13 = translation.y;
        m.m23 = translation.z;

        // 마지막 행 (0 0 0 1)
        m.m30 = 0f;  m.m31 = 0f;  m.m32 = 0f;  m.m33 = 1f;
        return m;
    }

    /// <summary>Transform 하나의 local 값(localRotation, localPosition)으로 local 4x4 행렬 구성.</summary>
    public static Matrix4x4 LocalMatrix(Transform t)
    {
        return BuildTransform(RotationFromQuaternion(t.localRotation), t.localPosition);
    }

    /// <summary>
    /// Step 4: 강체 변환의 역행렬을 직접 유도해서 계산.
    ///   T = [R t; 0 1]  →  T⁻¹ = [Rᵀ  -Rᵀt; 0 1]
    ///   근거: R은 직교행렬이라 R⁻¹ = Rᵀ.
    ///         p_world = R p + t  →  p = Rᵀ(p_world - t) = Rᵀ p_world - Rᵀ t
    /// </summary>
    public static Matrix4x4 InverseRigid(Matrix4x4 m)
    {
        Matrix4x4 inv = new Matrix4x4();

        // 회전 부분: 전치 (행과 열을 바꿈)
        inv.m00 = m.m00;  inv.m01 = m.m10;  inv.m02 = m.m20;
        inv.m10 = m.m01;  inv.m11 = m.m11;  inv.m12 = m.m21;
        inv.m20 = m.m02;  inv.m21 = m.m12;  inv.m22 = m.m22;

        // 이동 부분: -Rᵀ t
        float tx = m.m03, ty = m.m13, tz = m.m23;
        inv.m03 = -(inv.m00 * tx + inv.m01 * ty + inv.m02 * tz);
        inv.m13 = -(inv.m10 * tx + inv.m11 * ty + inv.m12 * tz);
        inv.m23 = -(inv.m20 * tx + inv.m21 * ty + inv.m22 * tz);

        inv.m30 = 0f;  inv.m31 = 0f;  inv.m32 = 0f;  inv.m33 = 1f;
        return inv;
    }

    /// <summary>동차 좌표 점 변환: (x, y, z, 1)을 곱한 결과의 xyz.</summary>
    public static Vector3 TransformPoint(Matrix4x4 m, Vector3 p)
    {
        Vector4 r = m * new Vector4(p.x, p.y, p.z, 1f);
        return new Vector3(r.x, r.y, r.z);
    }

    /// <summary>Frobenius norm: 모든 원소 차이의 제곱합의 제곱근.</summary>
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
}
