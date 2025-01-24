using System.Net;

namespace Devmachinist.Constellations
{
    public partial class Constellation
    {
        /// <summary>
        /// Looks up an IP address to check if it has a hostname.
        /// </summary>
        /// <param name="ipAddress">The IP address to run a hostname lookup</param>
        /// <returns>A string of an ip address or hostname</returns>
        private string GetDomainNameFromIp(string ipAddress)
        {
            try
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(ipAddress);
                if (hostEntry != null)
                {
                    return hostEntry.HostName;
                }
                else
                {
                    return ipAddress;
                }
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                return ipAddress;
            }
            catch (System.Exception ex)
            {
                return ipAddress;
            }
        }
        private string GetWebSocketOrigin(string request)
        {
            foreach (string line in request.Split(new[] { "\r\n" }, StringSplitOptions.None))
            {
                if (line.StartsWith("Origin:", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Split(':')[1].Trim();
                }
            }

            return "*";
        }
    }
}
