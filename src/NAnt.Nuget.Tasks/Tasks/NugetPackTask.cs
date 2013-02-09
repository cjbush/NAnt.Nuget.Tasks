using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.NuGet.Tasks.Types;
using NAnt.NuGet.Tasks.Common;
using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NAnt.NuGet.Tasks.Validators;
using System.Runtime.Versioning;

namespace NAnt.NuGet.Tasks.Tasks
{
    [TaskName("nuget-pack")]
    public class NugetPackTask : Task
    {
        [TaskAttribute("id", Required = true), StringValidator(AllowEmpty = false)]
        public string Id { get; set; }

        [TaskAttribute("version", Required = true), SemanticVersionValidator]
        public string Version { get; set; }

        [TaskAttribute("title", Required = true), StringValidator(AllowEmpty = false)]
        public string Title { get; set; }

        [TaskAttribute("authors", Required = true), StringValidator(AllowEmpty = false)]
        public string Authors { get; set; }

        [TaskAttribute("owners", Required = true), StringValidator(AllowEmpty = false)]
        public string Owners { get; set; }

        [TaskAttribute("icon"), StringValidator(AllowEmpty = false)]
        public Uri Icon { get; set; }

        [TaskAttribute("url"), StringValidator(AllowEmpty = false)]
        public Uri Url { get; set; }

        [TaskAttribute("license"), StringValidator(AllowEmpty = false)]
        public Uri License { get; set; }

        [TaskAttribute("outdir", Required = true)]
        public DirectoryInfo OutDir { get; set; }

        [TaskAttribute("property"), StringValidator(AllowEmpty = false)]
        public string Property { get; set; }

        [TaskAttribute("description", Required = true), StringValidator(AllowEmpty = false)]
        public string Description { get; set; }

        [TaskAttribute("summary"), StringValidator(AllowEmpty = false)]
        public string Summary { get; set; }

        [TaskAttribute("copyright"), StringValidator(AllowEmpty = false)]
        public string Copyright { get; set; }

        [TaskAttribute("release-notes"), StringValidator(AllowEmpty = false)]
        public string ReleaseNotes { get; set; }

        [BuildElementArray("content", Required = true)]
        public NuGetContentSet[] ContentSets { get; set; }

        [BuildElementCollection("framework-assemblies", "assembly")]
        public FrameworkAssembly[] FrameworkAssemblies { get; set; }

        [BuildElementArray("dependencies")]
        public NuGetDependencies[] Dependencies { get; set; }


        protected override void ExecuteTask()
        {
            PackageBuilder pb = new PackageBuilder();
            pb.Id = Id;
            pb.Version = new SemanticVersion(Version);
            pb.Title = Title;
            pb.Description = Description;
            pb.Authors.AddRange(Authors.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
            pb.Owners.AddRange(Owners.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
            if (FrameworkAssemblies != null)
                foreach (var fa in FrameworkAssemblies)
                    pb.FrameworkReferences.Add(new FrameworkAssemblyReference(fa.FrameworkName));
            if (Dependencies != null)
            {
                var groups = new Dictionary<string, List<NuGetDependency>>();
                foreach (var dpg in Dependencies)
                {
                    string framework = dpg.TargetFramework;
                    if (String.IsNullOrWhiteSpace(framework))
                        framework = "";

                    List<NuGetDependency> list;
                    if (!groups.TryGetValue(framework, out list))
                        groups[framework] = list = new List<NuGetDependency>();

                    list.AddRange(dpg.Dependencies);
                }

                foreach (var g in groups)
                {
                    FrameworkName fn = g.Key == "" ? null : VersionUtility.ParseFrameworkName(g.Key);
                    PackageDependencySet ds = new PackageDependencySet(fn, g.Value.Select(v => (PackageDependency)v));
                    pb.DependencySets.Add(ds);
                }
            }
            if (Icon != null)
                pb.IconUrl = Icon;
            if (Url != null)
                pb.ProjectUrl = Url;
            if (License != null)
                pb.LicenseUrl = License;
            if (Summary != null)
                pb.Summary = Summary;
            if (Copyright != null)
                pb.Copyright = Copyright;
            if (ReleaseNotes != null)
                pb.ReleaseNotes = ReleaseNotes;

            List<ManifestFile> files = new List<ManifestFile>();
            foreach (var contentSet in ContentSets)
            {
                var target = contentSet.GetTarget();
                foreach (var file in contentSet.FileNames)
                {
                    ManifestFile mf = new ManifestFile();
                    mf.Source = file;
                    mf.Target = Path.Combine(target, Path.GetFileName(file));
                    Log(Level.Verbose, "Added file '{0}' -> '{1}'", file, mf.Target);
                    files.Add(mf);
                }
            }
            pb.PopulateFiles(Project.BaseDirectory, files);

            string savePath = Path.Combine(OutDir.FullName, pb.Id + "-" + pb.Version.ToString() + ".nupkg");
            var saveDir = Directory.GetParent(savePath);
            if (!saveDir.Exists)
                saveDir.Create();
            using (FileStream fs = File.Open(savePath, FileMode.Create, FileAccess.ReadWrite))
                pb.Save(fs);

            Log(Level.Info, "Package created at '{0}'", savePath);
            if (!String.IsNullOrWhiteSpace(Property))
                Properties.Add(Property, savePath);
        }
    }
}
