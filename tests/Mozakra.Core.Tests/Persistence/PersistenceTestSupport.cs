using Mozakra.Core.Models;

namespace Mozakra.Core.Tests.Persistence;

internal sealed class TestClock : IClock
{
    public TestClock(DateOnly today) => Today = today;

    public DateOnly Today { get; set; }
    public DateTime UtcNow => Today.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc);
}

/// <summary>A unique temp folder deleted on dispose.</summary>
internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mozakra-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}
