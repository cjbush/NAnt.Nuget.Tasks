using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAnt.NuGet.Tasks.Common
{
    public interface IPackagePushLocation
    {
        void Push(string zipPackagePath);
        string Name { get; }
    }
}
