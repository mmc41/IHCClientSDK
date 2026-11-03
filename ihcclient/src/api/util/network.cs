using System.Threading.Tasks;
using System;
using System.Linq;
using System.Text;
using System.Net;

namespace Ihc
{
    internal class NetworkHelper
    {
        /// <summary>
        /// Convert 32-bit integer to IP address string.
        /// IP addresses are stored in network byte order (big-endian).
        /// </summary>
        /// <param name="ipInt">IP address as 32-bit integer.</param>
        /// <returns>IP address string (e.g., "192.168.1.1").</returns>
        public static string ConvertIntToIPAddress(int ipInt)
        {
            byte[] bytes = BitConverter.GetBytes(ipInt);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return new IPAddress(bytes).ToString();
        }

        /// <summary>
        /// Convert dotted-decimal IPv4 text to the 32-bit integer the wire carries, in network byte order
        /// (big-endian).
        /// </summary>
        /// <param name="ipString">Four dot-separated groups of one to three ASCII digits, each 0-255 (e.g.
        /// "192.168.1.1"); a leading zero is a decimal digit, so "010" is ten.</param>
        /// <returns>IP address as 32-bit integer.</returns>
        /// <exception cref="ArgumentException">The text is not of that form; the message names it.</exception>
        /// <remarks>
        /// <see cref="IPAddress.Parse(string)"/> is deliberately not used: it also accepts the legacy BSD forms, reading a
        /// leading zero as octal ("010" as 8), widening a short address ("10.1" as 10.0.0.1) and taking hexadecimal or
        /// one 32-bit number, and it accepts IPv6, which has no 32-bit form. Each would send a server nobody typed.
        /// IHC Administrator reads every group as decimal.
        /// </remarks>
        public static int ConvertIPAddressToInt(string ipString)
        {
            ArgumentNullException.ThrowIfNull(ipString);
            string[] groups = ipString.Split('.');
            if (groups.Length != 4)
                throw NotDottedDecimal(ipString);

            uint packed = 0;
            foreach (string group in groups)
            {
                // NumberStyles.None admits ASCII digits only, no sign or white space; byte caps the group at 255.
                if (group.Length > 3 || !byte.TryParse(group, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out byte value))
                    throw NotDottedDecimal(ipString);
                packed = (packed << 8) | value;
            }
            return unchecked((int)packed);
        }

        private static ArgumentException NotDottedDecimal(string ipString) =>
            new ArgumentException(
                $"'{ipString}' is no dotted-decimal IPv4 address: four groups of one to three digits 0-9, each 0-255, separated by dots.",
                nameof(ipString));
    }
}