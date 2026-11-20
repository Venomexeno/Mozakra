using Mozakra.Core.Models;
using Mozakra.Core.Persistence;

namespace Mozakra.Core.Tests.Persistence;

public class StateJsonCompatibilityTests
{
    [Fact]
    public void Deserialize_StateFromV1_0_WithRemovedPracticeFactor_LoadsTheCourse()
    {
        // v1.0.0 saved a "PracticeFactor" on every plan; it was removed in v1.0.1 and must be ignored.
        const string json = """
        {
          "SchemaVersion": 1,
          "Plans": [
            { "Name": "LINQ", "Kind": "Course", "StartDate": "2026-11-20",
              "TotalVideos": 61, "VideosDone": 18, "TotalVideoMinutes": 640, "PracticeFactor": 1.5 }
          ]
        }
        """;

        var state = StateJson.Deserialize(json);

        var plan = Assert.Single(state.Plans);
        Assert.Equal(PlanKind.Course, plan.Kind);
        Assert.Equal(61, plan.TotalVideos);
        Assert.Equal(18, plan.VideosDone);
        Assert.Equal(640, plan.TotalVideoMinutes);
    }
}
