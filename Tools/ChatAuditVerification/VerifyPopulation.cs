using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;

internal static class VerifyPopulation
{
    internal static int Run(string root, string output)
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp9);
        ClassDeclarationSyntax Read(string path, string name) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, path)), parse)
            .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == name);
        string Slice(ClassDeclarationSyntax type, string name, string[] methods, string[] nested = null, bool constants = false)
        {
            var selected = type.Members.Where(m => m is MethodDeclarationSyntax method && methods.Contains(method.Identifier.ValueText) ||
                m is ClassDeclarationSyntax inner && (nested ?? Array.Empty<string>()).Contains(inner.Identifier.ValueText) ||
                constants && m is FieldDeclarationSyntax field && field.Modifiers.Any(token => token.IsKind(SyntaxKind.ConstKeyword))).ToArray();
            foreach (string method in methods)
                if (!selected.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.ValueText == method))
                    throw new InvalidOperationException("Missing production member: " + method);
            return "using System; using System.Collections; using System.Collections.Generic; using System.Text; using UnityEngine; using TMPro;\n" +
                "public static partial class " + name + " {\n" + string.Join("\n", selected.Select(m => m.ToFullString())) + "\n}";
        }
        var population = Read("Assets/Assets/Scripts/Generated/YQGeneratedWorldPopulation.cs", "YQGeneratedWorldPopulation");
        var repair = Read("Assets/Assets/Scripts/Tutorial/YQRuntimeUrpMaterialRepair.cs", "YQRuntimeUrpMaterialRepair");
        var index = Read("Assets/Assets/Scripts/Generated/YQRuntimeCreatureAssetIndex.cs", "YQRuntimeCreatureAssetIndex");
        var sources = new[] {
            Slice(population, "YQGeneratedWorldPopulation", new[] { "SameNpcPopulationBody", "SameNpcPopulationStringList", "ShouldMaterializeNpc", "SafeText", "InstantiateRegisteredPrefabRoutine" }, new[] { "ContinuationPopulationSnapshot" }),
            Slice(repair, "YQRuntimeUrpMaterialRepair", new[] { "RepairHierarchy", "RepairTextHierarchy", "StabilizeLodHierarchy", "GetTransformDepth", "ResolveStableLodThreshold" }),
            Slice(index, "YQRuntimeCreatureAssetIndex", new[] { "ClassifyEntry", "ClassifyMetadataPath", "ClassifySemanticEntry", "ExcludesHumanCategory", "BuildEntrySemantic", "BuildEntryLocalIdentitySemantic", "NormalizeSemanticText", "SafeText", "HasAny", "ContainsToken" }, constants: true),
            File.ReadAllText(Path.Combine(root, "Tools/ChatAuditVerification/PopulationFixtures.cs"))
        };
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Concat(new[] { typeof(JsonConvert).Assembly.Location }).Distinct().Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("YourQuestPopulationFixtures", sources.Select((s, i) =>
            CSharpSyntaxTree.ParseText(s, parse, "PopulationSource" + i + ".cs")), references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var binary = new MemoryStream();
        var emit = compilation.Emit(binary);
        string[] errors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();
        File.WriteAllText(Path.Combine(output, "population-compile.json"), JsonConvert.SerializeObject(new {
            evidence = "CURRENT_MANAGED_METHODS_WITH_CONTROLLED_UNITY_AND_DTO_FIXTURES_NOT_UNITY_COMPILATION", success = emit.Success, errors
        }, Formatting.Indented));
        if (!emit.Success) { foreach (string error in errors) Console.WriteLine(error); return 1; }
        var assembly = Assembly.Load(binary.ToArray());
        return (int)assembly.GetType("PopulationFixtures").GetMethod("Run").Invoke(null, new object[] { output });
    }
}
