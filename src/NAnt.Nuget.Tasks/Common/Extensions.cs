using NAnt.NuGet.Tasks.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAnt.NuGet.Tasks.Common
{
    public static class Extensions
    {
        public static string GetTarget(this NuGetContentSet contentSet)
        {
            string frameWorkName = contentSet.FrameworkName;
            if (!String.IsNullOrWhiteSpace(frameWorkName))
                return Path.Combine(contentSet.Type.GetTarget(), frameWorkName);
            return contentSet.Type.GetTarget();
        }

        public static string GetTarget(this ContentTarget target)
        {
            switch (target)
            {
                case ContentTarget.Content: return "content";
                case ContentTarget.Lib: return "lib";
                case ContentTarget.Src: return "content";
                case ContentTarget.Tools: return "tools";
                default: throw new ArgumentException();
            }
        }
    }
}
