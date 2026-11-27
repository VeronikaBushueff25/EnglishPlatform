using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace EnglishPlatform.Architecture.Tests;

/// <summary>
/// Правила модульного монолита: модуль видит у соседей только Contracts,
/// домен не зависит от инфраструктуры, Contracts ни от чего не зависят.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private const string Root = "EnglishPlatform";

    public static readonly string[] Modules =
    [
        "Identity", "Users", "LearningProfiles", "Vocabulary", "LearningSessions",
        "Exercises", "SpacedRepetition", "Grammar", "Ai", "Analytics", "Pets",
    ];

    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Contracts"];

    public static TheoryData<string> ModuleNames => new(Modules);

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_does_not_depend_on_other_modules_internals(string module)
    {
        string[] forbidden = Modules
            .Where(other => other != module)
            .SelectMany(other => new[] { "Domain", "Application", "Infrastructure" }.Select(layer => $"{Root}.{other}.{layer}"))
            .ToArray();

        foreach (string layer in Layers)
        {
            AssertNoDependency(Load(module, layer), forbidden);
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Domain_does_not_depend_on_application_or_infrastructure(string module)
    {
        AssertNoDependency(
            Load(module, "Domain"),
            $"{Root}.{module}.Application",
            $"{Root}.{module}.Infrastructure",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Application_does_not_depend_on_infrastructure(string module)
    {
        AssertNoDependency(Load(module, "Application"), $"{Root}.{module}.Infrastructure", "Microsoft.AspNetCore");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Contracts_do_not_depend_on_module_internals(string module)
    {
        AssertNoDependency(
            Load(module, "Contracts"),
            $"{Root}.{module}.Domain",
            $"{Root}.{module}.Application",
            $"{Root}.{module}.Infrastructure",
            "Microsoft.EntityFrameworkCore");
    }

    private static Assembly Load(string module, string layer) => Assembly.Load($"{Root}.{module}.{layer}");

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"{assembly.GetName().Name} нарушает границы модулей: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
