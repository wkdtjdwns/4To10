using FourTen.Scripts;

namespace FourTen.Models;

public sealed class Stage
{
    public int Index { get; }          // 1-based 스테이지 번호
    public int[] Numbers { get; }      // 화면에 표시되는 4개의 숫자(초기 순서)

    private Solution? _solution;

    public Stage(int index, int[] numbers)
    {
        Index = index;
        Numbers = numbers;
    }

    /// <summary>정답 수식(힌트용). 최초 호출 시 솔버로 계산 후 캐시.</summary>
    public Solution Solution => _solution ??= Solver.Solve(Numbers)
        ?? throw new InvalidOperationException($"스테이지 {Index} 에 해가 없습니다: {string.Join(",", Numbers)}");
}
