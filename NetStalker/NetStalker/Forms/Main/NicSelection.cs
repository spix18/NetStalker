﻿using MaterialSkin;
using MaterialSkin.Controls;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Net;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Forms;

namespace NetStalker
{
    public partial class NicSelection : MaterialForm
    {
        private NetworkInterface SelectedInterface;
        private string FriendlyName;
        private static List<NetworkInterface> Nics = new List<NetworkInterface>();
        private MaterialSkinManager materialSkinManager;

        public NicSelection()
        {
            InitializeComponent();
            materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.ColorScheme = new ColorScheme(Primary.Grey800, Primary.Grey700, Primary.Grey900, Accent.Teal700, TextShade.WHITE);
            materialFlatButton1.Enabled = false;
        }

        /// <summary>
        /// Populates the list of available network cards.
        /// </summary>
        public void GetNics()
        {
            foreach (var net in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (net.OperationalStatus == OperationalStatus.Up && net.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    Nics.Add(net);
                }
            }
        }

        /// <summary>
        /// Returns the name of the connected network.
        /// </summary>
        /// <param name="neti"></param>
        /// <returns></returns>
        public static string GetConnectedNetworks(NetworkInterface neti)
        {
            var connectedNet = NetworkListManager.GetNetworks(NetworkConnectivityLevels.Connected);

            foreach (var net in connectedNet)
            {
                foreach (var conn in net.Connections)
                {
                    if (conn.AdapterId == Guid.Parse(neti.Id))
                    {
                        return net.Name;
                    }
                }

            }
            return "";
        }

        /// <summary>
        /// Returns the details for the selected network interface.
        /// </summary>
        /// <param name="FriendlyName"></param>
        /// <param name="selectedInterface"></param>
        public static void NetDetails(string FriendlyName, ref NetworkInterface selectedInterface)
        {
            foreach (var net in Nics)
            {
                if (net.Name == FriendlyName && net.NetworkInterfaceType != NetworkInterfaceType.Loopback && net.OperationalStatus == OperationalStatus.Up)
                {
                    selectedInterface = net;
                    return;
                }
            }

        }

