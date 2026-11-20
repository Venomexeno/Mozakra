using Mozakra.Core.Models;

namespace Mozakra.Core.Tests;

public class ModelsTests
{
    [Fact]
    public void DefaultAppState_HasEmptyListsAndSchemaVersion1()
    {
        var state = new AppState();

        Assert.Equal(1, state.SchemaVersion);
        Assert.Empty(state.Plans);
        Assert.Empty(state.Exceptions);
        Assert.Empty(state.Logs);
        Assert.Null(state.Timer);
        Assert.Equal(7, state.Weekly.MinutesByDay.Length);
    }
}
