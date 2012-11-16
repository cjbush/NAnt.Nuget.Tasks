using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.NuGet.Tasks.Validators;
using NuGet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAnt.NuGet.Tasks.Types
{
    [Serializable]
    [ElementName("nuget-dependencies")]
    public class NuGetDependencies : DataTypeBase
    {
        [TaskAttribute("framework")]
        public string TargetFramework { get; set; }

        [BuildElementArray("dependency", Required = true)]
        public NuGetDependency[] Dependencies { get; set; }
    }

    [Serializable]
    public class NuGetDependency : Element
    {
        [TaskAttribute("id", Required = true), StringValidator(AllowEmpty = false)]
        public string Id { get; set; }

        [TaskAttribute("version", Required = false), SemanticVersionValidator]
        public string Version { get; set; }

        [TaskAttribute("min-version", Required = false), SemanticVersionValidator]
        public string MinVersion { get; set; }

        [TaskAttribute("max-version", Required = false), SemanticVersionValidator]
        public string MaxVersion { get; set; }

        public VersionSpec VersionSpec
        {
            get
            {
                if (String.IsNullOrWhiteSpace(Version + MinVersion + MaxVersion))
                    return null;

                VersionSpec spec = new VersionSpec();
                spec.IsMinInclusive = spec.IsMaxInclusive = true;
                if (!String.IsNullOrWhiteSpace(MinVersion))
                    spec.MinVersion = SemanticVersion.Parse(MinVersion);
                if (!String.IsNullOrWhiteSpace(MaxVersion))
                    spec.MaxVersion = SemanticVersion.Parse(MaxVersion);
                if (!String.IsNullOrWhiteSpace(Version))
                    spec.MinVersion = spec.MaxVersion = SemanticVersion.Parse(Version);

                return spec;
            }
        }

        public static implicit operator PackageDependency(NuGetDependency dep)
        {
            return new PackageDependency(dep.Id, dep.VersionSpec);
        }

        public static implicit operator NuGetDependency(PackageDependency dep)
        {
            var ret = new NuGetDependency
            {
                Id = dep.Id
            };
            if (dep.VersionSpec != null)
            {
                ret.MinVersion = dep.VersionSpec.MinVersion.ToString();
                ret.MaxVersion = dep.VersionSpec.MaxVersion.ToString();
            }
            return ret;
        }
    }
}
