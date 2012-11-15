using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.Core.Util;
using NuGet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NAnt.NuGet.Tasks.Validators
{
    public class SemanticVersionValidatorAttribute : ValidatorAttribute
    {
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

            SemanticVersion v;
            if (!SemanticVersion.TryParse(valueString, out v))
                throw new ValidationException("Invalid value");
        }
    }
}
