using System.Globalization;

namespace FourTen.Scripts;

/// <summary>
/// +, -, ×(*), ÷(/) 와 괄호 한 쌍을 지원하는 간단한 수식 평가기.
/// 토큰: 숫자(double), 연산자 char('+','-','*','/'), 괄호 '(' ')'.
/// 나눗셈은 실수로 계산되어 5/2 = 2.5 처럼 소수로 표현된다.
/// </summary>
public static class ExpressionEvaluator
{
    /// <summary>
    /// 토큰 목록을 계산한다. 성공 시 value 반환, 실패(문법 오류/0으로 나눔 등) 시 null.
    /// </summary>
    public static double? Evaluate(IReadOnlyList<Token> tokens)
    {
        try
        {
            var output = new List<object>();   // 후위표기(숫자 double / 연산자 char)
            var ops = new Stack<char>();

            foreach (var t in tokens)
            {
                switch (t.Kind)
                {
                    case TokenKind.Number:
                        output.Add(t.Number);
                        break;
                    case TokenKind.Operator:
                        while (ops.Count > 0 && ops.Peek() != '('
                               && Precedence(ops.Peek()) >= Precedence(t.Op))
                            output.Add(ops.Pop());
                        ops.Push(t.Op);
                        break;
                    case TokenKind.OpenParen:
                        ops.Push('(');
                        break;
                    case TokenKind.CloseParen:
                        while (ops.Count > 0 && ops.Peek() != '(')
                            output.Add(ops.Pop());
                        if (ops.Count == 0) return null; // 괄호 불일치
                        ops.Pop(); // '(' 제거
                        break;
                }
            }
            while (ops.Count > 0)
            {
                char op = ops.Pop();
                if (op == '(') return null; // 괄호 불일치
                output.Add(op);
            }

            // 후위표기 계산
            var eval = new Stack<double>();
            foreach (var item in output)
            {
                if (item is double d)
                {
                    eval.Push(d);
                }
                else
                {
                    char op = (char)item;
                    if (eval.Count < 2) return null;
                    double b = eval.Pop();
                    double a = eval.Pop();
                    switch (op)
                    {
                        case '+': eval.Push(a + b); break;
                        case '-': eval.Push(a - b); break;
                        case '*': eval.Push(a * b); break;
                        case '/':
                            if (Math.Abs(b) < NumberFormat.Epsilon) return null; // 0으로 나눔
                            eval.Push(a / b);
                            break;
                    }
                }
            }
            if (eval.Count != 1) return null;
            return eval.Pop();
        }
        catch
        {
            return null;
        }
    }

    private static int Precedence(char op) => op is '*' or '/' ? 2 : 1;
}

public enum TokenKind { Number, Operator, OpenParen, CloseParen }

public readonly struct Token
{
    public TokenKind Kind { get; }
    public double Number { get; }
    public char Op { get; }

    private Token(TokenKind kind, double number, char op)
    {
        Kind = kind; Number = number; Op = op;
    }

    public static Token Num(double n) => new(TokenKind.Number, n, '\0');
    public static Token Operator(char op) => new(TokenKind.Operator, 0, op);
    public static Token Open() => new(TokenKind.OpenParen, 0, '(');
    public static Token Close() => new(TokenKind.CloseParen, 0, ')');
}
