using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Ihc
{
    /// <summary>
    /// Helper class for validating data annotation attributes on model objects.
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// Validates an object against its data annotation attributes.
        /// </summary>
        /// <param name="obj">Object to validate</param>
        /// <param name="parameterName">Parameter name for exception message</param>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        public static void ValidateDataAnnotations(object obj, string parameterName)
        {
            if (obj == null)
            {
                throw new ArgumentException(message: "Parameter must be provided", paramName: parameterName);
            }

            var validationContext = new ValidationContext(obj);
            var validationResults = new List<ValidationResult>();

            if (!Validator.TryValidateObject(obj, validationContext, validationResults, validateAllProperties: true))
            {
                var errorMessages = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
                throw new ArgumentException(message: $"Validation failed: {errorMessages}", paramName: parameterName);
            }
        }
    }
}
