using NAnt.Core.Attributes;
using NAnt.Core.Types;
using NAnt.NuGet.Tasks.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAnt.NuGet.Tasks.Types
{
    [Serializable]
    [ElementName("nuget-contentset")]
    public class NuGetContentSet : FileSet
    {
        #region Private Instance Fields

        ContentTarget _target;
        string _frameworkName;

        #endregion Private Instance Fields

        #region Private Static Fields

        #endregion Private Static Fields

        #region Public Instance Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSet" /> class.
        /// </summary>
        public NuGetContentSet()
        {
            
        }
        
        /// <summary>
        /// copy constructor
        /// </summary>
        /// <param name="fs"></param>
        public NuGetContentSet(NuGetContentSet fs)
            : this()
        {
            fs.CopyTo((NuGetContentSet)this);
        }

        #endregion Public Instance Constructors

        #region Public Instance Properties

        [TaskAttribute("type", Required = true)]
        public ContentTarget Type
        {
            get { return _target; }
            set { _target = value; }
        }

        [TaskAttribute("framework", Required = true), StringValidator(AllowEmpty = false)]
        public string FrameworkName
        {
            get { return _frameworkName; }
            set { _frameworkName = value; }
        }

        #endregion

        public override object Clone()
        {
            NuGetContentSet clone = new NuGetContentSet();
            CopyTo(clone);
            return clone;
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (DefaultExcludes) {
                // add default exclude patterns
                Excludes.Add("**/*.nupkg");
                Excludes.Add("**/*.nuspec");
            }
        }

        #region Protected Instance Methods

        /// <summary>
        /// Copies all instance data of the <see cref="FileSet" /> to a given
        /// <see cref="FileSet" />.
        /// </summary>
        protected void CopyTo(NuGetContentSet clone)
        {
            base.CopyTo(clone);
        }

        #endregion Protected Instance Methods
    }
}
