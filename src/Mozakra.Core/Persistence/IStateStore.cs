using Mozakra.Core.Models;

namespace Mozakra.Core.Persistence;

public interface IStateStore
{
    string DataDirectory { get; }
    AppState Load();          // returns a new AppState when there is no file; recovers from the latest backup when corrupt
    void Save(AppState state); // atomic write (temp file + replace) + daily backup (keep 7)
}
