using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.Core.Types;
using NAnt.NuGet.Tasks.Types;
using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NAnt.NuGet.Tasks.Tasks
{
    [TaskName("nuget-publish")]
    public class NugetPublishTask : Task
    {
        [BuildElement("packages", Required = true)]
        public FileSet Packages { get; set; }

        [BuildElement("feeds", Required = true)]
        public NuGetFeedSet Feeds { get; set; }

        protected override void ExecuteTask()
        {
            var feeds = Feeds.GetPushLocations().ToArray();
            var files = Packages.FileNames.Cast<string>().ToArray();

            foreach (var feed in feeds)
            {
                Log(Level.Info, "Publishing to {0}", feed.Name);
                foreach (var file in files)
                {
                    Log(Level.Info, "  * {0}", Path.GetFileName(file));
                    try
                    {
                        feed.Push(file);
                    }
                    catch (Exception e)
                    {
                        throw new BuildException("Failed to publish package.", e);
                    }
                }
            }
        }
    }
}