        private void NicSelection_Load(object sender, EventArgs e)
        {
            Main m = Application.OpenForms["Main"] as Main;

            #region Some tedious visual garbage

            if (Properties.Settings.Default.color == "Dark")
            {
                materialSkinManager.Theme = MaterialSkinManager.Themes.DARK;
                m.textOverlay.BackColor = Color.FromArgb(71, 71, 71);
                m.textOverlay.TextColor = Color.FromArgb(204, 204, 204);
                m.textOverlay.BorderColor = Color.Teal;
                m.pictureBox2.Image = NetStalker.Properties.Resources._30G;

            }
            else
            {
                m.textOverlay.BorderColor = Color.Teal;
                materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;
                m.fastObjectListView1.HeaderFormatStyle = m.LightHeaders;
                m.fastObjectListView1.HotItemStyle = m.LightHot;
                m.textOverlay.BackColor = Color.FromArgb(204, 204, 204);
                m.textOverlay.TextColor = Color.FromArgb(71, 71, 71);
                m.fastObjectListView1.BackColor = Color.White;
                m.fastObjectListView1.ForeColor = Color.FromArgb(54, 54, 54);
                m.fastObjectListView1.SelectedBackColor = Color.FromArgb(214, 214, 214);
                m.fastObjectListView1.SelectedForeColor = Color.FromArgb(51, 51, 51);
                m.fastObjectListView1.UnfocusedSelectedBackColor = Color.FromArgb(71, 71, 71);
                m.fastObjectListView1.UnfocusedSelectedForeColor = Color.FromArgb(204, 204, 204);
                m.pictureBox2.Image = NetStalker.Properties.Resources._30W;
            }

            #endregion

            #region Check for the chosen minimization option

            if (Properties.Settings.Default.minimize == "Tray")
            {
                m.Resize -= m.Main_Resize_1;
                m.Resize += m.Main_Resize;
                m.resizestate = "Tray";
            }
            else
            {
                m.Resize -= m.Main_Resize;
                m.Resize += m.Main_Resize_1;
                m.resizestate = "Taskbar";
            }

            #endregion

            //Open the registry, show the License Agreement dialog if it's not accepted,
            //then check for the WinPcap driver and display an error dialog if it's not installed,
            //otherwise grab the driver version and show it.
            var root = Registry.CurrentUser;
            RegistryKey reg1 = root.OpenSubKey("Software", true).CreateSubKey("hSmNz");
            if (reg1 != null)
            {
                #region License agreement

                if (string.IsNullOrEmpty((string)reg1.GetValue("Des")) || (string)reg1.GetValue("Des") != "True")
                {
                    LicenseAgreement la = new LicenseAgreement();
                    if (la.ShowDialog() == DialogResult.Yes)
                    {
                        reg1.SetValue("Des", "True");
                    }
                    else
                    {
                        reg1.SetValue("Des", "False");
                        reg1.Close();
                        root.Close();
                        Application.Exit();
                    }
                }

                #endregion

                #region WinPcap driver check

                RegistryKey winpcapkey = null;
                if (Environment.Is64BitOperatingSystem)
                {
                    winpcapkey =
                       Registry.LocalMachine.OpenSubKey(
                           @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\WinPcapInst");
                }
                else
                {
                    winpcapkey =
                        Registry.LocalMachine.OpenSubKey(
                            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WinPcapInst");
                }

                if (winpcapkey != null)
                {
                    string ver = (string)winpcapkey.GetValue("DisplayName");
                    if (!string.IsNullOrEmpty(ver))
                    {
                        materialLabel3.Text = ver;
                        winpcapkey.Close();
                    }
                }
                else
                {
                    winpcapkey.Close();

                    ErrorForm EF = new ErrorForm();
                    EF.ShowDialog();
                }

                #endregion

                #region Password check

                if (!string.IsNullOrEmpty((string)reg1.GetValue("IsSNG")) && (string)reg1.GetValue("IsSNG") == "True")
                {
                    reg1.Close();
                    root.Close();
                    PasswordCheck pass = new PasswordCheck();
                    pass.ShowDialog();
                }

                #endregion
            }

            GetNics();

            //Populate the combo box with the available network interfaces
            foreach (var nic in Nics)
            {
                comboBox1.Items.Add(nic.Name);
            }
        }

        private void materialFlatButton2_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            FriendlyName = comboBox1.SelectedItem.ToString();
            SelectedInterface = Nics.FirstOrDefault(x => x.Name == FriendlyName);

            materialLabel10.Text = SelectedInterface?.NetworkInterfaceType.ToString() ?? "Error";

            //Show local IP
            foreach (var IP in SelectedInterface.GetIPProperties().UnicastAddresses)
            {
                if (IP.Address.AddressFamily == AddressFamily.InterNetwork) //Grab the IPV4 address of the selected NIC
                {
                    materialLabel4.Text = IP?.Address?.ToString() ?? "";
                    Properties.Settings.Default.NetMask = IP.IPv4Mask.ToString();
                    Properties.Settings.Default.NetSize = IP.IPv4Mask.ToString().Count(c => c == '0');
                    break;
                }
            }

            //Show local MAC
            materialLabel5.Text = SelectedInterface?
                .GetPhysicalAddress()?
                .ToString().Insert(2, "-").Insert(5, "-").Insert(8, "-").Insert(11, "-").Insert(14, "-") ?? "";

            //Show gateway IP
            GatewayIPAddressInformationCollection addresses = null;

            if ((addresses = SelectedInterface.GetIPProperties().GatewayAddresses).Count == 0)
            {
                materialLabel7.Text = "";
                materialFlatButton1.Enabled = false;
            }
            else
            {
                foreach (var gateway in addresses)
                {
                    if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        materialLabel7.Text = gateway?.Address?.ToString() ?? "";
                        materialFlatButton1.Enabled = true;
                        break;
                    }
                }
            }

            //Show connected wireless network (if there is one)
            if (SelectedInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                materialLabel12.Text = GetConnectedNetworks(SelectedInterface) ?? "";
            }
            else
            {
                materialLabel12.Text = "";
            }
        }

        private void materialFlatButton1_Click(object sender, EventArgs e)
        {
            //Save the main app configuration values
            Properties.Settings.Default.friendlyname = FriendlyName;
            Properties.Settings.Default.Gateway =
                materialLabel7.Text;
            Properties.Settings.Default.localip =
                materialLabel4.Text;
            Properties.Settings.Default.localmac =
              SelectedInterface.GetPhysicalAddress().ToString();

            Properties.Settings.Default.Save();

            Close();
        }

        private void ComboBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            //Style the combo box dynamically
            if (e.Index != -1)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                e.Graphics.FillRectangle(Brushes.DimGray, e.Bounds);
                e.Graphics.DrawString(Nics[e.Index].Name, new Font("Roboto", 9), Brushes.LightGray,
                    new Point(Properties.Resources.icons8_network_card_16.Width * 2, e.Bounds.Y));
                e.Graphics.DrawImage(Properties.Resources.icons8_network_card_16, new Point(e.Bounds.X, e.Bounds.Y));

                if ((e.State & DrawItemState.Focus) == 0)
                {
                    e.Graphics.FillRectangle(Brushes.LightGray, e.Bounds);
                    e.Graphics.DrawString(Nics[e.Index].Name, new Font("Roboto", 9), Brushes.DimGray,
                        new Point(Properties.Resources.icons8_network_card_16.Width * 2, e.Bounds.Y));
                    e.Graphics.DrawImage(Properties.Resources.icons8_network_card_16, new Point(e.Bounds.X, e.Bounds.Y));
                }
            }
        }
    }
}
