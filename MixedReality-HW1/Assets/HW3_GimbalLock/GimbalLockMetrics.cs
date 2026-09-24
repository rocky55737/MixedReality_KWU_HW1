using UnityEngine;

/// <summary>
/// 과제 3 Step 2: 자유도 손실을 수치로 재는 함수들 (순수 계산, 씬과 무관).
///
/// Unity의 Quaternion.Euler(pitch, yaw, roll) 은 Z → X → Y 순서(= Ry·Rx·Rz)로 적용된다.
/// 이를 "물체 입장"에서 보면:
///   - yaw  : 항상 월드 Y축(세상의 위쪽)을 중심으로 회전
///   - roll : 물체 자신의 forward 축을 중심으로 회전
/// pitch = ±90° 이면 물체의 forward가 월드 Y축과 평행해지므로
/// yaw 축과 roll 축이 같은 직선이 된다 → 두 슬라이더가 같은 일을 함 = 자유도 1개 손실.
///
/// 세 가지 지표를 제공한다.
///   1) ForwardCosine : 명세 그대로 (forward 변화 벡터끼리). ※ 아래 주석 참고
///   2) BasisCosine   : forward 대신 물체의 세 축(right, up, forward) 변화를 모두 사용
///   3) AxisCosine    : yaw 변화와 roll 변화가 "어느 축을 중심으로 돌리는가"를 직접 비교
/// </summary>
public static class GimbalLockMetrics
{
    /// <summary>코사인 유사도. 한쪽 벡터 길이가 사실상 0이면 정의되지 않으므로 NaN 반환.</summary>
    public static float CosineSimilarity(Vector3 a, Vector3 b, float epsilon = 1e-6f)
    {
        float ma = a.magnitude, mb = b.magnitude;
        if (ma < epsilon || mb < epsilon) return float.NaN;
        return Vector3.Dot(a, b) / (ma * mb);
    }

    /// <summary>
    /// 지표 1 (명세 그대로): Δf_yaw 와 Δf_roll 의 코사인 유사도.
    ///
    /// 주의: roll은 물체 자신의 forward 축을 중심으로 돌리므로 forward 벡터는 전혀 변하지 않는다.
    ///       → Δf_roll = 0 → 코사인 유사도 = 0/0 (정의 불가, NaN).
    ///       NaN 처리를 안 하면 부동소수점 잡음(1e-8 수준)끼리 나눈 무의미한 값이 나온다.
    /// </summary>
    public static float ForwardCosine(float pitch, float yaw, float roll, float delta,
                                      out float yawChangeLength, out float rollChangeLength)
    {
        Vector3 f0 = Quaternion.Euler(pitch, yaw, roll) * Vector3.forward;
        Vector3 fYaw = Quaternion.Euler(pitch, yaw + delta, roll) * Vector3.forward;
        Vector3 fRoll = Quaternion.Euler(pitch, yaw, roll + delta) * Vector3.forward;

        Vector3 dYaw = fYaw - f0;
        Vector3 dRoll = fRoll - f0;
        yawChangeLength = dYaw.magnitude;
        rollChangeLength = dRoll.magnitude;
        return CosineSimilarity(dYaw, dRoll);
    }

    /// <summary>
    /// 지표 2: 명세의 아이디어를 물체의 세 축 전부로 확장.
    /// 세 축의 변화 벡터를 이어 붙인 9차원 벡터 두 개의 코사인 유사도.
    ///   (= 회전행렬 변화량 ΔR_yaw, ΔR_roll 을 펼쳐서 비교하는 것과 같음)
    /// Δ가 작을수록 지표 3(AxisCosine)에 가까워진다 → 수치 미분의 스텝 크기 영향 확인용.
    /// </summary>
    public static float BasisCosine(float pitch, float yaw, float roll, float delta)
    {
        Quaternion q0 = Quaternion.Euler(pitch, yaw, roll);
        Quaternion qYaw = Quaternion.Euler(pitch, yaw + delta, roll);
        Quaternion qRoll = Quaternion.Euler(pitch, yaw, roll + delta);

        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        float dot = 0f, sqrYaw = 0f, sqrRoll = 0f;
        foreach (Vector3 axis in axes)
        {
            Vector3 dYaw = qYaw * axis - q0 * axis;
            Vector3 dRoll = qRoll * axis - q0 * axis;
            dot += Vector3.Dot(dYaw, dRoll);
            sqrYaw += dYaw.sqrMagnitude;
            sqrRoll += dRoll.sqrMagnitude;
        }
        if (sqrYaw < 1e-12f || sqrRoll < 1e-12f) return float.NaN;
        return dot / Mathf.Sqrt(sqrYaw * sqrRoll);
    }

    /// <summary>
    /// 지표 3: yaw를 Δ 바꿨을 때와 roll을 Δ 바꿨을 때, 각각 월드 공간에서 어느 축을 중심으로
    /// 회전한 것인지 구해 두 축의 코사인을 비교. 이론값은 -sin(pitch).
    ///   변화 회전 dq = q_new · q0⁻¹  (q0 자세에서 q_new 자세로 가는 월드 기준 회전)
    /// </summary>
    public static float AxisCosine(float pitch, float yaw, float roll, float delta)
    {
        Quaternion q0 = Quaternion.Euler(pitch, yaw, roll);
        Vector3 yawAxis = ChangeAxis(q0, Quaternion.Euler(pitch, yaw + delta, roll));
        Vector3 rollAxis = ChangeAxis(q0, Quaternion.Euler(pitch, yaw, roll + delta));
        return CosineSimilarity(yawAxis, rollAxis);
    }

    /// <summary>q0 → q1 로 가는 회전의 회전축 (회전각을 0~180°로 맞춘 방향)</summary>
    public static Vector3 ChangeAxis(Quaternion q0, Quaternion q1)
    {
        Quaternion dq = q1 * Quaternion.Inverse(q0);
        dq.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) axis = -axis; // 360-θ 로 표현된 경우 같은 회전을 θ 방향으로 뒤집기
        return axis;
    }
}
