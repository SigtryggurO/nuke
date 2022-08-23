// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Nuke.Common.ProjectModel;
using Nuke.Common.Utilities.Collections;
using Serilog;

namespace Nuke.Common.Execution
{
    [PublicAPI]
    [Obsolete("This attribute solely checks whether solutions have active build configurations for the build project, " +
              "which leads to an error because the build project cannot be compiled again while executing. " +
              $"The sanity check is now integrated in the {nameof(SolutionAttribute)}")]
    public class CheckBuildProjectConfigurationsAttribute : BuildExtensionAttributeBase, IOnBuildInitialized
    {
        public int TimeoutInMilliseconds { get; set; } = 500;

        public void OnBuildInitialized(
            IReadOnlyCollection<ExecutableTarget> executableTargets,
            IReadOnlyCollection<ExecutableTarget> executionPlan)
        {
            if (Build.IsInterceptorExecution)
                return;

            if (!Task.Run(CheckConfiguration).Wait(TimeoutInMilliseconds))
                Log.Warning("Could not complete checking build configurations within {Timeout} milliseconds", TimeoutInMilliseconds);

            Task CheckConfiguration()
            {
                var rootDirectory = new DirectoryInfo(Build.RootDirectory);
                new[] { rootDirectory }
                    .Concat(rootDirectory.EnumerateDirectories("*", SearchOption.AllDirectories).Where(x => !x.Name.StartsWith(".")))
                    .SelectMany(x => x.GetFiles("*.sln", SearchOption.TopDirectoryOnly))
                    .Select(x => x.FullName)
                    .Select(SolutionModelTasks.ParseSolution)
                    .SelectMany(x => x.Projects)
                    .Where(x => x.Directory.Equals(Build.BuildProjectDirectory))
                    .Where(x => x.Configurations.Any(y => y.Key.Contains("Build")))
                    .ForEach(x => Log.Warning("Solution {Solution} has an active build configuration for {Project}", x.Solution, x));

                return Task.CompletedTask;
            }
        }
    }
}
