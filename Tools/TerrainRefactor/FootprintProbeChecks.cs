using System;
using UnityEngine;
// note: Native physics is explicitly substituted; these controls isolate existing support arithmetic and an unaccepted centre-reference proposal.
public static class FootprintProbeChecks
{
    private static int checks,failed;
    private static void Check(string name,bool ok){checks++;if(!ok)failed++;Console.WriteLine((ok?"PASS ":"FAIL ")+name);}
    public static int Main()
    {
        foreach(float angle in new[]{0f,35f,50f,57f,58f})foreach(int mirror in new[]{-1,1}) {
            float grade=Mathf.Tan(angle*Mathf.Deg2Rad)*mirror;
            var scene=new TestScene{height=(x,z)=>grade*x,normal=new Vector3(-grade,1,0).normalized};
            var original=new OriginalProbe(scene).Measure(Vector3.zero,false,Vector3.zero);
            var proposed=new CandidateProbe(scene).Measure(Vector3.zero,false,Vector3.zero);
            var current=new CurrentProbe(scene).Measure(Vector3.zero,false,Vector3.zero);
            int reference=VerifiedSource.CurrentIsApplied?(int)proposed.issues:(int)original.issues;
            Vector3 referenceFeet=VerifiedSource.CurrentIsApplied?proposed.feet:original.feet;
            Check("exact current production matches recognized reference angle="+angle+" mirror="+mirror,(int)current.issues==reference&&current.feet==referenceFeet);
            if(angle==50)Check("original50degree smooth plane false-positive captured mirror="+mirror,((int)original.issues&64)!=0&&original.hasSupport&&((int)original.issues&2)==0);
            Check("proposed smooth allowed plane accepted angle="+angle+" mirror="+mirror,(int)proposed.issues==0);
            Console.WriteLine("PLANE angle="+angle+" mirror="+mirror+" original="+original.issues+" candidate="+proposed.issues+" originalFeet="+original.feet+" found="+original.footprintHasSupport+" delta="+original.footprintHeightDelta+" slope="+original.footprintSlopeDegrees);
        }
        var gap=new CandidateProbe(new TestScene{height=(x,z)=>x<-.2f?(float?)null:0}).Measure(Vector3.zero,false,default);
        Check("true unsupported footprint remains rejected",((int)gap.issues&64)!=0);
        var narrow=new CandidateProbe(new TestScene{height=(x,z)=>Mathf.Abs(x)>.15f?(float?)null:0}).Measure(Vector3.zero,false,default);
        Check("narrow beam with centre support remains rejected",narrow.hasSupport&&((int)narrow.issues&64)!=0);
        float diagonal=Mathf.Tan(50*Mathf.Deg2Rad)/Mathf.Sqrt(2);
        var diagonalScene=new TestScene{height=(x,z)=>diagonal*(x+z),normal=new Vector3(-diagonal,1,-diagonal).normalized};
        var diagonalOld=new OriginalProbe(diagonalScene).Measure(Vector3.zero,false,default);
        var diagonalNew=new CandidateProbe(diagonalScene).Measure(Vector3.zero,false,default);
        Check("original diagonal50plane false delta reproduced",((int)diagonalOld.issues&64)!=0&&diagonalOld.footprintHasSupport&&diagonalOld.footprintHeightDelta<-.6f);
        Check("diagonal50plane centre reference accepted",(int)diagonalNew.issues==0);
        Console.WriteLine("DIAGONAL oldDelta="+diagonalOld.footprintHeightDelta+" oldSlope="+diagonalOld.footprintSlopeDegrees+" candidate="+diagonalNew.issues);
        float steep=Mathf.Tan(62*Mathf.Deg2Rad);
        var steepResult=new CandidateProbe(new TestScene{height=(x,z)=>steep*x,normal=new Vector3(-steep,1,0).normalized}).Measure(Vector3.zero,false,default);
        Check("above-controller slope remains rejected",((int)steepResult.issues&2)!=0);
        var tooHigh=new CandidateProbe(new TestScene{height=(x,z)=>x>.2f?.75f:0}).Measure(Vector3.zero,false,default);
        Check("too-high step remains rejected",((int)tooHigh.issues&64)!=0);
        var supportedStep=new CandidateProbe(new TestScene{height=(x,z)=>x>.2f?.5f:0}).Measure(Vector3.zero,false,default);
        Check("reachable half-metre tread preserves step acquisition",(int)supportedStep.issues==0&&Mathf.Abs(supportedStep.feet.y-.5f)<.00001f);
        var ledge=new CandidateProbe(new TestScene{height=(x,z)=>x<-.2f?0:1.5f}).Measure(new Vector3(0,1.5f,0),false,default);
        Check("real elevated ledge remains rejected",((int)ledge.issues&64)!=0);
        var occupied=new CandidateProbe(new TestScene{height=(x,z)=>0,occupied=true}).Measure(Vector3.zero,false,default);
        Check("occupancy evidence retained",((int)occupied.issues&4)!=0);
        var swept=new CandidateProbe(new TestScene{height=(x,z)=>0,swept=true}).Measure(new Vector3(1,0,0),true,Vector3.zero);
        Check("swept obstacle evidence retained",((int)swept.issues&8)!=0);
        var saturated=new CandidateProbe(new TestScene{height=(x,z)=>0,saturated=true}).Measure(Vector3.zero,false,default);
        Check("saturated queries remain rejected",((int)saturated.issues&16)!=0&&((int)saturated.issues&1)!=0);
        Console.WriteLine("RESULT checks="+checks+" failures="+failed);return failed==0?0:1;
    }
}
