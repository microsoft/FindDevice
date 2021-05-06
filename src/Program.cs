using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Makaretu.Dns;

namespace finddevice
{
    class Program
    {
        /// <summary>
        /// Discover devices connected to your local network or link-local devices, such as UsbNCM.
        /// By default looks for devices running Factory Orchestrator with network access enabled.
        /// </summary>
        /// <param name="linkLocalOnly">Only look for link-local devices, such as UsbNCM devices Default: false</param>
        /// <param name="displayHostname">Display the device hostname Default: true</param>
        /// <param name="displayIPv4">Display the device IPv4 address Default: true</param>
        /// <param name="displayIPv6">Display the device IPv6 address Default: false</param>
        /// <param name="timeout">The amount of time in milliseconds to wait for responses (use greater than 2000 ms for WiFi). Default: Infinite</param>
        /// <param name="queryInterval">The amount of time in milliseconds to wait between queries. Default: 1000ms</param>
        /// <param name="service">The DNS-SD service string used for discovery Default: _factorch._tcp.local</param>
        ///                       
        static async Task Main(bool linkLocalOnly = false,
            bool displayHostname = true,
            bool displayIPv4 = true,
            bool displayIPv6 = false,
            int timeout = -1,
            int queryInterval = 1000,
            string service = "_factorch._tcp.local"
            )
        {
            if (!displayHostname && !displayIPv4 && !displayIPv6)
            {
                Console.Error.WriteLine("Error: at least one display method must be used!");
            }

            if ((queryInterval < 500))
            {
                Console.WriteLine("Warning: a query interval below 500ms may result in 'erroneous' data, as ip address can change on first enumeration.");
            }

            if (linkLocalOnly)
            {
                Console.WriteLine($"Looking for {service} devices connected via link-local network interfaces such as USBNCM...");
            }
            else
            {
                Console.WriteLine($"Looking for {service} devices connected to any network interface...");
            }
            Console.WriteLine();

            // Setup global variables
            TimeSinceReCheck = 0;
            QueryInterval = queryInterval;
            ServiceToQuery = service;
            LinkLocalOnly = linkLocalOnly;
            DisplayHostname = displayHostname;
            DisplayIPv4 = displayIPv4;
            DisplayIPv6 = displayIPv6;
            DeviceList = new ConcurrentDictionary<string, (AAAARecord ipv6, bool ActivelySeen)>();
            PrintQueue = new ConcurrentQueue<string>();
            MDns = new MulticastService();

            // Send the first DNS-SD query
            MDns.AnswerReceived += MDns_AnswerReceived;
            MDns.Start();
            MDns.SendQuery(ServiceToQuery);

            // Set a timer to continuously query for new devices
            // Also set the interval that we wish to recheck for devices
            ReCheckInterval = Math.Max(2000, queryInterval * 2);
            QueryTimer = new System.Timers.Timer(QueryInterval);
            QueryTimer.Elapsed += FindDeviceTimer_Elapsed;
            QueryTimer.Start();
            PrintTimer = new System.Timers.Timer(200);
            PrintTimer.Elapsed += PrintTimer_Elapsed;
            PrintTimer.Start();

            if (timeout == -1)
            {
                Console.WriteLine("Press any key to exit.");
                Console.WriteLine();
                Console.ReadLine();
            }
            else
            {
                await Task.Delay(timeout);
            }

            MDns.Stop();
            QueryTimer.Stop();

            Console.WriteLine($"Done looking for devices, exiting.");
        }

        /// <summary>
        /// A timer to print output in sequental order
        /// </summary>
        private static void PrintTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (PrintLock)
            {
                string output;
                while (PrintQueue.TryDequeue(out output))
                {
                    Console.WriteLine(output);
                }
            }
        }

