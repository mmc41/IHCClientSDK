using System.Threading.Tasks;
using System;
using System.Linq;
using System.Text;
using System.Net;

namespace Ihc
{
    internal class NetworkHelper
    {
        /**
        * Convert 32-bit integer to IP address string.
        * IP addresses are stored in network byte order (big-endian).
        *
        * @param ipInt IP address as 32-bit integer
        * @return IP address string (e.g., "192.168.1.1")
        */
        public static string ConvertIntToIPAddress(int ipInt)
        {
            byte[] bytes = BitConverter.GetBytes(ipInt);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return new IPAddress(bytes).ToString();
        }

        /**
        * Convert IP address string to 32-bit integer.
        * IP addresses are stored in network byte order (big-endian).
        *
        * @param ipString IP address string (e.g., "192.168.1.1")
        * @return IP address as 32-bit integer
        */
        public static int ConvertIPAddressToInt(string ipString)
        {
            var ipAddress = IPAddress.Parse(ipString);
            byte[] bytes = ipAddress.GetAddressBytes();
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToInt32(bytes, 0);
        }
    }
}