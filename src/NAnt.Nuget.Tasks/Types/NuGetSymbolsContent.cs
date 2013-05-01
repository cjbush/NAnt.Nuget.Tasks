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
    public class NuGetSymbolsContent : DataTypeBase
    {
        [BuildElementArray("sources", Required = true)]
        public NuGetContentSet[] ContentSets { get; set; }

        [TaskAttribute("property"), StringValidator(AllowEmpty = false)]
        public string Property { get; set; }
    }
}
