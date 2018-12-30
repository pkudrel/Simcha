using System;
using System.IO;
using System.Runtime.InteropServices;
using AbcVersionTool;
using Helpers;
using Nuke.Common;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.NuGet.NuGetTasks;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;

class Build : NukeBuild
{
    static readonly DateTime _buildDate = DateTime.UtcNow;
    [Parameter("Build counter from outside environment")] readonly int BuildCounter;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly string Configuration = "Release";


    [Solution("src/Simcha.sln")] readonly Solution Solution;


    AbcVersion Version => AbcVersionFactory.Create(BuildCounter, _buildDate);

    Project MainProject =>
        Solution.GetProject("Simcha").NotNull();


    AbsolutePath SourceDir => RootDirectory / "src";
    AbsolutePath ToolsDir => RootDirectory / "tools";
    AbsolutePath ArtifactsDir => RootDirectory / "_artifacts";
    AbsolutePath DevDir => RootDirectory / "dev";
    AbsolutePath TmpBuild => TemporaryDirectory / "build";
    AbsolutePath LibzPath => ToolsDir / "LibZ.Tool" / "tools" / "libz.exe";
    AbsolutePath NugetPath => ToolsDir / "nuget" / "nuget.exe";
    AbsolutePath SevenZipPath => ToolsDir / "7-Zip.CommandLine" / "tools" / "7za.exe";


    Target AbcVersionTarget => _ => _
        .Executes(() =>
        {
            Logger.Info(Version.InformationalVersion);
        });


    Target Information => _ => _
        .Executes(() =>
        {
            var b = Version;
            Logger.Info($"Host: {Host}");
            Logger.Info($"Configuration: {Configuration}");
            Logger.Info($"Version: {b.SemVersion}");
            Logger.Info($"Version: {b.InformationalVersion}");

            SetVariable("NUGET_EXE", NugetPath);
        });


    Target CheckTools => _ => _
        .DependsOn(Information)
        .Executes(() =>
        {
            Downloader.DownloadIfNotExists("https://dist.nuget.org/win-x86-commandline/latest/nuget.exe", NugetPath,
                "Nuget");
            var toolsNugetFile = ToolsDir / "packages.config";
            using (var process = ProcessTasks.StartProcess(
                NugetPath,
                $"install   {toolsNugetFile} -OutputDirectory {ToolsDir} -ExcludeVersion",
                SourceDir))
            {
                process.AssertWaitForExit();
                ControlFlow.AssertWarn(process.ExitCode == 0,
                    "Nuget restore report generation process exited with some errors.");
            }
        });

