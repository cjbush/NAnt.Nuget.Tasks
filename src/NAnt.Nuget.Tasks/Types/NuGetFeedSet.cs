using NAnt.Core;
using NAnt.Core.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet;

using IOPath = System.IO.Path;
using NAnt.NuGet.Tasks.Validators;
using NAnt.NuGet.Tasks.Common;

namespace NAnt.NuGet.Tasks.Types
{
    public class NuGetFeedSet : Element
    {
        [BuildElementArray("local-feed")]
        public NuGetLocalFeed[] LocalFeeds { get; set; }

        [BuildElementArray("server-feed")]
        public NuGetServerFeed[] ServerFeeds { get; set; }

        internal IEnumerable<IPackagePushLocation> GetPushLocations()
        {
            if (LocalFeeds != null)
                foreach (var f in LocalFeeds)
                    yield return f.GetPushLocation();

            if (ServerFeeds != null)
                foreach (var f in ServerFeeds)
                    yield return f.GetPushLocation();
        }
    }

    public abstract class NuGetFeed : Element
    {
        internal protected abstract IPackagePushLocation GetPushLocation();
    }

    public class NuGetLocalFeed : NuGetFeed
    {
        [TaskAttribute("path", Required = true)]
        public DirectoryInfo Path { get; set; }

        protected internal override IPackagePushLocation GetPushLocation()
        {
            var ret = new LocalPackageRepository(Path.FullName);
            ret.PathResolver = new LocalPathResolver(Path);
            return new LocalPushLocation(ret);
        }

        class LocalPushLocation : IPackagePushLocation
        {
            readonly LocalPackageRepository _repo;
            public LocalPushLocation(LocalPackageRepository repo)
            {
                _repo = repo;
            }

            public void Push(string zipPackagePath)
            {
                _repo.AddPackage(new ZipPackage(zipPackagePath));
            }

            public string Name
            {
                get { return _repo.Source; }
            }
        }

        class LocalPathResolver : IPackagePathResolver
        {
            readonly DirectoryInfo _path;

            public LocalPathResolver(DirectoryInfo path)
            {
                _path = path;
            }

            public string GetInstallPath(IPackage package)
            {
                throw new NotImplementedException();
            }

            public string GetPackageDirectory(string packageId, SemanticVersion version)
            {
                return IOPath.Combine(_path.FullName, packageId);
            }

            public string GetPackageDirectory(IPackage package)
            {
                return GetPackageDirectory(package.Id, package.Version);
            }

            public string GetPackageFileName(string packageId, SemanticVersion version)
            {
                return packageId + "." + version.ToString() + ".nupkg";
            }

            public string GetPackageFileName(IPackage package)
            {
                return GetPackageFileName(package.Id, package.Version);
            }
        }
    }

    public class NuGetServerFeed : NuGetFeed
    {
        [TaskAttribute("server", Required = false), UriValidator(Absolute = true)]
        public Uri ServerPath { get; set; }

        [TaskAttribute("apikey-file")]
        public FileInfo ApiKeyFile { get; set; }

        [TaskAttribute("apikey"), StringValidator(AllowEmpty = false)]
        public string ApiKey { get; set; }

        protected internal override IPackagePushLocation GetPushLocation()
        {
            if (ServerPath == null)
                ServerPath = new Uri("http://nuget.org");
            PackageServer server = new PackageServer(ServerPath.ToString(), "NAnt.NuGet");
            string apiKey = ApiKey;
            if (apiKey == null && ApiKeyFile != null)
            {
                if (!ApiKeyFile.Exists)
                    throw new BuildException(String.Format("Api key-file '{0}' does not exist.", ApiKeyFile.FullName));
                apiKey = File.ReadAllText(ApiKeyFile.FullName);
            }

            return new ServerPushLocation(apiKey, server);
        }

        class ServerPushLocation : IPackagePushLocation
        {
            readonly string _apiKey;
            readonly PackageServer _server;

            public ServerPushLocation(string apiKey, PackageServer server)
            {
                _apiKey = apiKey;
                _server = server;
            }

            public void Push(string zipPackagePath)
            {
                _server.PushPackage(_apiKey, () => File.OpenRead(zipPackagePath), 0);
            }

            public string Name
            {
                get { return _server.Source; }
            }
        }
    }
}
