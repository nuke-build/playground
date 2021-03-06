using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Coverlet;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

partial class Build
{
    [Parameter] bool IgnoreFailedSources;
    [GitVersion] readonly GitVersion GitVersion;

    Target FluentApi => _ => _
        .Executes(() =>
        {
            DotNetRestore(_ => _
                .SetProjectFile(Solution)
                .SetIgnoreFailedSources(IgnoreFailedSources));

            DotNetBuild(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetVerbosity(DotNetVerbosity.Minimal));

            DotNetPack(_ => _
                .SetProject(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(GitVersion.NuGetVersionV2)
                .EnableNoBuild()
                .SetOutputDirectory(OutputDirectory));
        });

    [Parameter] bool EnableCoverage;

    // Enable code coverage via parameter and use SourceLink on server builds
    Target FluentApiConditional => _ => _
        .Executes(() =>
        {
            DotNetTest(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .When(EnableCoverage, _ => _
                    .EnableCollectCoverage()
                    .SetCoverletOutputFormat(CoverletOutputFormat.cobertura)
                    .When(IsServerBuild, _ => _
                        .EnableUseSourceLink())));
        });

    // Publish multiple projects with their respective target frameworks
    Target FluentApiCombinatorial01 => _ => _
        .Executes(() =>
        {
            var publishConfigurations =
                from project in Solution.GetProjects("*rary")
                from framework in project.GetTargetFrameworks()
                select new {project, framework};

            DotNetPublish(_ => _
                .SetConfiguration(Configuration)
                .CombineWith(publishConfigurations, (_, v) => _
                    .SetProject(v.project)
                    .SetFramework(v.framework)));
        });

    AbsolutePath OutputDirectory => RootDirectory / ".." / ".." / "output";

    // Push multiple packages in parallel and aggregate errors
    Target FluentApiCombinatorial02 => _ => _
        .Executes(() =>
        {
            var packages = OutputDirectory.GlobFiles("*.nupkg").NotEmpty();

            DotNetNuGetPush(_ => _
                    .SetSource("url")
                    .SetApiKey("api-key")
                    .CombineWith(packages, (_, v) => _
                        .SetTargetPath(v)),
                degreeOfParallelism: 5,
                completeOnFailure: true);
        });
}
