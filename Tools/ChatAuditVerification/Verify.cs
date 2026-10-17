using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;

internal static class Verify
{
    private static readonly CSharpParseOptions Parse = new CSharpParseOptions(LanguageVersion.CSharp9);

    private static int Main(string[] args)
    {
        string root = args[0], output = args[1];
        var syntax = new List<object>();
        int errors = 0;
        var paths = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(Path.Combine(output, "changed-sources.json")));
        foreach (string path in paths)
        foreach (bool editor in new[] { false, true })
        {
            // note: Both player and Editor conditional branches are parsed with Unity's C# 9 language ceiling; this does not resolve Unity types.
            var symbols = new List<string> { "UNITY_6000_3_OR_NEWER", "UNITY_6000_0_OR_NEWER", "ENABLE_INPUT_SYSTEM", "DEVELOPMENT_BUILD" };
            if (editor) symbols.Add("UNITY_EDITOR");
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, path)), Parse.WithPreprocessorSymbols(symbols), path);
            var diagnostics = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();
            syntax.Add(new { path, editor, errors = diagnostics });
            errors += diagnostics.Length;
        }
        File.WriteAllText(Path.Combine(output, "syntax.json"), JsonConvert.SerializeObject(new {
            evidence = "ROSLYN_CSHARP9_SYNTAX_ONLY_NOT_UNITY_COMPILATION", sourceFiles = paths.Length, errors, syntax
        }, Formatting.Indented));
        Console.WriteLine("C# 9 syntax: " + paths.Length + " changed sources in player/Editor configurations; errors=" + errors);

        string profilePath = Path.Combine(root, "Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs");
        var profile = CSharpSyntaxTree.ParseText(File.ReadAllText(profilePath), Parse).GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "YQProfileSaveSystem");
        var methodNames = new HashSet<string> {
            "TryPrepareContinuationPublication", "TryPrepareContinuationPublicationCore", "TryPrepareAndPublishContinuation",
            "TryPublishContinuation", "TryPublishContinuationCore", "TryRequireContinuationOwner", "HasSameDurableContinuationManifest",
            "TryCaptureContinuationAuxiliary", "TryValidateContinuationPublication", "SameAuxiliaryDocuments", "FindProfile", "FindLatestCommit"
        };
        var typeNames = new HashSet<string> { "ProfileManifest", "ProfileEntry", "PreparedContinuationPublication", "ContinuationPublicationData" };
        var fields = new HashSet<string> { "_manifest", "_manifestUnsupported", "_profileTransactionInProgress", "_continuationReloadRequired",
            "_auxiliaryProviders", "_loadedAuxiliaryDocuments", "_preparedContinuationPublication", "_preparedContinuationData" };
        var members = profile.Members.Where(member =>
            member is MethodDeclarationSyntax m && methodNames.Contains(m.Identifier.ValueText) ||
            member is ClassDeclarationSyntax c && typeNames.Contains(c.Identifier.ValueText) ||
            member is FieldDeclarationSyntax f && f.Declaration.Variables.Any(v => fields.Contains(v.Identifier.ValueText))).ToArray();
        foreach (string method in methodNames)
            if (!members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.ValueText == method))
                throw new InvalidOperationException("Required production method missing: " + method);
        // note: Compile verbatim current publication/validation methods, never a reimplementation of their branching. Fixture owners below replace only Unity-dependent boundaries.
        string extracted = "using System; using System.Collections.Generic; using System.IO; using Newtonsoft.Json; using UnityEngine; " +
            "using FrontierTiming = YQGeneratedWorldRuntimeBuilder.FrontierPublicationDiagnostics;\npublic sealed partial class YQProfileSaveSystem {\n" +
            string.Join("\n", members.Select(m => m.ToFullString())) + "\n}";
        var foundation = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "Assets/Assets/Scripts/Data/State/YQStateFoundation.cs")), Parse).GetRoot();
        var receipt = foundation.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "YQProfileTransactionReceipt");
        var state = foundation.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "YQStateContract");
        var hash = state.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Sha256Hex");
        var constants = state.Members.OfType<FieldDeclarationSyntax>().Where(f => f.Declaration.Variables.Any(v =>
            v.Identifier.ValueText == "CurrentStateSchemaVersion" || v.Identifier.ValueText == "CurrentProfileManifestSchemaVersion"));
        string foundationSource = "using System; using System.Collections.Generic; using System.Globalization; using System.Security.Cryptography; using System.Text;\n" +
            receipt.ToFullString() + "\npublic static class YQStateContract {\n" + string.Join("\n", constants) + "\n" + hash.ToFullString() + "\n}";
        var trees = new[] {
            CSharpSyntaxTree.ParseText(extracted, Parse, "CurrentPublicationMethods.cs"),
            CSharpSyntaxTree.ParseText(foundationSource, Parse, "CurrentReceiptAndHash.cs"),
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "Assets/Assets/Scripts/Data/State/YQProfileCommitStore.cs")), Parse, "YQProfileCommitStore.cs"),
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "Tools/ChatAuditVerification/PublicationFixtures.cs")), Parse, "PublicationFixtures.cs")
        };
        var referencePaths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Concat(new[] { typeof(JsonConvert).Assembly.Location }).Distinct();
        var compilation = CSharpCompilation.Create("YourQuestPublicationFixtures", trees,
            referencePaths.Select(p => MetadataReference.CreateFromFile(p)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var binary = new MemoryStream();
        var emit = compilation.Emit(binary);
        var compileErrors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();
        File.WriteAllText(Path.Combine(output, "publication-compile.json"), JsonConvert.SerializeObject(new {
            evidence = "CURRENT_PUBLICATION_METHODS_AND_REAL_COMMIT_STORE_WITH_CONTROLLED_UNITY_OWNER_FIXTURES", success = emit.Success, errors = compileErrors
        }, Formatting.Indented));
        if (!emit.Success)
        {
            foreach (string error in compileErrors) Console.WriteLine(error);
            return 1;
        }
        var assembly = Assembly.Load(binary.ToArray());
        int failures = (int)assembly.GetType("PublicationFixtures").GetMethod("Run").Invoke(null, new object[] { output });
        int populationFailures = VerifyPopulation.Run(root, output);
        int movementFailures = VerifyMovement.Run(root, output);
        return failures == 0 && populationFailures == 0 && movementFailures == 0 && errors == 0 ? 0 : 1;
    }
}
