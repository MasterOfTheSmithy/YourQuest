using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// note: Only scheduling/lifetime orchestration is source-extracted; physical publication, native timing and full validation owners are stand-ins.
static class Debug
{
    public static readonly List<string> Messages = new List<string>();
    public static void Log(string value) => Messages.Add(value);
    public static void LogWarning(string value) => Messages.Add(value);
    public static void LogException(Exception value, object owner = null) => Messages.Add("exception:" + value.Message);
}
static class Time { public static float realtimeSinceStartup, unscaledTime; }
static class Mathf { public static float Max(float a, float b) => Math.Max(a, b); }
class AsyncOperation { public bool isDone; }
readonly struct ProfilerMarker
{
    public ProfilerMarker(string name) { }
    public Scope Auto() => new Scope();
    public readonly struct Scope : IDisposable { public void Dispose() { } }
}
class YQGeneratedWorldRuntimeBuilder
{
    public class FrontierConstructionAcceptance { }
    public static T MeasureStartupOperation<T>(string name, Func<T> action) => action();
}
class YQProfileSaveSystem
{
    public static YQProfileSaveSystem Instance;
    public bool isActiveAndEnabled = true, PrepareOK = true, PublishOK = true, DeepChanged, Pointer;
    public int Prepares, Publishes;
    public Action OnPrepare;
    public class PreparedContinuationPublication { }
    class ContinuationPublicationData { }
    private PreparedContinuationPublication _preparedContinuationPublication;
    private ContinuationPublicationData _preparedContinuationData;
    public PreparedContinuationPublication Marker => _preparedContinuationPublication;
    public bool HasData => _preparedContinuationData != null;
    public PreparedContinuationPublication MakeNewMarker()
    {
        _preparedContinuationPublication = new PreparedContinuationPublication();
        _preparedContinuationData = new ContinuationPublicationData();
        return Marker;
    }
    public bool TryPrepareContinuationPublication(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        out PreparedContinuationPublication publication, out string failure)
    {
        Prepares++; failure = PrepareOK ? "" : "prepare rejected";
        publication = PrepareOK ? MakeNewMarker() : null;
        Time.realtimeSinceStartup += .004f;
        OnPrepare?.Invoke();
        return PrepareOK;
    }
    public bool TryPublishContinuation(PreparedContinuationPublication publication, out object receipt, out string failure)
    {
        Publishes++; receipt = null;
        bool valid = PublishOK && !DeepChanged && ReferenceEquals(publication, Marker);
        failure = valid ? "" : "final source/provider/disk rejection";
        if (!valid) return false;
        _preparedContinuationPublication = null; _preparedContinuationData = null; Pointer = true;
        return true;
    }
@@RETIRE@@
}
class Streamer
{
    readonly Stack<IEnumerator> _frontierConstructionWork = new Stack<IEnumerator>();
    AsyncOperation _frontierConstructionPendingLoad;
    object _frontierConstructionWorld = new object(), _frontierConstructionPlan = new object(), _frontierConstructionPrevious = new object();
    YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance _frontierConstructionAccepted = new YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance();
    string _frontierConstructionFailure = "";
    bool _frontierPublicationStarted, _frontierPublicationCompleted, _frontierPublicationPublished;
    float _nextFrontierConstructionAt;
    const float FrontierConstructionRetrySeconds = .75f;
    public bool Current = true, RefreshOK = true, ValidateRefreshOK = true, InstallOK = true, enabled = true;
    public int Refreshes, Installs, AtomicCalls;
    public Action OnRefresh;
    class PreparedContinuationRefresh { }
    readonly struct FrontierPublicationTimingScope : IDisposable
    {
        public FrontierPublicationTimingScope(Streamer owner, int phase) { }
        public void Dispose() { }
    }
    bool IsFrontierConstructionWorkCurrent() => Current && _frontierConstructionWorld != null;
    bool TryPrepareContinuationRefresh(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        out PreparedContinuationRefresh refresh, out string failure)
    {
        Refreshes++; refresh = new PreparedContinuationRefresh(); failure = RefreshOK ? "" : "refresh rejected";
        Time.realtimeSinceStartup += .004f;
        OnRefresh?.Invoke(); return RefreshOK;
    }
    bool TryPublishAndInstallPreparedContinuation(YQProfileSaveSystem profile,
        YQProfileSaveSystem.PreparedContinuationPublication publication, PreparedContinuationRefresh refresh, out string failure)
    {
        AtomicCalls++;
        Time.realtimeSinceStartup += .004f;
        failure = "refresh gate rejected";
        if (!ValidateRefreshOK || !profile.TryPublishContinuation(publication, out _, out failure)) return false;
        // note: Model the expected prior-reference change only after the durable pointer; the actual unchanged atomic owner is verified separately by source equality/full compile.
        Current = false;
        Installs++;
        if (!InstallOK) { enabled = false; failure = "published runtime install failed"; return false; }
        failure = ""; return true;
    }
@@ROUTINE@@
    public void Advance()
    {
        float started = Time.realtimeSinceStartup;
@@SCHEDULER@@
    public void Cancel() => CancelFrontierConstructionWork();
    void RecordAggregateWorkSlice(float start, string name) { }
    public int Pending => _frontierConstructionWork.Count;
    public bool FlagsClear => !_frontierPublicationStarted && !_frontierPublicationCompleted && !_frontierPublicationPublished;
    public float RetryAt => _nextFrontierConstructionAt;
@@CANCEL@@
@@DISPOSE@@
}
static class Program
{
    static int checks;
    static void Check(bool ok, string name) { checks++; if(!ok)throw new Exception(name); Console.WriteLine("PASS " + name); }
    static Streamer Fresh(out YQProfileSaveSystem profile)
    {
        Debug.Messages.Clear(); Time.realtimeSinceStartup = Time.unscaledTime = 0;
        profile = new YQProfileSaveSystem(); YQProfileSaveSystem.Instance = profile;
        return new Streamer();
    }
    static void ToFirstGap(Streamer streamer) { streamer.Advance(); streamer.Advance(); }
    static void ToSecondGap(Streamer streamer) { ToFirstGap(streamer); streamer.Advance(); }
    static void Main()
    {
        var s = Fresh(out var p);
        s.Advance(); Check(s.Refreshes==0 && s.Pending==1,"publication dispatch defers to existing stack exactly once");
        s.Advance(); Check(s.Refreshes==1 && p.Prepares==0 && s.Pending==1 && !p.Pointer,"first yield after private refresh");
        s.Advance(); Check(p.Prepares==1 && p.Publishes==0 && p.Marker!=null && s.Pending==1 && !p.Pointer,"second yield after private profile prepare");
        s.Advance(); Check(s.AtomicCalls==1 && p.Publishes==1 && s.Installs==1 && p.Pointer,"publish and install complete in one resumed step");
        Check(s.Pending==0 && s.FlagsClear && p.Marker==null && !p.HasData,"completed request and exact consumed marker retired");
        Check(Debug.Messages.Count(x=>x.Contains("Accepted frontier location published"))==1,"success retained despite expected prior-owner change");

        s=Fresh(out p);ToFirstGap(s);s.Cancel();Check(p.Prepares==0 && s.Pending==0 && s.FlagsClear && !p.Pointer,"cancel first gap does not capture or publish");
        s=Fresh(out p);ToSecondGap(s);s.Cancel();Check(p.Marker==null && !p.HasData && p.Publishes==0 && s.Pending==0,"cancel second gap retires own private profile token");
        s=Fresh(out p);ToSecondGap(s);var newer=p.MakeNewMarker();s.Cancel();Check(ReferenceEquals(newer,p.Marker)&&p.HasData,"old cancellation preserves newer prepared token");
        var foreign=new YQProfileSaveSystem.PreparedContinuationPublication();p.RetirePreparedContinuationPublication(foreign);Check(ReferenceEquals(newer,p.Marker),"foreign token cannot retire current data");
        p.RetirePreparedContinuationPublication(null);Check(ReferenceEquals(newer,p.Marker),"null token cannot retire current data");
        p.RetirePreparedContinuationPublication(newer);p.RetirePreparedContinuationPublication(newer);Check(p.Marker==null&&!p.HasData,"matching token cleanup is idempotent");

        s=Fresh(out p);ToFirstGap(s);s.Current=false;s.Advance();Check(p.Prepares==0 && !p.Pointer && s.Pending==0,"stale source owner after refresh rejects");
        s=Fresh(out p);ToSecondGap(s);s.Current=false;s.Advance();Check(p.Publishes==0 && p.Marker==null && s.Pending==0,"stale source owner after profile prepare rejects and cleans");
        s=Fresh(out p);ToFirstGap(s);YQProfileSaveSystem.Instance=new YQProfileSaveSystem();s.Advance();Check(p.Prepares==0&&!p.Pointer&&s.Pending==0,"profile singleton replacement after refresh rejects");
        s=Fresh(out p);ToSecondGap(s);YQProfileSaveSystem.Instance=new YQProfileSaveSystem();s.Advance();Check(p.Publishes==0&&p.Marker==null&&s.Pending==0,"profile singleton replacement after capture rejects and cleans");
        s=Fresh(out p);ToSecondGap(s);p.isActiveAndEnabled=false;s.Advance();Check(p.Publishes==0&&p.Marker==null&&!p.Pointer,"disabled profile after capture rejects");

        s=Fresh(out p);ToSecondGap(s);p.DeepChanged=true;s.Advance();Check(s.AtomicCalls==1&&p.Publishes==1&&!p.Pointer&&s.Installs==0,"deep changed source still reaches unchanged final rejection gate");
        Check(s.RetryAt==.75f && s.Pending==0,"source rejection retains existing cooldown without freezing stack");
        s=Fresh(out p);ToSecondGap(s);s.ValidateRefreshOK=false;s.Advance();Check(p.Publishes==0&&!p.Pointer&&s.Installs==0,"final refresh rejection skips profile publication");
        s=Fresh(out p);s.RefreshOK=false;s.Advance();s.Advance();Check(s.Refreshes==1&&p.Prepares==0&&s.Pending==0,"refresh failure skips later phases");
        s=Fresh(out p);p.PrepareOK=false;ToFirstGap(s);s.Advance();Check(p.Prepares==1&&p.Publishes==0&&!p.Pointer&&s.Pending==0,"profile preparation failure skips commit");
        s=Fresh(out p);s.OnRefresh=()=>throw new Exception("refresh throw");s.Advance();s.Advance();Check(s.Pending==0&&s.FlagsClear&&!p.Pointer,"thrown refresh retires owned iterator");
        s=Fresh(out p);p.OnPrepare=()=>throw new Exception("prepare throw");ToFirstGap(s);s.Advance();Check(s.Pending==0&&s.FlagsClear&&!p.Pointer,"thrown profile prepare retires owned iterator");
        s=Fresh(out p);p.OnPrepare=()=>s.Current=false;ToFirstGap(s);s.Advance();Check(s.Pending==0&&p.Marker==null&&!p.Pointer,"owner changes inside prepare callback reject before second yield");
        s=Fresh(out p);s.InstallOK=false;ToSecondGap(s);s.Advance();Check(p.Pointer&&!s.enabled&&s.Installs==1&&s.Pending==0,"post-pointer install failure retains durable pointer and stops owner");
        Check(!Debug.Messages.Any(x=>x.Contains("Accepted frontier location published")),"failed install does not report runtime success");
        Console.WriteLine("CHECKS " + checks + " FAILURES 0");
    }
}
