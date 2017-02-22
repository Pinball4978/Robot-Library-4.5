using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace Robot_Library_4_5
{
    public class IPAddressChanger
    {
        public enum Connection_Type { Ethernet = 0x01, Wireless = 0x10};
        List<string> connectionNames;
        Process netSH;

        public IPAddressChanger(Connection_Type connectionToChange)
        {
            connectionNames = new List<string>();
            System.Net.NetworkInformation.NetworkInterface[] interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (System.Net.NetworkInformation.NetworkInterface network in interfaces)
            {
                if (network.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                {
                    if (connectionToChange == Connection_Type.Ethernet && network.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet)
                    {
                        connectionNames.Add(network.Name);
                    }
                    else if (connectionToChange == Connection_Type.Wireless && network.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                    {
                        connectionNames.Add(network.Name);
                    }
                }
            }
            netSH = new Process();
            netSH.StartInfo = new ProcessStartInfo("cmd");
            netSH.StartInfo.CreateNoWindow = true;
            netSH.StartInfo.LoadUserProfile = false;
            netSH.StartInfo.RedirectStandardOutput = true;
            netSH.StartInfo.UseShellExecute = false;
        }

        public string getCurrentIPAddress(int connectionNumber)
        {
            netSH.StartInfo.Arguments = "/C netsh interface ip show address \"" + connectionNames[connectionNumber] + "\" | find \"IP Address\"";
            netSH.StartInfo.Verb = "";
            netSH.StartInfo.RedirectStandardOutput = true;
            netSH.StartInfo.UseShellExecute = false;
            netSH.Start();
            StreamReader inputStream = netSH.StandardOutput;
            string temp = inputStream.ReadLine();
            while (temp.Equals(""))
            {
                temp = inputStream.ReadLine();
            }
            int index = temp.IndexOf(':');
            temp = temp.Substring(index + 1).Trim();
            return temp;
        }

        public void setIPAddress(int connectionNumber, string address)
        {
            netSH.StartInfo.Arguments = "/C netsh interface ip set address name=\"" + connectionNames[connectionNumber] + "\" source=static addr=" + address + " mask=255.255.255.0";
            netSH.StartInfo.Verb = "runas";
            netSH.StartInfo.RedirectStandardOutput = false;
            netSH.StartInfo.UseShellExecute = true;
            netSH.Start();
        }

        public void resetIPAddress(int connectionNumber)
        {
            netSH.StartInfo.Arguments = "/C netsh interface ip set address name=\"" + connectionNames[connectionNumber] + "\" source=dhcp";
            netSH.StartInfo.Verb = "runas";
            netSH.StartInfo.RedirectStandardOutput = false;
            netSH.StartInfo.UseShellExecute = true;
            netSH.Start();
        }
    }
}
