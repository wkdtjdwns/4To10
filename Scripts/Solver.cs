namespace FourTen.Scripts;

/// <summary>정답 수식 한 가지(힌트/검증에 사용).</summary>
public sealed class Solution
{
    public required int[] NumberOrder { get; init; }   // 정답 순서로 배열된 4개의 숫자
    public required char[] Operators { get; init; }    // 3개의 연산자 '+','-','*','/'
    public int OpenBefore { get; init; } = -1;         // 이 인덱스의 숫자 앞에 '(' (없으면 -1)
    public int CloseAfter { get; init; } = -1;         // 이 인덱스의 숫자 뒤에 ')' (없으면 -1)

    /// <summary>순서 힌트: 숫자의 순서 문자열 예) "6  4  6  6"</summary>
    public string NumberHint() => string.Join("   ", NumberOrder);

    /// <summary>연산자 힌트: 괄호 포함 기호의 순서 예) "(  +  )  ÷"</summary>
    public string OperatorHint()
    {
        var parts = new List<string>();
        for (int i = 0; i < NumberOrder.Length; i++)
        {
            if (OpenBefore == i) parts.Add("(");
            if (CloseAfter == i) parts.Add(")");
            if (i < Operators.Length) parts.Add(Symbol(Operators[i]));
        }
        return string.Join("   ", parts);
    }

    private static string Symbol(char op) => op switch
    {
        '+' => "+",
        '-' => "−",
        '*' => "×",
        '/' => "÷",
        _ => op.ToString()
    };
}

public static class Solver
{
    private static readonly char[] AllOps = { '+', '-', '*', '/' };

    // 괄호 옵션: 없음(-1,-1) 우선, 이후 (open,close) 조합
    private static readonly (int open, int close)[] ParenOptions =
    {
        (-1, -1),
        (0, 1), (0, 2), (0, 3),
        (1, 2), (1, 3),
        (2, 3),
    };

    /// <summary>4개의 숫자로 결과가 10이 되는 첫 번째 정답을 찾는다. 없으면 null.</summary>
    public static Solution? Solve(int[] numbers)
    {
        foreach (var (open, close) in ParenOptions)
        {
            foreach (var perm in Permutations(numbers))
            {
                foreach (var ops in OperatorTriples())
                {
                    var tokens = BuildTokens(perm, ops, open, close);
                    var result = ExpressionEvaluator.Evaluate(tokens);
                    if (result.HasValue && NumberFormat.IsTen(result.Value))
                    {
                        return new Solution
                        {
                            NumberOrder = perm,
                            Operators = ops,
                            OpenBefore = open,
                            CloseAfter = close,
                        };
                    }
                }
            }
        }
        return null;
    }

    /// <summary>플레이어가 만든 상태(숫자 순서·연산자·괄호)를 토큰으로 만든다.</summary>
    public static List<Token> BuildTokens(int[] numbers, char?[] ops, int openBefore, int closeAfter)
    {
        var tokens = new List<Token>();
        for (int i = 0; i < numbers.Length; i++)
        {
            if (openBefore == i) tokens.Add(Token.Open());
            tokens.Add(Token.Num(numbers[i]));
            if (closeAfter == i) tokens.Add(Token.Close());
            if (i < ops.Length && ops[i].HasValue) tokens.Add(Token.Operator(ops[i]!.Value));
        }
        return tokens;
    }

    private static List<Token> BuildTokens(int[] numbers, char[] ops, int openBefore, int closeAfter)
    {
        var nullable = new char?[ops.Length];
        for (int i = 0; i < ops.Length; i++) nullable[i] = ops[i];
        return BuildTokens(numbers, nullable, openBefore, closeAfter);
    }

    private static IEnumerable<char[]> OperatorTriples()
    {
        foreach (var a in AllOps)
            foreach (var b in AllOps)
                foreach (var c in AllOps)
                    yield return new[] { a, b, c };
    }

    private static IEnumerable<int[]> Permutations(int[] source)
    {
        return Permute(source, 0);

        static IEnumerable<int[]> Permute(int[] arr, int start)
        {
            if (start >= arr.Length)
            {
                yield return (int[])arr.Clone();
                yield break;
            }
            for (int i = start; i < arr.Length; i++)
            {
                (arr[start], arr[i]) = (arr[i], arr[start]);
                foreach (var p in Permute(arr, start + 1))
                    yield return p;
                (arr[start], arr[i]) = (arr[i], arr[start]);
            }
        }
    }
}
