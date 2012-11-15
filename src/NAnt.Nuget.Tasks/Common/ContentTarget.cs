using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAnt.NuGet.Tasks.Common
{
    [TypeConverter(typeof(ContentTargetConverter))]
    public enum ContentTarget
    {
        Content,
        Lib,
        Src,
        Tools
    }

    public class ContentTargetConverter : EnumConverter
    {
        public ContentTargetConverter()
            : base(typeof(ContentTarget))
        {
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            string val = value as string;
            if (val != null)
                return base.ConvertFrom(context, culture, val.Substring(0, 1).ToLower() + val.Substring(1));
            return base.ConvertFrom(context, culture, value);
        }
    }
}
