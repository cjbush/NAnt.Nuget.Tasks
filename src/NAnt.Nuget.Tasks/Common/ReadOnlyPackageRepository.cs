using NuGet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAnt.NuGet.Tasks.Common
{
    class ReadOnlyPackageRepository : PackageRepositoryBase
    {
        private readonly IEnumerable<IPackage> _packages;
        public ReadOnlyPackageRepository(IEnumerable<IPackage> packages)
        {
            _packages = packages;
        }

        public override string Source
        {
            get { return null; }
        }

        public override bool SupportsPrereleasePackages
        {
            get { return true; }
        }

        public override IQueryable<IPackage> GetPackages()
        {
            return _packages.AsQueryable();
        }
    }
}
