using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ihc
{
    /// <summary>
    /// Helper class for security-related operations such as password redaction.
    /// </summary>
    internal static class SecurityHelper
    {
        /// <summary>
        /// Redacts password values from XML content in SOAP request/response logging.
        /// </summary>
        /// <param name="input">The XML string to redact passwords from</param>
        /// <returns>The XML string with passwords redacted, or null if input is null</returns>
        public static string RedactPassword(string input)
        {
            if (input == null)
            {
                return null;
            }

            // Redact password content in XML elements like <ns1:password>xxx</ns1:password>
            // where ns1 can be any namespace prefix (or no prefix at all)
            // Pattern explanation:
            // - (<\w*:?password) - captures opening tag start with optional namespace prefix (e.g., <password, <ns1:password, <utcs:password)
            // - (?:\s+[^>]*)? - optionally matches attributes (non-capturing group): whitespace followed by any characters except '>'
            // - (>) - captures the closing bracket of the opening tag
            // - [^<]+ - matches the password content (one or more characters except '<')
            // - (</\w*:?password>) - captures closing tag with optional namespace prefix
            string pattern = @"(<\w*:?password(?:\s+[^>]*)?)(>)[^<]+(</\w*:?password>)";
            string replacement = "$1$2" + UserConstants.REDACTED_PASSWORD + "$3";

            return Regex.Replace(input, pattern, replacement, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Redacts sessionId from HTTP headers.
        /// </summary>
        public static IEnumerable<string> RedactSessionId(string key, IEnumerable<string> input)
        {
            if (key == "Set-Cookie")
                return [ CookieHandler.REDACTED_COOKIE ];
            else return input;
        }
    }
}
