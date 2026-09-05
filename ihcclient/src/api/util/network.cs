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
        /// Convert IP address string to 32-bit integer.
        /// IP addresses are stored in network byte order (big-endian).
        /// </summary>
        /// <param name="ipString">IP address string (e.g., "192.168.1.1").</param>
        /// <returns>IP address as 32-bit integer.</returns>
        /// <exception cref="ArgumentException">The address is not IPv4, and so has no 32-bit form.</exception>
        public static int ConvertIPAddressToInt(string ipString)
        {
            var ipAddress = IPAddress.Parse(ipString);
            // The wire field is 32 bits, so anything wider has no representation in it. Parse accepts an
            // IPv6 literal and hands back sixteen bytes, of which the conversion below would read four -
            // yielding an address nobody submitted. IPv4-mapped IPv6 is refused with the rest: its first
            // four bytes are zeroes, so it narrows just as wrongly as any other.
            if (ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                throw new ArgumentException($"Only IPv4 addresses can be written as a 32-bit address; '{ipString}' is {ipAddress.AddressFamily}.", nameof(ipString));

            byte[] bytes = ipAddress.GetAddressBytes();
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToInt32(bytes, 0);
        }
    }
}