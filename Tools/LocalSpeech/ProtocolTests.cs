using System;
using System.IO;
using System.Linq;

internal static class ProtocolTests
{
    private static void Require(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static void Main(string[] args)
    {
        byte[] wire = File.ReadAllBytes(args[0]);
        var buffer = new YQSpeechPcmBuffer();
        // note: Network reads split headers, float samples and terminators at arbitrary boundaries.
        int[] splits = { 1, 3, 7, 4091, 29, 32768 };
        for (int offset = 0, i = 0; offset < wire.Length; i++)
        {
            int count = Math.Min(splits[i % splits.Length], wire.Length - offset);
            byte[] part = new byte[count]; Buffer.BlockCopy(wire, offset, part, 0, count);
            if (!buffer.Receive(part, count)) throw new Exception("Invalid fragment " + i);
            offset += count;
        }
        Require(buffer.Complete() && buffer.Ended && buffer.BufferedSamples > 24000, "real Pocket wire stream accepted");
        int samples = buffer.BufferedSamples;
        float[] pcm = new float[samples + 13]; buffer.Read(pcm);
        Require(buffer.BufferedSamples == 0 && pcm.Take(samples).Any(v => Math.Abs(v) > .001f) && pcm.Skip(samples).All(v => v == 0f), "audio drained and underrun padding bounded");
        var truncated = new YQSpeechPcmBuffer();truncated.Receive(wire,wire.Length-1);
        Require(!truncated.Complete(), "truncated transport rejected");
        byte[] bad = (byte[])wire.Clone();bad[0] = 0;
        Require(!new YQSpeechPcmBuffer().Receive(bad,bad.Length), "invalid protocol rejected");
        bad=(byte[])wire.Clone();Buffer.BlockCopy(BitConverter.GetBytes(uint.MaxValue),0,bad,12,4);
        Require(!new YQSpeechPcmBuffer().Receive(bad,bad.Length), "oversized frame rejected");
        bad=(byte[])wire.Clone();Buffer.BlockCopy(BitConverter.GetBytes(float.NaN),0,bad,16,4);
        Require(!new YQSpeechPcmBuffer().Receive(bad,bad.Length), "non-finite synthesis rejected");
        Require(!buffer.Receive(new byte[]{1},1), "post-terminator bytes rejected");
        Require(YQVoiceRouteRules.CanHear(36f,6f,true) && !YQVoiceRouteRules.CanHear(36.01f,6f,true), "listener hearing boundary enforced");
        Require(!YQVoiceRouteRules.CanHear(1f,6f,false) && !YQVoiceRouteRules.CanHear(float.NaN,6f,true), "inactive and invalid candidates excluded");
        Require(YQVoiceRouteRules.Prefer(4f,"far-id",9f,"near-id"), "closest eligible listener wins");
        Require(YQVoiceRouteRules.Prefer(4f,"npc-a",4f,"npc-b") && !YQVoiceRouteRules.Prefer(4f,"npc-b",4f,"npc-a"), "equal distances resolve by stable identity");
    }
}
