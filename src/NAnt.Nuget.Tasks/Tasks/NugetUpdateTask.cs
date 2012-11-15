using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.NuGet.Tasks.Common;
using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NAnt.NuGet.Tasks.Tasks
{
    [TaskName("nuget-update")]
    public class NugetUpdateTask : Task
    {
        readonly List<string> _sources = new List<string>();
        readonly List<string> _ids = new List<string>();
        IFileSystem _fileSystem;

        public IPackageRepositoryFactory RepositoryFactory { get; private set; }

        public IPackageSourceProvider SourceProvider { get; private set; }

        [TaskAttribute("solution", Required = true)]
        public FileInfo Solution { get; set; }

        [TaskAttribute("repository", Required = false)]
        public DirectoryInfo RepositoryPath { get; set; }

        [TaskAttribute("prerelease", Required = false), BooleanValidator]
        public bool Prerelease { get; set; }

        [TaskAttribute("safe", Required = false), BooleanValidator]
        public bool Safe { get; set; }

        public ICollection<string> Source
        {
            get { return _sources; }
        }

        public ICollection<string> Id
        {
            get { return _ids; }
        }

        protected override void ExecuteTask()
        {
            string dir = Solution.Directory.FullName;
            _fileSystem = new PhysicalFileSystem(dir);

            RepositoryFactory = new PackageRepositoryFactory();
            SourceProvider = new PackageSourceProvider(new Settings(_fileSystem));

            UpdateAllPackages(dir);
        }

        void UpdateAllPackages(string solutionDir)
        {
            Log(Level.Debug, "Scanning for projects");

            // Search recursively for all packages.config files
            var packagesConfigFiles = Directory.GetFiles(solutionDir, Constants.PackageReferenceFile, SearchOption.AllDirectories);
            var projects = packagesConfigFiles.Select(GetProject)
                .Where(p => p.Project != null)
                .ToList();

            if (projects.Count == 0)
            {
                Log(Level.Info, "No projects found");
                return;
            }

            string repositoryPath = GetRepositoryPathFromSolution(solutionDir);
            IPackageRepository sourceRepository = AggregateRepositoryHelper.CreateAggregateRepositoryFromSources(RepositoryFactory, SourceProvider, Source);

            foreach (var project in projects)
            {
                try
                {
                    UpdatePackages(project.PackagesConfigPath, project.Project, repositoryPath, sourceRepository);
                }
                catch (Exception e)
                {
                    Log(Level.Error, e.Message);
                }
            }
        }

        private void UpdatePackages(string packagesConfigPath, IMSBuildProjectSystem project = null, string repositoryPath = null, IPackageRepository sourceRepository = null)
        {
            // Get the msbuild project
            project = project ?? GetMSBuildProject(packagesConfigPath);

            // Resolve the repository path
            repositoryPath = repositoryPath ?? GetRepositoryPath(project.Root);

            var sharedRepositoryFileSystem = new PhysicalFileSystem(repositoryPath);
            var pathResolver = new DefaultPackagePathResolver(sharedRepositoryFileSystem);

            // Create the local and source repositories
            var sharedPackageRepository = new SharedPackageRepository(pathResolver, sharedRepositoryFileSystem, sharedRepositoryFileSystem);
            var localRepository = new PackageReferenceRepository(project, sharedPackageRepository);
            sourceRepository = sourceRepository ?? AggregateRepositoryHelper.CreateAggregateRepositoryFromSources(RepositoryFactory, SourceProvider, Source);
            IPackageConstraintProvider constraintProvider = localRepository;

            Log(Level.Info, "Updating project {0}", project.ProjectName);
            UpdatePackages(localRepository, sharedRepositoryFileSystem, sharedPackageRepository, sourceRepository, constraintProvider, pathResolver, project);
            project.Save();
        }

        internal void UpdatePackages(IPackageRepository localRepository,
                                     IFileSystem sharedRepositoryFileSystem,
                                     ISharedPackageRepository sharedPackageRepository,
                                     IPackageRepository sourceRepository,
                                     IPackageConstraintProvider constraintProvider,
                                     IPackagePathResolver pathResolver,
                                     IProjectSystem project)
        {
            var packageManager = new PackageManager(sourceRepository, pathResolver, sharedRepositoryFileSystem, sharedPackageRepository);

            var projectManager = new ProjectManager(sourceRepository, pathResolver, project, localRepository)
            {
                ConstraintProvider = constraintProvider
            };

            // Fix for work item 2411: When updating packages, we did not add packages to the shared package repository. 
            // Consequently, when querying the package reference repository, we would have package references with no backing package files in
            // the shared repository. This would cause the reference repository to skip the package assuming that the entry is invalid.
            projectManager.PackageReferenceAdded += (sender, eventArgs) =>
            {
                PackageExtractor.InstallPackage(packageManager, eventArgs.Package);
            };

            projectManager.Logger = project.Logger = new VerboseLogger(this);

            using (sourceRepository.StartOperation(RepositoryOperationNames.Update))
            {
                foreach (var package in GetPackages(localRepository))
                {
                    if (localRepository.Exists(package.Id))
                    {
                        try
                        {
                            // If the user explicitly allows prerelease or if the package being updated is prerelease we'll include prerelease versions in our list of packages
                            // being considered for an update.
                            bool allowPrerelease = Prerelease || !package.IsReleaseVersion();
                            if (Safe)
                            {
                                IVersionSpec safeRange = VersionUtility.GetSafeRange(package.Version);
                                projectManager.UpdatePackageReference(package.Id, safeRange, updateDependencies: true, allowPrereleaseVersions: allowPrerelease);
                            }
                            else
                            {
                                projectManager.UpdatePackageReference(package.Id, version: null, updateDependencies: true, allowPrereleaseVersions: allowPrerelease);
                            }
                        }
                        catch (InvalidOperationException e)
                        {
                            Log(Level.Warning, e.Message);
                        }
                    }
                }
            }
        }

        private IEnumerable<IPackage> GetPackages(IPackageRepository repository)
        {
            var packages = repository.GetPackages();
            if (Id.Any())
            {
                var packageIdSet = new HashSet<string>(packages.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
                var idSet = new HashSet<string>(Id, StringComparer.OrdinalIgnoreCase);
                var invalid = Id.Where(id => !packageIdSet.Contains(id));

                if (invalid.Any())
                {
                    throw new Exception(String.Format("Unable to find packages {0}", String.Join(", ", invalid)));
                }

                packages = packages.Where(r => idSet.Contains(r.Id));
            }
            var packageSorter = new PackageSorter(targetFramework: null);
            return packageSorter.GetPackagesByDependencyOrder(new ReadOnlyPackageRepository(packages)).Reverse();
        }

        private static ProjectPair GetProject(string packagesConfigPath)
        {
            IMSBuildProjectSystem msBuildProjectSystem = null;
            msBuildProjectSystem = GetMSBuildProject(packagesConfigPath);
            return new ProjectPair
            {
                PackagesConfigPath = packagesConfigPath,
                Project = msBuildProjectSystem
            };
        }

        private string GetRepositoryPath(string projectRoot)
        {
            string packagesDir = AbsoluteRepositoryPath;

            if (String.IsNullOrEmpty(packagesDir))
            {
                packagesDir = DefaultSettings.GetRepositoryPath();
                if (String.IsNullOrEmpty(packagesDir))
                {
                    // Try to resolve the packages directory from the project
                    string projectDir = Path.GetDirectoryName(projectRoot);
                    string solutionDir = ProjectHelper.GetSolutionDir(projectDir);

                    return GetRepositoryPathFromSolution(solutionDir);
                }
            }

            return GetPackagesDir(packagesDir);
        }

        private ISettings DefaultSettings
        {
            get { return Settings.LoadDefaultSettings(_fileSystem); }
        }

        private string AbsoluteRepositoryPath
        {
            get { return RepositoryPath != null ? RepositoryPath.FullName : null; }
        }

        private string GetRepositoryPathFromSolution(string solutionDir)
        {
            string packagesDir = AbsoluteRepositoryPath;

            if (String.IsNullOrEmpty(packagesDir) &&
                !String.IsNullOrEmpty(solutionDir))
            {
                packagesDir = Path.Combine(solutionDir, NuGetConstants.PackagesDirectoryName);
            }

            return GetPackagesDir(packagesDir);
        }

        private string GetPackagesDir(string packagesDir)
        {
            if (!String.IsNullOrEmpty(packagesDir))
            {
                // Get the full path to the packages directory
                packagesDir = Path.GetFullPath(packagesDir);

                // REVIEW: Do we need to check for existence?
                if (Directory.Exists(packagesDir))
                {
                    string currentDirectory = Directory.GetCurrentDirectory();
                    string relativePath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(currentDirectory), packagesDir);
                    //Console.WriteLine(NuGetResources.LookingForInstalledPackages, relativePath);
                    return packagesDir;
                }
            }

            //throw new CommandLineException(NuGetResources.UnableToLocatePackagesFolder);
            return null;
        }

        private static IMSBuildProjectSystem GetMSBuildProject(string packageReferenceFilePath)
        {
            // Try to locate the project file associated with this packages.config file
            string directory = Path.GetDirectoryName(packageReferenceFilePath);
            string projectFile;
            if (ProjectHelper.TryGetProjectFile(directory, out projectFile))
            {
                return new MSBuildProjectSystem(projectFile);
            }

            return null;
        }

        private struct ProjectPair
        {
            public string PackagesConfigPath { get; set; }
            public IMSBuildProjectSystem Project { get; set; }
        }

        private class VerboseLogger : ILogger
        {
            Task _task;
            public VerboseLogger(Task task)
            {
                _task = task;
            }

            public void Log(MessageLevel level, string message, params object[] args)
            {
                _task.Log(TranslateLevel(level), "- " + message, args);
            }

            static Level TranslateLevel(MessageLevel level)
            {
                switch (level)
                {
                    case MessageLevel.Debug: return Level.Debug;
                    case MessageLevel.Error: return Level.Error;
                    case MessageLevel.Info: return Level.Info;
                    case MessageLevel.Warning: return Level.Warning;
                    default: throw new ArgumentException();
                }
            }
        }
    }
}
