using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.NuGet.Tasks.Common;
using NAnt.NuGet.Tasks.Types;
using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NAnt.NuGet.Tasks.Tasks
{
    [TaskName("nuget-get-dependencies")]
    public class NugetGetDependenciesTask : Task
    {
        readonly List<string> _sources = new List<string>();
        readonly List<string> _ids = new List<string>();
        IFileSystem _fileSystem;

        public IPackageRepositoryFactory RepositoryFactory { get; private set; }

        public IPackageSourceProvider SourceProvider { get; private set; }

        [TaskAttribute("solution-dir", Required = true)]
        public DirectoryInfo SolutionDir { get; set; }

        [TaskAttribute("project")]
        public FileInfo ProjectFile { get; set; }

        [TaskAttribute("project-dir")]
        public DirectoryInfo ProjectDir { get; set; }

        [TaskAttribute("id"), StringValidator(AllowEmpty = false)]
        public string ReferenceId { get; set; }

        [TaskAttribute("repository", Required = false)]
        public DirectoryInfo RepositoryPath { get; set; }

        [TaskAttribute("allow-newer"), BooleanValidator]
        public bool AllowNewer { get; set; }

        private ISettings DefaultSettings
        {
            get { return Settings.LoadDefaultSettings(_fileSystem); }
        }

        private string AbsoluteRepositoryPath
        {
            get { return RepositoryPath != null ? RepositoryPath.FullName : null; }
        }

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
            if (ProjectFile == null && ProjectDir == null)
                throw new ValidationException("Either project or project-dir must be set on <nuget-get-dependencies />.");

            string dir = SolutionDir.FullName;
            _fileSystem = new PhysicalFileSystem(dir);

            string projectDir = ProjectFile == null ? ProjectDir.FullName : ProjectFile.Directory.FullName;

            RepositoryFactory = new PackageRepositoryFactory();
            SourceProvider = new PackageSourceProvider(new Settings(_fileSystem));

            var packagesConfigFiles = Directory.GetFiles(projectDir, Constants.PackageReferenceFile, SearchOption.AllDirectories);
            var project = packagesConfigFiles.Select(GetProject)
                .Where(p => p.Project != null)
                .SingleOrDefault();

            if (project == null)
            {
                throw new BuildException("No project found", Location);
            }

            string repositoryPath = GetRepositoryPathFromSolution(dir);
            IPackageRepository sourceRepository = AggregateRepositoryHelper.CreateAggregateRepositoryFromSources(RepositoryFactory, SourceProvider, Source);

            var references = GetReferences(project.PackagesConfigPath, project.Project, repositoryPath, sourceRepository);
            var deps = new NuGetDependencies
            {
                Dependencies = references.Select(GetDependency).ToArray()
            };

            Project.DataTypeReferences.Add(ReferenceId, deps);
        }

        private NuGetDependency GetDependency(PackageDependency dep)
        {
            if (AllowNewer)
            {
                return new NuGetDependency
                {
                    Id = dep.Id,
                    MinVersion = dep.VersionSpec.MinVersion.ToString()
                };
            }
            return dep;
        }

        private IEnumerable<PackageDependency> GetReferences(string packagesConfigPath, IMSBuildProjectSystem project = null, string repositoryPath = null, IPackageRepository sourceRepository = null)
        {
            // Get the msbuild project
            project = project ?? NugetUpdateTask.GetMSBuildProject(packagesConfigPath);

            // Resolve the repository path
            repositoryPath = repositoryPath ?? GetRepositoryPath(project.Root);

            var sharedRepositoryFileSystem = new PhysicalFileSystem(repositoryPath);
            var pathResolver = new DefaultPackagePathResolver(sharedRepositoryFileSystem);

            // Create the local and source repositories
            var sharedPackageRepository = new SharedPackageRepository(pathResolver, sharedRepositoryFileSystem, sharedRepositoryFileSystem);
            var localRepository = new PackageReferenceRepository(project, sharedPackageRepository);
            sourceRepository = sourceRepository ?? AggregateRepositoryHelper.CreateAggregateRepositoryFromSources(RepositoryFactory, SourceProvider, Source);
            IPackageConstraintProvider constraintProvider = localRepository;

            return GetReferences(localRepository, sharedRepositoryFileSystem, sharedPackageRepository, sourceRepository, constraintProvider, pathResolver, project);
        }

        internal IEnumerable<PackageDependency> GetReferences(IPackageRepository localRepository,
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

            projectManager.Logger = project.Logger = new NugetUpdateTask.VerboseLogger(this);

            using (sourceRepository.StartOperation(RepositoryOperationNames.Update))
            {
                foreach (var package in GetPackages(localRepository))
                {
                    if (localRepository.Exists(package.Id))
                    {
                        if (projectManager.IsInstalled(package))
                        {
                            Log(Level.Debug, "Found installed package {0} version {1}", package.Id, package.Version);
                            yield return new PackageDependency(package.Id, new VersionSpec(package.Version));
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
                    throw new BuildException(String.Format("Unable to find packages {0}", String.Join(", ", invalid)), Location);
                }

                packages = packages.Where(r => idSet.Contains(r.Id));
            }
            var packageSorter = new PackageSorter(targetFramework: null);
            return packageSorter.GetPackagesByDependencyOrder(new ReadOnlyPackageRepository(packages)).Reverse();
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
            throw new BuildException("Unable to locate packages folder", Location);
        }

        private static NugetUpdateTask.ProjectPair GetProject(string packagesConfigPath)
        {
            IMSBuildProjectSystem msBuildProjectSystem = null;
            msBuildProjectSystem = NugetUpdateTask.GetMSBuildProject(packagesConfigPath);
            return new NugetUpdateTask.ProjectPair
            {
                PackagesConfigPath = packagesConfigPath,
                Project = msBuildProjectSystem
            };
        }
    }
}
