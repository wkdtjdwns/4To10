using System.IO;
using System.Text.Json;

namespace FourTen.Models;

/// <summary>클리어한 스테이지를 %AppData%\4=10\progress.json 에 저장/로드.</summary>
public sealed class ProgressStore
{
    private readonly string _path;
    private HashSet<int> _cleared = new();

    public ProgressStore()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "4=10");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "progress.json");
        Load();
    }

    public IReadOnlySet<int> Cleared => _cleared;

    public bool IsCleared(int stageIndex) => _cleared.Contains(stageIndex);

    public void MarkCleared(int stageIndex)
    {
        if (_cleared.Add(stageIndex))
            Save();
    }

    /// <summary>클리어하지 않은 첫 스테이지 번호(모두 클리어했으면 1).</summary>
    public int FirstUnclearedStage()
    {
        for (int i = 1; i <= StageRepository.Stages.Count; i++)
            if (!_cleared.Contains(i)) return i;
        return 1;
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var data = JsonSerializer.Deserialize<int[]>(File.ReadAllText(_path));
                if (data != null) _cleared = new HashSet<int>(data);
            }
        }
        catch
        {
            _cleared = new HashSet<int>();
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_cleared.OrderBy(x => x).ToArray()));
        }
        catch
        {
            // 저장 실패는 무시(권한 등)
        }
    }
}
