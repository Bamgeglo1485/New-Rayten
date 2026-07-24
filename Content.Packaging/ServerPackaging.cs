using System.Diagnostics;
using System.IO.Compression;
using Robust.Packaging;
using Robust.Packaging.AssetProcessing;
using Robust.Packaging.AssetProcessing.Passes;
using Robust.Packaging.Utility;
using Robust.Shared.Timing;

namespace Content.Packaging;

public static class ServerPackaging
{
    private static readonly List<PlatformReg> Platforms = new()
    {
        new PlatformReg("win-x64", "Windows", true),
        new PlatformReg("win-arm64", "Windows", true),
        new PlatformReg("linux-x64", "Linux", true),
        new PlatformReg("linux-arm64", "Linux", true),
        new PlatformReg("osx-x64", "MacOS", true),
        new PlatformReg("osx-arm64", "MacOS", true),
        // Non-default platforms (i.e. for Watchdog Git)
        new PlatformReg("freebsd-x64", "FreeBSD", false),
    };

    private static IReadOnlySet<string> ServerContentIgnoresResources { get; } = new HashSet<string>
    {
        "ServerInfo",
        "Changelog",
    };

    private static List<string> PlatformRids => Platforms
        .Select(o => o.Rid)
        .ToList();

    private static List<string> PlatformRidsDefault => Platforms
        .Where(o => o.BuildByDefault)
        .Select(o => o.Rid)
        .ToList();

    private static readonly List<string> ServerNotExtraAssemblies = new()
    {
        "JetBrains.Annotations",
    };

    private static readonly HashSet<string> BinSkipFolders = new()
    {
        // Roslyn localization files, screw em.
        "cs",
        "de",
        "es",
        "fr",
        "it",
        "ja",
        "ko",
        "pl",
        "pt-BR",
        "ru",
        "tr",
        "zh-Hans",
        "zh-Hant"
    };

    // RAYTEN STARTS
    private static readonly List<string> ContentProjectPrefixes = new()
    {
        "Content.Trauma",
        "Content.Goobstation",
    };
    // RAYTEN ENDS

    public static async Task PackageServer(bool skipBuild, bool hybridAcz, bool logBuild, IPackageLogger logger, string configuration, List<string>? platforms = null)
    {
        if (platforms == null)
        {
            platforms ??= PlatformRidsDefault;
        }

        if (hybridAcz)
        {
            await ClientPackaging.PackageClient(skipBuild, logBuild, configuration, logger);
        }

        foreach (var platform in Platforms)
        {
            if (!platforms.Contains(platform.Rid))
                continue;

            await BuildPlatform(platform, skipBuild, hybridAcz, logBuild, configuration, logger);
        }
    }

    private static async Task BuildPlatform(PlatformReg platform,
        bool skipBuild,
        bool hybridAcz,
        bool logBuild,
        string configuration,
        IPackageLogger logger)
    {
        logger.Info($"Building project for {platform.TargetOs}...");

        if (!skipBuild)
        {
            // RAYTEN STARTS
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList =
                {
                    "build",
                    Path.Combine("Content.Server", "Content.Server.csproj"),
                    "-c", configuration,
                    "--nologo",
                    "/v:m",
                    $"/p:TargetOs={platform.TargetOs}",
                    "/t:Rebuild",
                    "/p:FullRelease=true",
                    "/m"
                }
            };

            if (logBuild)
            {
                startInfo.ArgumentList.Add($"/bl:{Path.Combine("release", $"server-{platform.Rid}.binlog")}");
                startInfo.ArgumentList.Add("/p:ReportAnalyzer=true");
            }

            await ProcessHelpers.RunCheck(startInfo);
            // RAYTEN ENDS

            await PublishClientServer(platform.Rid, platform.TargetOs, configuration);
        }

        logger.Info($"Packaging {platform.Rid} server...");

