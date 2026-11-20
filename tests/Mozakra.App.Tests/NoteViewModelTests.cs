using Mozakra.App.ViewModels;
using Mozakra.Core.Models;

namespace Mozakra.App.Tests;

public class NoteViewModelTests
{
    [Fact]
    public void Agenda_lists_plans_with_targets_and_suggests_the_most_urgent_for_logging()
    {
        var bed = new NoteTestBed(NoteTestBed.SampleState(out var course, out _));
        var vm = bed.CreateViewModel();

        Assert.False(vm.HasNoPlans);
        Assert.False(vm.IsRestDay);
        Assert.Contains(vm.Agenda, a => a.PlanId == course.Id);
        Assert.Equal(course.Id, vm.LogPlan?.Id);
    }

    [Fact]
    public void Empty_states_no_plans_and_rest_day()
    {
        var bed = new NoteTestBed(new AppState());
        Assert.True(bed.CreateViewModel().HasNoPlans);

        var state = NoteTestBed.SampleState(out _, out _);
        state.Weekly.MinutesByDay[(int)NoteTestBed.Today.DayOfWeek] = 0;
        var vm = new NoteTestBed(state).CreateViewModel();
        Assert.True(vm.IsRestDay);
        Assert.Empty(vm.Agenda);
    }

    [Fact]
    public void A_course_is_logged_in_videos_and_its_time_comes_from_the_average()
    {
        var bed = new NoteTestBed(NoteTestBed.SampleState(out var course, out _));   // 10 min per video
        var vm = bed.CreateViewModel();
        vm.LogPlan = vm.PlanOptions.Single(o => o.Id == course.Id);
        Assert.True(vm.IsLogPlanCourse);
        Assert.False(vm.IsLogPlanWork);

        vm.LogMinutesText = "999";               // ignored for a course
        vm.QuickAddVideosCommand.Execute("2");
        vm.QuickAddVideosCommand.Execute("1");
        Assert.Equal("3", vm.LogVideosText);
        vm.AddLogCommand.Execute(null);

        var log = Assert.Single(bed.State.State.Logs);
        Assert.Equal((course.Id, 30, 3, NoteTestBed.Today), (log.PlanId, log.Minutes, log.VideosDone, log.Date));
        Assert.Equal(3, course.VideosDone);
        Assert.Equal("", vm.LogVideosText);
        Assert.Contains("Note.Toast.Videos", vm.ToastText);
        Assert.DoesNotContain("Note.Toast.Added", vm.ToastText);
        Assert.Equal("Note.VideosProgress", vm.Agenda.Single(a => a.PlanId == course.Id).ProgressText);
    }

    [Fact]
    public void A_work_plan_is_logged_in_minutes()
    {
        var bed = new NoteTestBed(NoteTestBed.SampleState(out _, out var work));
        var vm = bed.CreateViewModel();
        vm.LogPlan = vm.PlanOptions.Single(o => o.Id == work.Id);
        Assert.True(vm.IsLogPlanWork);

        vm.QuickAddCommand.Execute("15");
        vm.QuickAddCommand.Execute("30");
        Assert.Equal("45", vm.LogMinutesText);
        Assert.Empty(bed.State.State.Logs);

        vm.AddLogCommand.Execute(null);
        var log = Assert.Single(bed.State.State.Logs);
        Assert.Equal((work.Id, 45, 0), (log.PlanId, log.Minutes, log.VideosDone));
        Assert.Contains("Note.Toast.Added", vm.ToastText);
    }

    [Fact]
    public void Empty_or_invalid_input_is_not_logged()
    {
        var bed = new NoteTestBed(NoteTestBed.SampleState(out var course, out var work));
        var vm = bed.CreateViewModel();

        vm.LogPlan = vm.PlanOptions.Single(o => o.Id == course.Id);
        vm.AddLogCommand.Execute(null);
        Assert.Equal("Note.Toast.EnterVideos", vm.ToastText);
        vm.LogVideosText = (course.TotalVideos + 1).ToString();
        vm.AddLogCommand.Execute(null);
        Assert.StartsWith("Note.Toast.VideosRange", vm.ToastText);

        vm.LogPlan = vm.PlanOptions.Single(o => o.Id == work.Id);
        vm.LogMinutesText = "abc";
        vm.AddLogCommand.Execute(null);
        Assert.Equal("Note.Toast.EnterMinutes", vm.ToastText);

        Assert.Empty(bed.State.State.Logs);
    }

    [Fact]
    public void Removing_videos_takes_back_their_time_too()
    {
        var bed = new NoteTestBed(NoteTestBed.SampleState(out var course, out _));
        var vm = bed.CreateViewModel();
        vm.LogPlan = vm.PlanOptions.Single(o => o.Id == course.Id);
        vm.LogVideosText = "4";
        vm.AddLogCommand.Execute(null);

        vm.LogVideosText = "9";                  // more than were logged that day
        vm.SubtractLogCommand.Execute(null);

        Assert.Equal(0, course.VideosDone);
        Assert.Equal(0, bed.State.State.Logs.Sum(l => l.Minutes));
        Assert.Equal(0, bed.State.State.Logs.Sum(l => l.VideosDone));
    }

    [Fact]
    public void A_timer_left_by_an_old_version_moves_into_the_minutes_box()
    {
        var state = NoteTestBed.SampleState(out _, out var work);
        state.Timer = new ActiveTimer { PlanId = work.Id, AccumulatedMs = TimeSpan.FromMinutes(78.7).TotalMilliseconds };
        var bed = new NoteTestBed(state);

        var vm = bed.CreateViewModel();

        Assert.Null(bed.State.State.Timer);
        Assert.Equal(work.Id, vm.LogPlan?.Id);
        Assert.Equal("79", vm.LogMinutesText);
        Assert.Empty(bed.State.State.Logs);
        Assert.StartsWith("Note.Toast.OldTimer", vm.ToastText);
    }

    [Fact]
    public void Details_show_only_the_next_waiting_plan_of_a_long_chain()
    {
        var state = NoteTestBed.SampleState(out var course, out _);
        var after = course.Id;
        for (int i = 0; i < 4; i++)
        {
            var next = new StudyPlan { Name = $"Phase {i}", Kind = PlanKind.Work, EstimatedMinutes = 600,
                                       StartDate = NoteTestBed.Today, StartsAfterPlanId = after };
            state.Plans.Add(next);
            after = next.Id;
        }

        var vm = new NoteTestBed(state).CreateViewModel();

        Assert.Equal(2 + 1, vm.Statuses.Count);   // the course, the work plan, and the first waiting phase
        Assert.Contains("Phase 0", vm.Statuses.Select(s => s.Name));
        Assert.DoesNotContain("Phase 1", vm.Statuses.Select(s => s.Name));
        Assert.StartsWith("Note.Status.MoreWaiting", vm.MoreWaitingText);
    }
}
