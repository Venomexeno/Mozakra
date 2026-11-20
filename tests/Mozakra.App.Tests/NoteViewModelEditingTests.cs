using Mozakra.App.ViewModels;
using Mozakra.Core.Models;

namespace Mozakra.App.Tests;

public class NoteViewModelEditingTests
{
    [Fact]
    public void Quick_add_logs_to_the_selected_plan_and_undo_reverts_it()
    {
        var bed = new NoteTestBed(NoteTestBed.SampleState(out _, out var work));
        var vm = bed.CreateViewModel();
        vm.LogPlan = vm.PlanOptions.Single(o => o.Id == work.Id);

        Log(vm, 30);
        var log = Assert.Single(bed.State.State.Logs);
        Assert.Equal((work.Id, 30), (log.PlanId, log.Minutes));
        Assert.True(vm.CanUndo);

        vm.UndoCommand.Execute(null);
        Assert.Empty(bed.State.State.Logs);
        Assert.False(vm.CanUndo);
        Assert.Contains("Note.Toast.Undone", vm.ToastText);
    }

    [Fact]
    public void Undo_keeps_the_current_settings()
    {
        var bed = new NoteTestBed(NoteTestBed.SampleState(out _, out var work));
        var vm = bed.CreateViewModel();
        vm.LogPlan = vm.PlanOptions.Single(o => o.Id == work.Id);
        Log(vm, 15);
        vm.SavePosition(100, 200);

        vm.UndoCommand.Execute(null);

        Assert.Equal(100, bed.State.State.Settings.NoteLeft);
    }

    [Fact]
    public void Subtract_never_goes_below_what_was_logged_that_day()
    {
        var bed = new NoteTestBed(NoteTestBed.SampleState(out _, out var work));
        var vm = bed.CreateViewModel();
        vm.LogPlan = vm.PlanOptions.Single(o => o.Id == work.Id);
        Log(vm, 20);

        vm.LogMinutesText = "45";
        vm.SubtractLogCommand.Execute(null);

        Assert.Equal(0, bed.State.State.Logs.Where(l => l.PlanId == work.Id).Sum(l => l.Minutes));
        vm.LogMinutesText = "10";
        vm.SubtractLogCommand.Execute(null);
        Assert.Equal("Note.Toast.NothingToSubtract", vm.ToastText);
    }

    [Fact]
    public void Manual_log_on_a_past_day_works_and_a_future_day_is_refused()
    {
        var bed = new NoteTestBed(NoteTestBed.SampleState(out _, out var work));
        var vm = bed.CreateViewModel();
        vm.LogPlan = vm.PlanOptions.Single(o => o.Id == work.Id);

        vm.LogDate = NoteTestBed.Today.AddDays(-2).ToDateTime(TimeOnly.MinValue);
        Log(vm, 45);
        Assert.Equal(NoteTestBed.Today.AddDays(-2), Assert.Single(bed.State.State.Logs).Date);

        vm.LogDate = NoteTestBed.Today.AddDays(1).ToDateTime(TimeOnly.MinValue);
        Log(vm, 45);
        Assert.Single(bed.State.State.Logs);
        Assert.Equal("Note.Toast.FutureDay", vm.ToastText);
    }

    [Fact]
    public void Impossible_plan_shows_a_warning_that_can_be_extended_or_left()
    {
        var state = NoteTestBed.SampleState(out var course, out _);
        course.Deadline = NoteTestBed.Today.AddDays(1);      // 2 days × 60 min = 120 min available
        course.TotalVideoMinutes = 600;                        // but it needs 600 min
        var bed = new NoteTestBed(state);
        var vm = bed.CreateViewModel();

        var warning = Assert.Single(vm.Warnings);
        Assert.True(warning.CanExtend);

        vm.ExtendDeadlineCommand.Execute(warning);
        Assert.Equal(warning.SuggestedDeadline, course.Deadline);
        Assert.Empty(vm.Warnings);

        vm.UndoCommand.Execute(null);
        var again = Assert.Single(vm.Warnings);
        vm.LeaveWarningCommand.Execute(again);
        Assert.Empty(vm.Warnings);
        vm.Refresh();
        Assert.Empty(vm.Warnings);                              // dismissed for today

        vm.AddFreeTimeCommand.Execute(null);
        Assert.Contains("OpenFreeTime", bed.Navigation.Calls);
    }

    [Fact]
    public void Edit_day_adds_the_difference_per_plan()
    {
        var bed = new NoteTestBed(NoteTestBed.SampleState(out var course, out var work));
        bed.State.State.Logs.Add(new LogEntry { PlanId = work.Id, Date = NoteTestBed.Today.AddDays(-1), Minutes = 40 });
        var vm = bed.CreateViewModel();
        var yesterday = vm.Week.Single(d => d.Date == NoteTestBed.Today.AddDays(-1));

        bed.Dialogs.EditDayAnswer = rows =>
        {
            rows.Single(r => r.PlanId == work.Id).MinutesText = "25";
            rows.Single(r => r.PlanId == course.Id).MinutesText = "10";
            return rows;
        };
        vm.EditDayCommand.Execute(yesterday);

        int Sum(Guid id) => bed.State.State.Logs.Where(l => l.PlanId == id && l.Date == yesterday.Date).Sum(l => l.Minutes);
        Assert.Equal(25, Sum(work.Id));
        Assert.Equal(10, Sum(course.Id));
    }

    [Fact]
    public void Details_toggle_is_saved_in_settings()
    {
        var bed = new NoteTestBed(NoteTestBed.SampleState(out _, out _));
        var vm = bed.CreateViewModel();
        vm.ToggleDetailsCommand.Execute(null);
        Assert.True(bed.State.State.Settings.ShowDetails);
        Assert.Equal(7, vm.Week.Count);
    }

    [Fact]
    public void Exit_message_depends_on_what_is_left_and_yes_shuts_down()
    {
        var bed = new NoteTestBed(NoteTestBed.SampleState(out _, out var work));
        bool shutDown = false;
        var exit = new NoteExitConfirmation(bed.State, bed.Scheduler, bed.Clock, bed.Dialogs, () => shutDown = true);

        Assert.Equal("Note.Exit.Remaining", exit.BuildMessage());

        bed.State.State.Logs.Add(new LogEntry { PlanId = work.Id, Date = NoteTestBed.Today, Minutes = 60 });
        Assert.Equal("Note.Exit.Done", exit.BuildMessage());

        bed.Dialogs.ConfirmAnswer = false;
        Assert.False(exit.Run());
        Assert.False(shutDown);
        bed.Dialogs.ConfirmAnswer = true;
        Assert.True(exit.Run());
        Assert.True(shutDown);
    }

    [Fact]
    public void Undo_stack_keeps_the_last_30()
    {
        var stack = new NoteUndoStack();
        for (int i = 0; i < 35; i++) stack.Push($"step {i}", new AppState());
        Assert.Equal(30, stack.Count);
        Assert.Equal("step 34", stack.NextLabel);
    }

    private static void Log(NoteViewModel vm, int minutes)
    {
        vm.LogMinutesText = minutes.ToString();
        vm.AddLogCommand.Execute(null);
    }
}