        var sw = RStopwatch.StartNew();
        {
            await using var zipFile =
                File.Open(Path.Combine("release", $"SS14.Server_{platform.Rid}.zip"), FileMode.Create, FileAccess.ReadWrite);
            using var zip = new ZipArchive(zipFile, ZipArchiveMode.Update);
            var writer = new AssetPassZipWriter(zip);

            await WriteServerResources(platform, "", writer, logger, hybridAcz, default);
            await writer.FinishedTask;
        }

        logger.Info($"Finished packaging server in {sw.Elapsed}");
    }

    private static async Task PublishClientServer(string runtime, string targetOs, string configuration)
    {
        await ProcessHelpers.RunCheck(new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList =
            {
                "publish",
                "--runtime", runtime,
                "--no-self-contained",
                "-c", configuration,
                $"/p:TargetOs={targetOs}",
                "/p:FullRelease=True",
                "/m",
                "RobustToolbox/Robust.Server/Robust.Server.csproj"
            }
        });
    }

    private static async Task WriteServerResources(
        PlatformReg platform,
        string contentDir,
        AssetPass pass,
        IPackageLogger logger,
        bool hybridAcz,
        CancellationToken cancel)
    {
        var graph = new RobustServerAssetGraph();
        var passes = graph.AllPasses.ToList();

        pass.Dependencies.Add(new AssetPassDependency(graph.Output.Name));
        passes.Add(pass);

        AssetGraph.CalculateGraph(passes, logger);

        var inputPassCore = graph.InputCore;
        var inputPassResources = graph.InputResources;

        var sourcePath = Path.Combine(contentDir, "bin", "Content.Server");

        // RAYTEN STARTS
        var deps = DepsHandler.Load(Path.Combine(sourcePath, "Content.Server.deps.json"));
        // RAYTEN ENDS

        var contentAssemblies = GetContentAssemblyNamesToCopy(deps, "Server");

        await RobustSharedPackaging.DoResourceCopy(
            Path.Combine("RobustToolbox", "bin", "Server",
            platform.Rid,
            "publish"),
            inputPassCore,
            BinSkipFolders,
            cancel: cancel);

        await RobustSharedPackaging.WriteContentAssemblies(
            inputPassResources,
            contentDir,
            "Content.Server",
            contentAssemblies,
            cancel: cancel);

        await RobustServerPackaging.WriteServerResources(
            contentDir,
            inputPassResources,
            ServerContentIgnoresResources.Concat(SharedPackaging.AdditionalIgnoredResources).ToHashSet(),
            cancel);

        if (hybridAcz)
        {
            inputPassCore.InjectFileFromDisk("Content.Client.zip", Path.Combine("release", "SS14.Client.zip"));
        }

        inputPassCore.InjectFinished();
        inputPassResources.InjectFinished();
    }

    // RAYTEN STARTS
    public static IEnumerable<string> GetContentAssemblyNamesToCopy(DepsHandler deps, string side)
    {
        var depsContent = new HashSet<string>();
        var depsRobust = new HashSet<string>();

        // Add Content.Server/Client assemblies
        depsContent.UnionWith(deps.RecursiveGetLibrariesFrom($"Content.{side}").SelectMany(GetLibraryNames));
        depsRobust.UnionWith(deps.RecursiveGetLibrariesFrom($"Robust.{side}").SelectMany(GetLibraryNames));

        // Add assemblies from all content projects
        foreach (var prefix in ContentProjectPrefixes)
        {
            depsContent.UnionWith(deps.RecursiveGetLibrariesFrom($"{prefix}.{side}").SelectMany(GetLibraryNames));
        }

        var depsContentExclusive = depsContent.Except(depsRobust).ToHashSet();

        // Remove .dll suffix and apply filtering.
        var names = depsContentExclusive.Select(p => p[..^4]).Where(p => !ServerNotExtraAssemblies.Any(p.StartsWith));

        return names;

        IEnumerable<string> GetLibraryNames(string library) => deps.Libraries[library].GetDllNames();
    }
    // RAYTEN ENDS

    private readonly record struct PlatformReg(string Rid, string TargetOs, bool BuildByDefault);
}
