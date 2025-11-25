using System;

namespace Visor.Core
{
    public static class VisorConvert
    {
        public static T? Unbox<T>(object value)
        {
            if (value is null or DBNull)
                return default;

            // Fast path: direct match
            if (value is T tVal)
                return tVal;

            var targetType = typeof(T);
            
            // Unwrap Nullable<T>
            if (Nullable.GetUnderlyingType(targetType) is { } underlying)
                targetType = underlying;

            // Special handling for CHAR (SQL drivers often return string for CHAR(1))
            if (targetType == typeof(char))
            {
                var text = value.ToString();
                
                if (string.IsNullOrEmpty(text)) 
                    return default;
                if (text!.Length == 1) 
                    return (T)(object)text[0];
                
                // Detailed error for the InnerException
                throw new ArgumentException($"Visor conversion error: Expected char, got string of length {text.Length}: '{text}'");
            }

            // Fallback to Convert.ChangeType
            try 
            {
                return (T?)Convert.ChangeType(value, targetType);
            }
            catch (Exception ex)
            {
                // This will be the InnerException of VisorMappingException
                throw new InvalidCastException($"Visor conversion error: Cannot cast value '{value}' (type: {value.GetType().Name}) to target type '{typeof(T).Name}'.", ex);
            }
        }
    }
}