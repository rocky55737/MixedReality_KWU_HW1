using UnityEngine;

/// <summary>
/// 과제 2 Step 1~2: 기본 회전 행렬을 직접 채우고, 두 가지 순서로 합성한다.
///
/// 각도 이름 규칙 (명세 기준)
///   yaw   = Y축 회전
///   pitch = X축 회전
///   roll  = Z축 회전
///
/// 행렬 × 벡터에서는 "오른쪽 행렬이 벡터에 먼저 적용"된다.
///   순서 A: R_A = Rz(roll) * Ry(yaw) * Rx(pitch)  → pitch 먼저, 그다음 yaw, 마지막 roll
///   순서 B: R_B = Rx(pitch) * Ry(yaw) * Rz(roll)  → roll 먼저, 그다음 yaw, 마지막 pitch
/// </summary>
public static class EulerComposer
{
    // ---------------------------------------------------------------
    // Step 1. 기본 회전 행렬 (Matrix4x4의 원소를 직접 대입)
    //   mRC = R행 C열. 회전은 왼쪽 위 3x3만 사용하고 나머지는 단위행렬 그대로.
    // ---------------------------------------------------------------

    /// <summary>
    /// X축 회전
    /// | 1  0   0 |
    /// | 0  c  -s |
    /// | 0  s   c |
    /// </summary>
    public static Matrix4x4 RotationX(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);

        Matrix4x4 m = Matrix4x4.identity;
        m.m11 = c;  m.m12 = -s;
        m.m21 = s;  m.m22 = c;
        return m;
    }

    /// <summary>
    /// Y축 회전
    /// |  c  0  s |
    /// |  0  1  0 |
    /// | -s  0  c |
    /// </summary>
    public static Matrix4x4 RotationY(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);

        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = c;   m.m02 = s;
        m.m20 = -s;  m.m22 = c;
        return m;
    }

    /// <summary>
    /// Z축 회전
    /// | c  -s  0 |
    /// | s   c  0 |
    /// | 0   0  1 |
    /// </summary>
    public static Matrix4x4 RotationZ(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);

        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = c;  m.m01 = -s;
        m.m10 = s;  m.m11 = c;
        return m;
    }

    // ---------------------------------------------------------------
    // Step 2. 두 가지 합성 순서
    // ---------------------------------------------------------------

    /// <summary>순서 A (Z-Y-X): R_A = Rz(roll) * Ry(yaw) * Rx(pitch)</summary>
    public static Matrix4x4 ComposeOrderA(float yaw, float pitch, float roll)
    {
        return RotationZ(roll) * RotationY(yaw) * RotationX(pitch);
    }

    /// <summary>순서 B (X-Y-Z): R_B = Rx(pitch) * Ry(yaw) * Rz(roll)</summary>
    public static Matrix4x4 ComposeOrderB(float yaw, float pitch, float roll)
    {
        return RotationX(pitch) * RotationY(yaw) * RotationZ(roll);
    }

    // ---------------------------------------------------------------
    // 비교용 도구
    // ---------------------------------------------------------------

    /// <summary>
    /// 두 회전 행렬 "전체"가 서로 몇 도 다른지 (forward 한 방향만이 아니라 자세 전체의 차이).
    /// R_diff = Aᵀ·B 도 회전행렬이고, 회전각 θ는  trace(R_diff) = 1 + 2cosθ  로 구할 수 있다.
    /// trace(Aᵀ·B) = Σ A_ij · B_ij  (같은 위치 원소끼리 곱한 합)
    /// </summary>
    public static float RotationAngleBetween(Matrix4x4 a, Matrix4x4 b)
    {
        float trace = 0f;
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                trace += a[row, col] * b[row, col];

        float cos = Mathf.Clamp((trace - 1f) * 0.5f, -1f, 1f);
        return Mathf.Acos(cos) * Mathf.Rad2Deg;
    }

    /// <summary>3x3 회전 부분의 Frobenius norm 차이 (내장 함수와 검증할 때 사용)</summary>
    public static float FrobeniusDiff3x3(Matrix4x4 a, Matrix4x4 b)
    {
        float sum = 0f;
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                float d = a[row, col] - b[row, col];
                sum += d * d;
            }
        return Mathf.Sqrt(sum);
    }
}
