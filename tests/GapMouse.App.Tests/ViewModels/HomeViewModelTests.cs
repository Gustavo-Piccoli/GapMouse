using GapMouse.App.Localization;
using GapMouse.App.Services;
using GapMouse.App.Tests.Fakes;
using GapMouse.App.ViewModels;
using GapMouse.Application.Settings;
using GapMouse.Domain.Engine;
using GapMouse.Testing.Engine;
using Microsoft.Extensions.Time.Testing;

namespace GapMouse.App.Tests.ViewModels;

public sealed class HomeViewModelTests
{
    private readonly FakeEngineController _engine = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
    private readonly FakePrompt _prompt = new();
    private readonly FakeLifetime _lifetime = new();
    private readonly Localizer _l = new(() => new System.Globalization.CultureInfo("en-US"));

    private HomeViewModel Create() =>
        new(_l, _engine, new ImmediateUiDispatcher(), _time, _prompt, _lifetime);

    private void With(Func<EngineSnapshot, EngineSnapshot> change) => _engine.Snapshot = change(_engine.Snapshot);

    [Fact]
    public void Refresh_Waiting_ShowsIdleOfThreshold()
    {
        With(s => s with { LastIdle = TimeSpan.FromSeconds(45), IdleThreshold = TimeSpan.FromSeconds(120) });
        var vm = Create();
        vm.Refresh();
        vm.IdleText.ShouldBe("Idle for 45 s of 120 s");
        vm.IdleProgress.ShouldBe(0.375);
        vm.StateText.ShouldBe("Waiting");
    }

    [Fact]
    public void Refresh_PausedTimed_ShowsCountdown()
    {
        With(s => s with { State = EngineState.Paused, PauseEndsAt = _time.GetLocalNow() + new TimeSpan(0, 9, 41) });
        var vm = Create();
        vm.Refresh();
        vm.PauseText.ShouldBe("Resumes in 09:41");
        vm.ResumeCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public void Refresh_PausedIndefinite_ShowsUntilResume()
    {
        With(s => s with { State = EngineState.Paused, PauseEndsAt = null });
        var vm = Create();
        vm.Refresh();
        vm.PauseText.ShouldBe("Paused until you resume");
    }

    [Fact]
    public void Refresh_TodayStats_FormatsHours()
    {
        With(s => s with { Today = new DailyStats(new DateOnly(2026, 10, 31), new TimeSpan(1, 2, 3), 4) });
        var vm = Create();
        vm.Refresh();
        vm.TodayActiveTime.ShouldBe("1:02:03");
        vm.TodayActivations.ShouldBe(4);
    }

    [Fact]
    public async Task Pause_TenMinutes_PausesEngine()
    {
        var vm = Create();
        await vm.PauseCommand.ExecuteAsync(PauseOption.TenMinutes);
        _engine.PauseCalls.Single().Length.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Pause_CustomCancelled_DoesNothing()
    {
        _prompt.Result = null;
        var vm = Create();
        await vm.PauseCommand.ExecuteAsync(PauseOption.Custom);
        _engine.PauseCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Pause_Custom45_PausesFortyFiveMinutes()
    {
        _prompt.Result = 45;
        var vm = Create();
        await vm.PauseCommand.ExecuteAsync(PauseOption.Custom);
        _engine.PauseCalls.Single().Length.ShouldBe(TimeSpan.FromMinutes(45));
    }

    [Fact]
    public void Resume_WhenPaused_ResumesEngine()
    {
        With(s => s with { State = EngineState.Paused });
        var vm = Create();
        vm.Refresh();
        vm.ResumeCommand.Execute(null);
        _engine.ResumeCalls.ShouldBe(1);
    }

    [Fact]
    public void Resume_WhenNotPaused_IsDisabled()
    {
        var vm = Create();
        vm.Refresh();
        vm.ResumeCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void Exit_CallsLifetime()
    {
        Create().ExitCommand.Execute(null);
        _lifetime.ExitCalls.ShouldBe(1);
    }

    [Fact]
    public void Timer_EverySecond_Refreshes()
    {
        var vm = Create();
        vm.Start();
        With(s => s with { LastIdle = TimeSpan.FromSeconds(30) });
        _time.Advance(TimeSpan.FromSeconds(1));
        vm.IdleText.ShouldBe("Idle for 30 s of 120 s");
    }

    [Fact]
    public void LanguageChange_RefreshesTexts()
    {
        var vm = Create();
        vm.Start();
        _l.SetLanguage(UiLanguage.Portuguese);
        vm.StateText.ShouldBe("Aguardando");
        vm.IdleText.ShouldStartWith("Inativo há");
    }

    [Fact]
    public void Dispose_StopsRefreshing()
    {
        var vm = Create();
        vm.Start();
        vm.Dispose();
        With(s => s with { LastIdle = TimeSpan.FromSeconds(30) });
        _time.Advance(TimeSpan.FromSeconds(2));
        _engine.Raise(new EngineStarted(_time.GetUtcNow()));
        vm.IdleText.ShouldBe("Idle for 0 s of 120 s");
    }

    private sealed class FakePrompt : IPausePrompt
    {
        public int? Result { get; set; }

        public Task<int?> AskMinutesAsync() => Task.FromResult(Result);
    }

    private sealed class FakeLifetime : IAppLifetime
    {
        public int ExitCalls { get; private set; }

        public void Exit() => ExitCalls++;
    }
}
