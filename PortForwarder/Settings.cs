using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace PortForwarder
{
    public class Settings
    {
        public static Settings instance;
        private string settingsFile;
        private List<ForwardEntry> entries = new List<ForwardEntry>();

        public Settings()
        {
            settingsFile = Path.Combine(MainClass.applicationPath, "portforwarding.txt");
            WriteDefaultSettingsFileIfNeeded();
            LoadSettings();
        }

        private void WriteDefaultSettingsFileIfNeeded()
        {
            if (!File.Exists(settingsFile))
            {
                Console.WriteLine("Creating settings file, edit it and restart the program!");
                using (StreamWriter sw = new StreamWriter(settingsFile, false))
                {
                    sw.WriteLine("#Port forwarding definitions");
                    sw.WriteLine("#File format: local port, remote destination hostname or IP, remote port");
                }                   
            }
        }

        public void LoadSettings()
        {
            Console.WriteLine("Loading settings file at " + settingsFile);
            entries.Clear();
            using (StreamReader sw = new StreamReader(settingsFile))
            {
                string currentLine;
                while ((currentLine = sw.ReadLine()) != null)
                {
                    if (currentLine.StartsWith("#"))
                    {
                        continue;
                    }
                    if (currentLine.Trim() == "")
                    {
                        continue;
                    }
                    string[] splitLine = currentLine.Split(',');
                    if (splitLine.Length != 3)
                    {
                        Console.WriteLine("Error reading line: " + currentLine);
                        continue;
                    }
                    AddForwardEntry(splitLine);
                }
            }
        }

        private void AddForwardEntry(string[] line)
        {
            string sourcePortString = line[0].Trim();
            string destinationIPString = line[1].Trim();
            string destinationPortString = line[2].Trim();
            ForwardEntry newEntry = new ForwardEntry();
            //Source port
            if (!Int32.TryParse(sourcePortString, out newEntry.sourcePort))
            {
                Console.WriteLine("Error reading value '" + sourcePortString + "': source port is not a number");
                return;
            }
            if (newEntry.sourcePort < 0 || newEntry.sourcePort > 65535)
            {
                Console.WriteLine("Error reading value '" + sourcePortString + "': source port is out of range");
                return;
            }
            //Destination IP
            if (!IPAddress.TryParse(destinationIPString, out newEntry.destinationIP))
            {
                IPAddress[] addresses;
                try
                {
                    addresses = Dns.GetHostEntry(destinationIPString).AddressList;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error reading value '" + destinationIPString + "': DNS Exception: " + e.Message);
                    addresses = new IPAddress[0];
                }
                if (addresses.Length == 0)
                {
                    Console.WriteLine("Error reading value '" + destinationIPString + "': did not return an address");
                    return;
                }
                //Try V6 first
                foreach (IPAddress address in addresses)
                {
                    if (address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        newEntry.destinationIP = address;
                        break;
                    }
                }
                if (newEntry.destinationIP == null)
                {
                    //Try V4 is no address was found
                    foreach (IPAddress address in addresses)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            newEntry.destinationIP = address;
                            break;
                        }  
                    }
                }
                if (newEntry.destinationIP == null)
                {
                    Console.WriteLine("Error reading value '" + destinationIPString + "': did not return an address");
                    return;
                }
            }
            //Destination port
            if (!Int32.TryParse(destinationPortString, out newEntry.destinationPort))
            {
                Console.WriteLine("Error reading value '" + destinationPortString + "': destination port is not a number");
                return;
            }
            if (newEntry.sourcePort < 0 || newEntry.sourcePort > 65535)
            {
                Console.WriteLine("Error reading value '" + destinationPortString + "': destination port is out of range");
                return;
            }
            Console.WriteLine("Port forwarding port " + newEntry.sourcePort + " to " + destinationIPString + " (" + newEntry.destinationIP + ") port " + newEntry.destinationPort);
            entries.Add(newEntry);
        }

        public ForwardEntry[] GetForwardEntries()
        {
            return entries.ToArray();
        }
    }

    public class ForwardEntry
    {
        public int sourcePort;
        public IPAddress destinationIP;
        public int destinationPort;
    }
}

