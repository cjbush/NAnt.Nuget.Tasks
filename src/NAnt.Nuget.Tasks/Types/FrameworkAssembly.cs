using NAnt.Core;
using NAnt.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAnt.NuGet.Tasks.Types
{
    [Serializable]
    [ElementName("nuget-framework-assembly")]
    public class FrameworkAssembly : Element
    {
        [TaskAttribute("name", Required = true), StringValidator(AllowEmpty = false)]
        public string FrameworkName { get; set; }
    }
}
