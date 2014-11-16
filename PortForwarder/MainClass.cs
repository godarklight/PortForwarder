using System;
using System.Collections.Generic;
using System.IO;

namespace PortForwarder
{
    public class MainClass
    {
        public static string applicationPath;
        private static List<NetworkForwarder> forwarders = new List<NetworkForwarder>();

        public static void Main()
        {
            //Setup
            applicationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Settings.instance = new Settings();

            //Load forwarders
            StartForwarders();

            //Wait until exit
            Console.WriteLine("Ready - type /exit to exit or /reload to reload.");
            string consoleLine = "";
            while (consoleLine == null || consoleLine != "/exit")
            {
                if (consoleLine == "/reload")
                {
                    Reload();
                }
                consoleLine = Console.ReadLine();
            }

            Console.WriteLine("Goodbye!");
        }

        private static void Reload()
        {
            StopForwarders();
            Settings.instance.LoadSettings();
            StartForwarders();
        }

        private static void StartForwarders()
        {
            foreach (ForwardEntry entry in Settings.instance.GetForwardEntries())
            {
                NetworkForwarder newEntry = new NetworkForwarder(entry);
                forwarders.Add(newEntry);
                newEntry.Start();
            }
        }

        private static void StopForwarders()
        {
            foreach (NetworkForwarder forwarder in forwarders)
            {
                forwarder.Stop();
            }
            forwarders.Clear();
        }
    }
}

