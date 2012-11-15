using NuGet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAnt.NuGet.Tasks.Common
{
    static class AggregateRepositoryHelper
    {
        public static AggregateRepository CreateAggregateRepositoryFromSources(IPackageRepositoryFactory factory, IPackageSourceProvider sourceProvider, IEnumerable<string> sources)
        {
            AggregateRepository repository;
            if (sources != null && sources.Any())
            {
                var repositories = sources.Select(s => sourceProvider.ResolveSource(s))
                                             .Select(factory.CreateRepository)
                                             .ToList();
                repository = new AggregateRepository(repositories);
            }
            else
            {
                repository = sourceProvider.GetAggregate(factory, ignoreFailingRepositories: true);
            }

            return repository;
        }
    }
}
