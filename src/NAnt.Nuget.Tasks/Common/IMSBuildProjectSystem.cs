using NuGet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAnt.NuGet.Tasks.Common
{
    interface IMSBuildProjectSystem : IProjectSystem
    {
        void Save();
    }
}
