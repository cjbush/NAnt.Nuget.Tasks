using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.Core.Types;
using NAnt.NuGet.Tasks.Common;
using NAnt.NuGet.Tasks.Types;
using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace NAnt.NuGet.Tasks.Tasks {
	[TaskName("nuget-get-files")]
	public class NugetGetFilesTask : Task {
		readonly List<string> _sources = new List<string>();
		readonly List<string> _ids = new List<string>();
		IFileSystem _fileSystem;

		public IPackageRepositoryFactory RepositoryFactory { get; private set; }

		public IPackageSourceProvider SourceProvider { get; private set; }

		[TaskAttribute("solution-dir", Required = true)]
		public DirectoryInfo SolutionDir { get; set; }

		[TaskAttribute("repository", Required = false)]
		public DirectoryInfo RepositoryPath { get; set; }

		[TaskAttribute("feed", Required = false)]
		public string Feed { get; set; }

		[TaskAttribute("files-property"), StringValidator(AllowEmpty = false)]
		public string FilesId { get; set; }

		[TaskAttribute("references-property"), StringValidator(AllowEmpty = false)]
		public string ReferencesId { get; set; }

		[BuildElementArray("dependencies", Required = true)]
		public NuGetDependencies[] Dependencies { get; set; }

		[TaskAttribute("framework", Required = true), StringValidator(AllowEmpty = false)]
		public string Framework { get; set; }

		private ISettings DefaultSettings {
			get { return Settings.LoadDefaultSettings(_fileSystem); }
		}

		private string AbsoluteRepositoryPath {
			get { return RepositoryPath != null ? RepositoryPath.FullName : null; }
		}

		public ICollection<string> Source {
			get { return _sources; }
		}

		public ICollection<string> Id {
			get { return _ids; }
		}

		protected override void ExecuteTask() {
			string dir = SolutionDir.FullName;
			string repositoryPath = GetRepositoryPathFromSolution(dir);

			if (Feed == null) {
				Feed = "https://www.nuget.org/api/v2/";
			}

			_fileSystem = new PhysicalFileSystem(dir);
			RepositoryFactory = new PackageRepositoryFactory();
			SourceProvider = new PackageSourceProvider(new Settings(_fileSystem));
			var repo = RepositoryFactory.CreateRepository(Feed);

			Log(Level.Debug, "Repo: {0}, Count: {1}", repo.Source, repo.GetPackages().Count());

			var fw = VersionUtility.ParseFrameworkName(Framework);
			List<string> files = new List<string>(), references = new List<string>();
			foreach (var deps in Dependencies) {
				foreach (var dep in deps.Dependencies) {
					var package = repo.FindPackage(dep.Id, dep.VersionSpec, !String.IsNullOrWhiteSpace(dep.VersionSpec.MinVersion.SpecialVersion), false);
					if (package == null)
						package = repo.FindPackage(dep.Id, dep.VersionSpec, true, false);
					if (package == null)
						package = repo.FindPackage(dep.Id, dep.VersionSpec, true, true);
					if (package == null)
						throw new BuildException(String.Format("Can't find package {0} with min version {1}", dep.Id, dep.MinVersion), Location);

					string pkgPath = Path.Combine(repositoryPath, package.Id + "." + package.Version);

					var package_files = package.GetLibFiles().ToList();
					IEnumerable<IPackageFile> compatible_files;
					Log(Level.Debug, "Found package {0} with {1} file(s) - {2}", package.Id, package_files.Count, package.GetType());
					if (!VersionUtility.TryGetCompatibleItems(fw, package_files, out compatible_files))
						throw new BuildException("Couldn't get compatible files.");

					foreach (var f in compatible_files) {
						var extension = Path.GetExtension(f.Path);

						var path = Path.Combine(pkgPath, f.Path);

						if (File.Exists(path)) break;

						if (!Directory.Exists(Path.GetDirectoryName(path))) {
							Directory.CreateDirectory(Path.GetDirectoryName(path));
						}

						using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)) {
							using (var stream = f.GetStream()) {
								stream.CopyTo(fileStream);
								fileStream.Flush();
								fileStream.Dispose();
							}
						}

						Log(Level.Debug, "  - Found compatible file {1} ({0}) - {2}", f.Path, f.EffectivePath, path);
						if (extension == ".dll" || extension == ".exe")
							references.Add(path);
						files.Add(path);
					}
				}
			}

			if (FilesId != null) {
				PatternSet ps = new PatternSet();
				ps.Include.AddRange(files.Select(MakePattern).ToArray());
				Project.DataTypeReferences.Add(FilesId, ps);
			}
			if (ReferencesId != null) {
				PatternSet ps = new PatternSet();
				ps.Include.AddRange(references.Select(MakePattern).ToArray());
				Project.DataTypeReferences.Add(ReferencesId, ps);
			}

			Log(Level.Info, "Found {0} file(s) and {1} reference(s)", files.Count, references.Count);
		}

		private static Pattern MakePattern(string path) {
			Pattern ret = new Pattern();
			ret.PatternName = path;
			return ret;
		}

		class PackagePathResolver : IPackagePathResolver {
			NugetGetFilesTask _task;
			public PackagePathResolver(NugetGetFilesTask task) {
				_task = task;
			}

			public string GetInstallPath(IPackage package) {
				throw new NotImplementedException();
			}

			public string GetPackageDirectory(string packageId, SemanticVersion version) {
				throw new NotImplementedException();
			}

			public string GetPackageDirectory(IPackage package) {
				throw new NotImplementedException();
			}

			public string GetPackageFileName(string packageId, SemanticVersion version) {
				throw new NotImplementedException();
			}

			public string GetPackageFileName(IPackage package) {
				throw new NotImplementedException();
			}
		}

		private string GetRepositoryPathFromSolution(string solutionDir) {
			string packagesDir = AbsoluteRepositoryPath;

			if (String.IsNullOrEmpty(packagesDir)) {
				string[] parts = solutionDir.Split('\\');
				string path = "";
				for (int i = 0; i < parts.Length; i++) {
					path += string.Format("{0}/", parts[i]);
					if (File.Exists(string.Format("{0}nuget.config", path))) {
						XDocument doc = XDocument.Load(string.Format("{0}nuget.config", path));
						packagesDir = string.Format("{0}{1}/", path, doc.Descendants("add").Where(e => e.Attribute("key").Value.Equals("repositorypath")).First().Attribute("value").Value);
						break;
					}
				}
				if (String.IsNullOrEmpty(packagesDir) && !String.IsNullOrEmpty(solutionDir)) {
					packagesDir = Path.Combine(solutionDir, NuGetConstants.PackagesDirectoryName);
				}
			}

			return GetPackagesDir(packagesDir);
		}

		private string GetPackagesDir(string packagesDir) {
			if (!String.IsNullOrEmpty(packagesDir)) {
				// Get the full path to the packages directory
				packagesDir = Path.GetFullPath(packagesDir);

				if (!Directory.Exists(packagesDir)) {
					Directory.CreateDirectory(packagesDir);
				}

				// REVIEW: Do we need to check for existence?
				if (Directory.Exists(packagesDir)) {
					string currentDirectory = Directory.GetCurrentDirectory();
					string relativePath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(currentDirectory), packagesDir);
					//Console.WriteLine(NuGetResources.LookingForInstalledPackages, relativePath);
					return packagesDir;
				}
			}

			//throw new CommandLineException(NuGetResources.UnableToLocatePackagesFolder);
			throw new BuildException("Unable to locate packages folder", Location);
		}
	}
}
