using System.Globalization;

namespace FourTen.Scripts;

/// <summary>
/// 4=10 규칙에 맞는 숫자 표기.
/// - 소수점은 최대 3자리까지만 표시
/// - 딱 나누어 떨어지지 않아 3자리를 넘어가는(무한소수 포함) 값은 "3.333..." 처럼 말줄임표를 붙임
/// </summary>
public static class NumberFormat
{
    public const double Epsilon = 1e-9;

    public static string Format(double value)
    {
        // -0 방지
        if (Math.Abs(value) < Epsilon) value = 0;

        double rounded3 = Math.Round(value, 3, MidpointRounding.AwayFromZero);

        // 3자리 반올림 값과 원래 값이 다르면 소수점 4자리 이상이 존재한다는 뜻 → 말줄임표
        bool truncated = Math.Abs(value - rounded3) > Epsilon;

        string text = rounded3.ToString("0.###", CultureInfo.InvariantCulture);

        if (truncated)
            text += "...";

        return text;
    }

    /// <summary>값이 정확히 10인지(부동소수 오차 허용).</summary>
    public static bool IsTen(double value) => Math.Abs(value - 10.0) < 1e-7;
}
