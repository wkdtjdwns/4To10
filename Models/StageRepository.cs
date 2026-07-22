namespace FourTen.Models;

/// <summary>25개의 스테이지 정의. 모든 세트는 결과가 10이 되는 해를 최소 1개 가진다.</summary>
public static class StageRepository
{
    public static readonly IReadOnlyList<Stage> Stages = BuildStages();

    private static List<Stage> BuildStages()
    {
        int[][] sets =
        {
            new[] { 1, 2, 3, 4 }, //  1  1+2+3+4
            new[] { 2, 3, 4, 5 }, //  2  5+4+3-2
            new[] { 4, 4, 3, 3 }, //  3  4×4-3-3
            new[] { 2, 2, 3, 3 }, //  4  2+2+3+3
            new[] { 5, 5, 5, 5 }, //  5  5+5+5-5
            new[] { 2, 4, 6, 8 }, //  6  8×6÷4-2
            new[] { 3, 3, 3, 3 }, //  7  3×3+3÷3
            new[] { 1, 3, 5, 7 }, //  8  7+5-3+1
            new[] { 2, 3, 5, 7 }, //  9  3×5-7+2
            new[] { 1, 2, 4, 8 }, // 10  8+4÷2×1
            new[] { 2, 2, 5, 5 }, // 11  5+5+2-2
            new[] { 1, 4, 5, 6 }, // 12  5×6÷(4-1)
            new[] { 2, 2, 4, 4 }, // 13  2×4+4-2
            new[] { 1, 2, 2, 5 }, // 14  (5-1)×2+2
            new[] { 1, 2, 3, 6 }, // 15  6÷2×3+1
            new[] { 2, 3, 4, 6 }, // 16  6×3-4×2
            new[] { 2, 4, 4, 6 }, // 17  4×2+6-4
            new[] { 1, 2, 3, 5 }, // 18  2×3+5-1
            new[] { 3, 4, 5, 8 }, // 19  8+4-5+3
            new[] { 2, 5, 6, 7 }, // 20  7+6-5+2
            new[] { 1, 4, 6, 9 }, // 21  9+6-4-1
            new[] { 2, 3, 6, 9 }, // 22  6+9-3-2
            new[] { 1, 5, 6, 8 }, // 23  8+6-5+1
            new[] { 2, 4, 7, 9 }, // 24  9+7-4-2
            new[] { 3, 5, 7, 9 }, // 25  9+3-7+5
        };

        var list = new List<Stage>();
        for (int i = 0; i < sets.Length; i++)
            list.Add(new Stage(i + 1, sets[i]));
        return list;
    }
}
