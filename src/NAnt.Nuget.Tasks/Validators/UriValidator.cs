using NAnt.Core;
using NAnt.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAnt.NuGet.Tasks.Validators
{
    public class UriValidatorAttribute : ValidatorAttribute
    {
        public bool Absolute { get; set; }

        public override void Validate(object value)
        {
            string valueString;

            try
            {
                valueString = Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new ValidationException(string.Format(CultureInfo.InvariantCulture,
                    "Invalid value: ", value.ToString()), ex);
            }

            if (String.IsNullOrWhiteSpace(valueString))
                throw new ValidationException("String is empty");

            UriKind kind = UriKind.RelativeOrAbsolute;
            if (Absolute)
                kind = UriKind.Absolute;
            if (!Uri.IsWellFormedUriString(valueString, kind))
                throw new ValidationException("Invalid value");
        }
    }
}
