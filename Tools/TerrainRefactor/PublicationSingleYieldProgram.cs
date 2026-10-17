// note: These controls use the actual compiled production PlayerState, production value converters and extracted exact JSON comparison helper.
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
    static void ToGap(Streamer s) { s.Advance(); s.Advance(); }
    static void MoveFrame(Streamer s, YQProfileSaveSystem p, int frame)
    {
        p.Player.lastPosition = new UnityEngine.Vector3(frame * .4f, 0, frame * .2f);
        p.Player.logicalPosition = p.Player.lastPosition;
        s.Advance();
    }
    static void Main()
    {
#if OLD_TWO_YIELDS
        var old = Fresh(out var oldProfile);
        for(int frame=1;frame<=4;frame++)MoveFrame(old,oldProfile,frame);
        Check(!oldProfile.Pointer && oldProfile.Prepares==1 && oldProfile.Publishes==1,"two-yield experiment rejects ordinary changed production player position after capture");
        Check(old.Installs==0 && old.Pending==0 && old.RetryAt==.75f,"two-yield moving-player rejection cannot install and enters cooldown");
        Check(oldProfile.Player.stateRevision==0,"negative reproduces source drift without claimed revision change");
#else
        var s = Fresh(out var p);
        s.Advance();Check(s.Refreshes==0&&s.Pending==1,"dispatch once into existing owned stack");
        s.Advance();Check(s.Refreshes==1&&p.Prepares==0&&p.Marker==null&&!p.Pointer&&s.Pending==1,"one yield after refresh holds no captured player or profile marker");
        s.Advance();Check(p.Prepares==1&&p.Publishes==1&&s.AtomicCalls==1&&s.Installs==1&&p.Pointer,"profile capture publication and install execute in same resumed step");
        Check(s.Pending==0&&s.FlagsClear,"success retires stack and outcome flags");
        Check(Debug.Messages.Count(x=>x.Contains("Accepted frontier location published"))==1,"expected prior-owner transition reports success once");

        s=Fresh(out p);for(int frame=1;frame<=3;frame++)MoveFrame(s,p,frame);
        Check(p.Pointer&&s.Installs==1&&p.Prepares==1&&p.Publishes==1,"one-yield candidate recaptures moving production player and accepts exact current source");
        Check(p.Player.stateRevision==0,"moving-player success does not depend on revision update");
        Check(p.CapturedPlayerSourceMatches,"published player source equals last actual position using production JSON contracts");

        s=Fresh(out p);ToGap(s);s.Cancel();Check(p.Prepares==0&&p.Marker==null&&!p.Pointer&&s.Pending==0&&s.FlagsClear,"gap cancellation leaves no profile marker or pointer");
        s=Fresh(out p);ToGap(s);s.Current=false;s.Advance();Check(p.Prepares==0&&!p.Pointer&&s.Pending==0,"stale streamer/world/epoch owner before capture rejects");
        s=Fresh(out p);ToGap(s);YQProfileSaveSystem.Instance=new YQProfileSaveSystem();s.Advance();Check(p.Prepares==0&&!p.Pointer&&s.Pending==0,"profile singleton replacement at gap rejects");
        s=Fresh(out p);ToGap(s);p.isActiveAndEnabled=false;s.Advance();Check(p.Prepares==0&&!p.Pointer,"disabled profile before capture rejects");
        s=Fresh(out p);ToGap(s);p.DeepChanged=true;s.Advance();Check(p.Publishes==1&&!p.Pointer&&s.Installs==0,"unchanged final deep source rejection gate remains authoritative");
        Check(s.RetryAt==.75f&&s.Pending==0,"rejection retains cooldown without freezing other stack work");
        s=Fresh(out p);ToGap(s);s.ValidateRefreshOK=false;s.Advance();Check(p.Publishes==0&&!p.Pointer&&s.Installs==0,"final physical refresh gate skips profile publish");
        s=Fresh(out p);s.RefreshOK=false;s.Advance();s.Advance();Check(p.Prepares==0&&s.Pending==0,"refresh failure excludes later phases");
        s=Fresh(out p);ToGap(s);p.PrepareOK=false;s.Advance();Check(p.Prepares==1&&p.Publishes==0&&!p.Pointer&&s.Pending==0,"profile preparation failure excludes final commit");
        s=Fresh(out p);s.OnRefresh=()=>throw new Exception("refresh throw");s.Advance();s.Advance();Check(s.Pending==0&&s.FlagsClear&&!p.Pointer,"refresh exception retires owned work");
        s=Fresh(out p);ToGap(s);p.OnPrepare=()=>throw new Exception("prepare throw");s.Advance();Check(s.Pending==0&&s.FlagsClear&&!p.Pointer,"prepare exception retires owned work");
        s=Fresh(out p);ToGap(s);p.OnPrepare=()=>s.Current=false;s.Advance();Check(p.Publishes==0&&!p.Pointer&&s.Pending==0,"callback owner invalidation rejects before atomic publish");
        s=Fresh(out p);ToGap(s);p.OnPrepare=()=>p.Player.lastPosition=new UnityEngine.Vector3(9,8,7);s.Advance();Check(!p.Pointer&&s.Installs==0,"source-mutating callback still fails exact captured player comparison");
        s=Fresh(out p);ToGap(s);s.InstallOK=false;s.Advance();Check(p.Pointer&&!s.enabled&&s.Installs==1&&s.Pending==0,"post-pointer install failure retains durable result and stops owner");
        Check(!Debug.Messages.Any(x=>x.Contains("Accepted frontier location published")),"failed install never reports runtime success");
#endif
        Console.WriteLine("CHECKS " + checks + " FAILURES 0");
    }
}