    Target Clean => _ => _
        .DependsOn(CheckTools)
        .Executes(() =>
        {
            EnsureExistingDirectory(TmpBuild);
            DeleteDirectories(GlobDirectories(TmpBuild, "**/*"));
            EnsureCleanDirectory(ArtifactsDir);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            using (var process = ProcessTasks.StartProcess(
                NugetPath,
                $"restore  {Solution.Path}",
                SourceDir))
            {
                process.AssertWaitForExit();
                ControlFlow.AssertWarn(process.ExitCode == 0,
                    "Nuget restore report generation process exited with some errors.");
            }
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>

        {
            var buildOut = TmpBuild / CommonDir.Build /
                           MainProject.Name;
            EnsureExistingDirectory(buildOut);

            MSBuild(s => s
                .SetTargetPath(Solution)
                .SetTargets("Rebuild")
                .SetOutDir(buildOut)
                .SetVerbosity(MSBuildVerbosity.Quiet)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(Version.AssemblyVersion)
                .SetFileVersion(Version.FileVersion)
                .SetInformationalVersion(Version.InformationalVersion)
                .SetMaxCpuCount(Environment.ProcessorCount)
                .SetNodeReuse(IsLocalBuild));
        });

    Target Marge => _ => _
        .DependsOn(Compile)
        .Executes(() =>

        {
            var buildOut = TmpBuild / CommonDir.Build /
                           MainProject.Name;
            var margeOut = TmpBuild / CommonDir.Merge /
                           MainProject.Name;

            EnsureExistingDirectory(margeOut);
            CopyDirectoryRecursively(buildOut, margeOut);

            using (var process = ProcessTasks.StartProcess(
                LibzPath,
                "inject-dll --assembly NugetComposer.exe --include *.dll --move",
                margeOut))
            {
                process.AssertWaitForExit();
                ControlFlow.AssertWarn(process.ExitCode == 0,
                    "Libz report generation process exited with some errors.");
            }
        });


    Target CopyToReady => _ => _
        .DependsOn(Marge)
        .Executes(() =>

        {
            var margeOut = TmpBuild / CommonDir.Merge /
                           MainProject.Name;

            var readyOut = TmpBuild / CommonDir.Ready /
                           MainProject.Name;

            EnsureExistingDirectory(readyOut);
            CopyFile(margeOut / "NugetComposer.exe", readyOut / $"{MainProject.Name.ToKebabCase()}.exe");
            var files = GlobFiles(SourceDir / "build" / "_res" / "config", "*.json");
            foreach (var file in files)
            {
                var dst = Path.Combine(readyOut, Path.GetFileName(file));
                CopyFile(file, dst, FileExistsPolicy.Skip);
            }

        });

    Target MakeNuget => _ => _
        .DependsOn(CopyToReady)
        .Executes(() =>
        {
            var nugetOut = TmpBuild / CommonDir.Nuget / MainProject.Name;

            var readyOut = TmpBuild / CommonDir.Ready /
                           MainProject.Name;

            var scaffoldDir = TmpBuild / "nuget-scaffold" / MainProject.Name;
            var scaffoldToolDir = scaffoldDir / "tools";


            EnsureExistingDirectory(scaffoldDir);
            EnsureExistingDirectory(nugetOut);
            EnsureExistingDirectory(readyOut);
            EnsureExistingDirectory(scaffoldToolDir);
            CopyDirectoryRecursively(readyOut, scaffoldToolDir);


            GlobFiles(SourceDir / "build" / "_res" / "nuget", "*.nuspec")
                .ForEach(x => NuGetPack(s => s
                    .SetTargetPath(x)
                    .SetConfiguration(Configuration)
                    .SetVersion(Version.NugetVersion)
                    .SetProperty("currentyear", DateTime.Now.Year.ToString())
                    .SetBasePath(scaffoldDir)
                    .SetOutputDirectory(nugetOut)
                    .EnableNoPackageAnalysis()));
        });

    Target MakeZip => _ => _
        .DependsOn(MakeNuget)
        .Executes(() =>
        {
            var readyOut = TmpBuild / CommonDir.Ready / MainProject.Name;
            var zipOut = TmpBuild / CommonDir.Zip / MainProject.Name;
            EnsureExistingDirectory(zipOut);
            // terraform_0.11.11_linux_amd64.zip
            var baseName = $"{MainProject.Name}".ToKebabCase();
            var filename = $"{baseName}.{Version.SemVersion}.zip";
            var zipFullOut = zipOut / filename;
            var process = ProcessTasks.StartProcess(SevenZipPath, $" a {zipFullOut} .\\*", readyOut);
            process?.WaitForExit();
        });

    


    Target PublishLocal => _ => _
        .OnlyWhen(() => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DlLocalPackages")) == false)
        .DependsOn(MakeZip)
        .Executes(() =>
        {
            var baseName = $"{MainProject.Name}".ToKebabCase();

            using (Logger.Block("Local packages"))
            {

                var localPackages = Environment.GetEnvironmentVariable("DlLocalPackages");
          
                var dirPath = Path.Combine(localPackages, baseName);


                var zipOut = TmpBuild / CommonDir.Zip / MainProject.Name;
                var files = GlobFiles(zipOut, "**/*");
                EnsureExistingDirectory(dirPath);
                foreach (var file in files)
                {
                    var dst = Path.Combine(dirPath, Path.GetFileName(file));
                   // CopyFile(file, dst, FileExistsPolicy.Skip);
                }
            }

            using (Logger.Block("App; app.standalone"))
            {

                var readyOut = TmpBuild / CommonDir.Ready /
                               MainProject.Name;

                var appStandalone = RootDirectory / CommonDir.DashDev / "app.standalone";
                var files = GlobFiles(readyOut, "**/*.exe");
                EnsureExistingDirectory(appStandalone);
                foreach (var file in files)
                {
                    var dst = Path.Combine(appStandalone, Path.GetFileName(file));
                    CopyFile(file, dst, FileExistsPolicy.Overwrite);
                }
            }



        });

    public static int Main() => Execute<Build>(x => x.PublishLocal);
}