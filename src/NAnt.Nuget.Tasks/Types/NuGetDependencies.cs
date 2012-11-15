using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.NuGet.Tasks.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAnt.NuGet.Tasks.Types
{
    public class NuGetDependencies : Element
    {
        [TaskAttribute("framework")]
        public string TargetFramework { get; set; }

        [BuildElementArray("dependency", Required = true)]
        public NuGetDependency[] Dependencies { get; set; }
    }

    public class NuGetDependency : Element
    {
        [TaskAttribute("id", Required = true), StringValidator(AllowEmpty = false)]
        public string Id { get; set; }

        [TaskAttribute("version", Required = false), SemanticVersionValidator]
        public string Version { get; set; }
    }
}
