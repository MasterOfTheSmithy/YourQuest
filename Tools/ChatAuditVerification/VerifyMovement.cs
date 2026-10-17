using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;

internal static class VerifyMovement
{
    internal static int Run(string root, string output)
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp9);
        string Slice(string path, string type, params string[] names)
        {
            var source = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, path)), parse).GetRoot()
                .DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == type);
            var members = source.Members.Where(member =>
                member is MethodDeclarationSyntax m && names.Contains(m.Identifier.ValueText) ||
                member is PropertyDeclarationSyntax p && names.Contains(p.Identifier.ValueText)).ToArray();
            if (members.Length != names.Length) throw new InvalidOperationException("Missing/ambiguous production movement member in " + path);
            return "using System; using UnityEngine; public sealed partial class " + type + " {\n" +
                string.Join("\n", members.Select(m => m.ToFullString())) + "\n}";
        }
        var sources = new[] {
            Slice("Assets/Assets/Scripts/Tutorial/YQInvestorPlayerMotor.cs", "YQInvestorPlayerMotor",
                "BindStreamedMovementAdmission", "TryResolveStreamedMovementAdmission", "MoveWithTraversalGate"),
            Slice("Assets/Assets/Scripts/Tutorial/YQInvestorPlayerMotor.Traversal.cs", "YQInvestorPlayerMotor",
                "BeginMotionFrame", "CaptureMotionIntegrationState", "EndMotionFrame", "ResetMotionSample"),
            Slice("Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs", "YQPlayerFollowingSemanticChunkStreamer",
                "IsMovementAdmissionAvailable", "IsPlayerMovementAdmissionAvailable", "BeginAggregateBudgetFrame",
                "CanAdvanceAggregateWork", "TryCompleteGroundSafetySynchronization", "RecordAggregateWorkSlice"),
            File.ReadAllText(Path.Combine(root, "Tools/ChatAuditVerification/MovementFixtures.cs"))
        };
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Concat(new[] { typeof(JsonConvert).Assembly.Location }).Distinct().Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("YourQuestMovementFixtures", sources.Select((source, i) =>
            CSharpSyntaxTree.ParseText(source, parse, "MovementSource" + i + ".cs")), references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var binary = new MemoryStream();
        var emit = compilation.Emit(binary);
        string[] errors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();
        File.WriteAllText(Path.Combine(output, "movement-compile.json"), JsonConvert.SerializeObject(new {
            evidence = "CURRENT_MANAGED_METHODS_WITH_CONTROLLED_UNITY_FIXTURES_NOT_UNITY_COMPILATION", success = emit.Success, errors
        }, Formatting.Indented));
        if (!emit.Success) { foreach (string error in errors) Console.WriteLine(error); return 1; }
        return (int)Assembly.Load(binary.ToArray()).GetType("MovementFixtures").GetMethod("Run").Invoke(null, new object[] { output });
    }
}