        /// <summary>
        /// Called when a DNS reply is received.
        /// </summary>
        /// <param name="sender">The MulticastService that sent the query.</param>
        /// <param name="e">The device that replied.</param>
        private static void MDns_AnswerReceived(object sender, MessageEventArgs e)
        {
            // Extract the DNS-SD service that replied, and the device's IP addresses
            // Replies might be in Answers on AdditionalRecords so check both
            var srvRecords = e.Message.AdditionalRecords.Union(e.Message.Answers).OfType<SRVRecord>().Distinct();
            var ipv4s = e.Message.AdditionalRecords.Union(e.Message.Answers).OfType<ARecord>();
            var ipv6s = e.Message.AdditionalRecords.Union(e.Message.Answers).OfType<AAAARecord>();

            // Ensure the service we found matches the service we are looking for.
            // Also ensure it has an ipv6 address (everything should!)
            if (ipv6s.Count() == 0 || srvRecords.Count() == 0 || !srvRecords.Any(x => x.CanonicalName.EndsWith(ServiceToQuery)) || (ipv6s.All(x => !x.Address.IsIPv6LinkLocal) && LinkLocalOnly))
            {
                return;
            }
            // Build device information string
            string output = "";
            bool shouldAdd = true;

            // Only show link-local IPs if requested
            if (DisplayHostname)
            {
                var host = ipv4s.Select(x => x.CanonicalName).FirstOrDefault();

                if (host != null)
                {
                    output += host + " ";
                }
                else
                {
                    shouldAdd = false;
                }
            }
            if (DisplayIPv4)
            {
                if (ipv4s.Count() > 0)
                {
                    foreach (var ip in ipv4s)
                    {
                        output += ip.Address + " ";
                    }
                }
                else
                {
                    shouldAdd = false;
                }
            }
            if (DisplayIPv6)
            {
                if (ipv6s.Count() > 0)
                {
                    foreach (var ip in ipv6s)
                    {
                        if (ip.Address.IsIPv6LinkLocal || !LinkLocalOnly)
                        {
                            output += ip.Address + " ";
                        }
                    }
                }
                else
                {
                    shouldAdd = false;
                }
            }

            if (shouldAdd)
            {
                // Only notify the user and add the device if the it has all the info the user asked for.
                // When a device is first discovered, it may only have an ipv6 address.
                if (DeviceList.TryAdd(output, (ipv6s.First(), true)))
                {
                    PrintQueue.Enqueue($"Discovered: {output}");
                    //Console.WriteLine($"Discovered: {output}");
                }
                else // Device has been discovered already.
                {
                    // Mark the device as discovered (again) to ensure it isn't removed by the recheck logic.
                    DeviceList.TryUpdate(output, (DeviceList[output].ipv6, true), (DeviceList[output].ipv6, false));
                }
            }
        }

        /// <summary>
        /// Event handler for polling timer. Look for new devices and remove old ones.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FindDeviceTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            TimeSinceReCheck += QueryInterval;
            if (TimeSinceReCheck - ReCheckInterval > 0)
            {
                TimeSinceReCheck = 0;
                var enumerator = DeviceList.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var key = enumerator.Current.Key;
                    try
                    {
                        // If it is time to recheck devices, mark every device as not discovered
                        if (!DeviceList.TryUpdate(key, (DeviceList[key].ipv6, false), (DeviceList[key].ipv6, true)))
                        {
                            // TryUpdate returned false.
                            // The device has already was not found the previous recheck cycle. Assume it is gone and notify the user.
                            (AAAARecord ipv6, bool ActivelySeen) ret;
                            if (DeviceList.TryRemove(key, out ret))
                            {
                                PrintQueue.Enqueue($"Lost: {key}");
                            }
                        }
                    }
                    catch (KeyNotFoundException) { } // Device was already removed.
                }
            }

            // Look for new and previously discovered devices
            MDns.SendQuery(ServiceToQuery);
        }

        private static int ReCheckInterval;
        private static int TimeSinceReCheck;
        private static int QueryInterval;
        private static MulticastService MDns;
        private static string ServiceToQuery;
        private static bool LinkLocalOnly;
        private static bool DisplayHostname;
        private static bool DisplayIPv4;
        private static bool DisplayIPv6;
        private static object PrintLock = new object();
        private static System.Timers.Timer QueryTimer;
        private static ConcurrentDictionary<string, (AAAARecord ipv6, bool ActivelySeen)> DeviceList;
        private static ConcurrentQueue<string> PrintQueue;
        private static Timer PrintTimer;
    }
}
