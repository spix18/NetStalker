﻿using BrightIdeasSoftware;
using MaterialSkin;
using MaterialSkin.Controls;
using MetroFramework;
using MetroFramework.Controls;
using NetStalker.MainLogic;
using NetStalker.ToastNotifications;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace NetStalker
{
    public partial class Main : MaterialForm, IView
    {
        #region Main Vars

        public static bool operationinprogress;
        public string resizestate;
        public TextOverlay textOverlay;
        public bool resizeDone;
        public Loading loading;
        public bool SnifferStarted;
        public readonly Timer ValuesTimer;
        public readonly Timer AliveTimer;
        public int timerCount;
        public bool PromptCalled;
        public static List<Device> Devices = new List<Device>();
        public static IPAddress LocalIp;
        public static PhysicalAddress LocalMac;
        public static bool CheckboxActive;

        #endregion

        public Main(string[] args = null)
        {
            InitializeComponent();

            #region MaterialSkin Configuration

            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);

            #endregion

            #region Object ListView Initial Configuration

            this.olvColumn1.ImageGetter = delegate (object rowObject) //Set the images from the image list (with setting small image list in properties)
            {
                Device s = (Device)rowObject;
                if (s.IsGateway)
                    return "router"; //Image name in the image list
                else
                    return "pc"; //Image name in the image list
            };
            this.olvColumn1.GroupKeyGetter = delegate (object rowObject) //Give every model object a key so items with the same key are grouped together
            {
                var Device = rowObject as Device;

                if (Device.IsGateway)
                {
                    return "Gateway";
                }
                else if (Device.IsLocalDevice)
                {
                    return "Own Device";
                }
                else
                {
                    return "Devices";
                }
            };
            this.olvColumn1.GroupKeyToTitleConverter = delegate (object key) { return key.ToString(); }; //Convert the key to a title for the groups
            fastObjectListView1.ShowGroups = true;

            textOverlay = this.fastObjectListView1.EmptyListMsgOverlay as TextOverlay;
            textOverlay.Font = new Font("Roboto", 25);

            #endregion

            #region Update Timers

            //Speed Timer
            ValuesTimer = new Timer
            {
                Interval = 1000
            };
            ValuesTimer.Tick += ValuesTimerOnTick;

            //Timeout Timer
            AliveTimer = new Timer
            {
                Interval = 5000
            };
            AliveTimer.Tick += AliveTimerOnTick;

            #endregion
        }

        #region Timers Handlers

        //Timeout handler: it removes devices once they exceed the timeoout period
        private void AliveTimerOnTick(object sender, EventArgs e)
        {
            try
            {
                //255.255.255.0
                if (Properties.Settings.Default.NetSize == 1)
                {
                    foreach (var Device in Devices)
                    {
                        if (!Device.IsGateway && !Device.IsLocalDevice && (DateTime.Now.Ticks - Device.TimeSinceLastArp.Ticks) > 600000000L) //1 minute
                        {
                            Devices.Remove(Device);
                            Scanner.ClientList.Remove(Device.IP);

                            Device.Blocked = false;
                            Device.Redirected = false;
                            fastObjectListView1.RemoveObject(Device);
                        }
                    }
                }
                //255.255.0.0
                else if (Properties.Settings.Default.NetSize == 2)
                {
                    foreach (var Device in Devices)
                    {
                        if (!Device.IsGateway && !Device.IsLocalDevice && (DateTime.Now.Ticks - Device.TimeSinceLastArp.Ticks) > 3000000000L) //5 minutes
                        {
                            Devices.Remove(Device);
                            Scanner.ClientList.Remove(Device.IP);

                            Device.Blocked = false;
                            Device.Redirected = false;
                            fastObjectListView1.RemoveObject(Device);
                        }
                    }
                }
                //255.0.0.0
                else if (Properties.Settings.Default.NetSize == 3)
                {
                    foreach (var Device in Devices)
                    {
                        if (!Device.IsGateway && !Device.IsLocalDevice && (DateTime.Now.Ticks - Device.TimeSinceLastArp.Ticks) > 6000000000L) //10 minutes, extremely large networks this option could theoretically work, but not worth it.
                        {
                            Devices.Remove(Device);
                            Scanner.ClientList.Remove(Device.IP);

                            Device.Blocked = false;
                            Device.Redirected = false;
                            fastObjectListView1.RemoveObject(Device);
                        }
                    }
                }
            }
            catch
            {

            }
        }

        //Speed update handler: it updates the speed of targeted devices in the UI
        private void ValuesTimerOnTick(object sender, EventArgs e)
        {
            timerCount++;
            foreach (Device Device in Devices)
            {
                if (Device.Redirected)
                {
                    string D = ((float)Device.PacketsReceivedSinceLastReset * 0.0009765625f / (float)(this.ValuesTimer.Interval / 1000) / (float)this.timerCount).ToString();
                    string U = ((float)Device.PacketsSentSinceLastReset * 0.0009765625f / (float)(this.ValuesTimer.Interval / 1000) / (float)this.timerCount).ToString();

                    //0.0009765625f = 1/1024 Conversion from Bytes to KBytes

                    if (D.Length - D.IndexOf(".") > 1) //remove the number after the period
                    {
                        int num = -2 - D.IndexOf("."); //exclude the first number and the period
                        string str3 = D;
                        D = str3.Remove(str3.IndexOf(".") + 1, D.Length + num); //remove all the numbers to the right of the period but the first number
                    }
                    if (U.Length - U.IndexOf(".") > 1) //same here for the upload
                    {
                        int num = -2 - U.IndexOf(".");
                        string str3 = U;
                        U = str3.Remove(str3.IndexOf(".") + 1, U.Length + num);
                    }
                    Device.DownloadSpeed = D + " KB/s";
                    Device.UploadSpeed = U + " KB/s";
                    fastObjectListView1.UpdateObject(Device);
                }
            }
            ResetPacketCount();
            timerCount = 0;
        }

        #endregion

        #region Tools

        /// <summary>
        /// Reset recieved and sent packets for all devices in order for the value counter to compute the next value
        /// </summary>
        public void ResetPacketCount()
        {
            foreach (var device in Devices)
            {
                device.PacketsSentSinceLastReset = 0;
                device.PacketsReceivedSinceLastReset = 0;
            }
        }

        #endregion

        #region IView Members

        public FastObjectListView ListView1
        {
            get
            {
                return fastObjectListView1;
            }
        }
        public MaterialLabel StatusLabel
        {
            get
            {
                return materialLabel2;
            }
        }
        public MaterialLabel StatusLabel2
        {
            get
            {
                return materialLabel3;
            }
        }
        public Form MainForm
        {
            get
            {
                return this;
            }
        }
        public PictureBox LoadingBar
        {
            get
            {
                return pictureBox2;
            }
        }
        public PictureBox PictureBox
        {
            get { return pictureBox1; }
        }
        public ToolTip TTip
        {
            get { return toolTip1; }
        }
        public MetroTile Tile
        {
            get { return metroTile1; }
        }
        public MetroTile Tile2
        {
            get { return metroTile2; }
        }

        #endregion

        #region Main Form Event Handlers

        #region From Event Handlers

        private void Main_Shown(object sender, EventArgs e)
        {
            NicSelection nicform = new NicSelection();
            nicform.ShowDialog();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            Controller.AttachOnExitEventHandler(this);

            Tools.TryCreateShortcut();
        }

        public void Main_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && !PromptCalled)
            {
                Hide();
                notifyIcon1.Visible = true;
                notifyIcon1.ShowBalloonTip(2);

                if (string.IsNullOrEmpty(Properties.Settings.Default.SuppressN))
                {
                    NotificationAPI napi = new NotificationAPI();
                    napi.CreateAndShowPrompt("Do you want me to inform you of newly connected devices?\n\n(This option can be changed back in the Options menu)");
                    PromptCalled = true;
                }
            }
        }

        public void Main_Resize_1(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && !PromptCalled)
            {
                if (string.IsNullOrEmpty(Properties.Settings.Default.SuppressN))
                {
                    NotificationAPI napi = new NotificationAPI();
                    napi.CreateAndShowPrompt("Do you want me to inform you of newly connected devices?\n\n(This option can be changed back in the Options menu)");
                    PromptCalled = true;
                }
            }
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            var nic = Application.OpenForms["NicSelection"] as NicSelection;

            if (nic == null)
            {
                if (AliveTimer.Enabled && ValuesTimer.Enabled)
                {
                    AliveTimer.Enabled = false;
                    AliveTimer.Dispose();

                    ValuesTimer.Enabled = false;
                    ValuesTimer.Dispose();
                }

                if (MetroMessageBox.Show(this, "Quit the application ?", "Quit", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        #endregion

        #region Button Event Handlers

        private void materialFlatButton1_Click(object sender, EventArgs e)
        {
            try
            {
                if (!operationinprogress)
                {
                    if (fastObjectListView1.GetItemCount() > 0)
                    {
                        if (MetroMessageBox.Show(this, "The list will be cleared and a new scan will be initiated are you sure?\nNote: The Scan button is recommended when the list is empty, NetStalker always performs background scans for new devices after the initial scan.", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            metroTile1.Enabled = false;
                            metroTile2.Enabled = false;
                            operationinprogress = true;
                            olvColumn7.MaximumWidth = 100;
                            olvColumn7.MinimumWidth = 100;
                            olvColumn7.Width = 100;
                            resizeDone = false;
                            materialLabel3.Text = "Working";
                            fastObjectListView1.EmptyListMsg = "Scanning...";
                            StatusLabel.Text = "Please wait...";
                            pictureBox1.Image = Properties.Resources.icons8_attention_96px;

                            foreach (var Device in Devices)
                            {
                                if (Device.Redirected || Device.Blocked)
                                {
                                    Device.Blocked = false;
                                    Device.Redirected = false;
                                    Device.DownloadCap = 0;
                                    Device.UploadCap = 0;
                                    Device.DownloadSpeed = "";
                                    Device.UploadSpeed = "";
                                }
                            }

                            fastObjectListView1.ClearObjects();

                            Task.Run(() => { Controller.RefreshClients(this); });
                        }
                    }
                    else
                    {
                        metroTile1.Enabled = false;
                        metroTile2.Enabled = false;
                        operationinprogress = true;
                        olvColumn7.MaximumWidth = 100;
                        olvColumn7.MinimumWidth = 100;
                        olvColumn7.Width = 100;
                        resizeDone = false;
                        materialLabel3.Text = "Working";
                        fastObjectListView1.EmptyListMsg = "Scanning...";
                        StatusLabel.Text = "Please wait...";
                        pictureBox1.Image = Properties.Resources.icons8_attention_96px;

                        AliveTimer.Enabled = true;

                        Task.Run(() =>
                        {
                            Controller.RefreshClients(this);
                            operationinprogress = false;
                        });
                    }
                }
                else
                {
                    MetroMessageBox.Show(this, "A scan is still in progress please wait until its finished", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception)
            {

            }

        }

        private void MaterialFlatButton2_Click(object sender, EventArgs e)
        {
            MetroMessageBox.Show(this, "Some guidelines on how to use this software properly:\n\n1- In order to use the Packet Sniffer you have to activate redirection for the selected device first. Note: For the Packet Sniffer to work properly, redirection and speed limitation will be deactivated for all but the selected device.\n2- In order to use the Speed Limiter you have to activate redirection for the selected device, once activated it will start redirecting packets for the selected device with no speed limitation, then you can open the speed limiter (on the bottom right) and set the desired speed for each device (0 means no limitation).\n3- Blocking and redirection can not be activated at the sametime, you either block a device or limit its speed.\n4- It's recommended for most stability to wait until the scanner is done before performing any action.\n5- NetStalker can be protected with a password, and can be set or removed via Options.\n6- NetStalker is available in dark and light modes.\n7- NetStalker has an option for spoof protection, if activated it can prevent your pc from being redirected or blocked by the same tool or any other spoofing software.\n8- Background scanning is always active so you don't have to consistently press scan to discover newly connected devices.", "Help", MessageBoxButtons.OK,
                MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, 375);
        }

        private void MaterialFlatButton3_Click_1(object sender, EventArgs e)
        {
            AboutForm af = new AboutForm();
            af.ShowDialog();
        }

        private void materialFlatButton4_Click(object sender, EventArgs e)
        {
            Options options = new Options();
            options.ShowDialog();
        }

        private void metroTile1_Click(object sender, EventArgs e)
        {
            try
            {
                if (fastObjectListView1.SelectedObjects.Count == 0)
                {
                    throw new ArgumentNullException();
                }

                var selectedDevice = fastObjectListView1.SelectedObject as Device;

                //Device should be redirected and not a gateway or a local device
                if (!selectedDevice.Redirected && !(selectedDevice.IsGateway || selectedDevice.IsLocalDevice))
                {
                    throw new CustomExceptions.RedirectionNotActiveException();
                }

                Task.Run(() =>
                {
                    loading = new Loading();
                    loading.ShowDialog();
                });

                SnifferStarted = true;

                //For the berkeley packet filter to work, mac addresses should have ':' separating each hex number
                Sniffer sniff = new Sniffer(selectedDevice.IP.ToString(), Tools.GetMACString(selectedDevice.MAC), Tools.GetMACString(AppConfiguration.GatewayMac), AppConfiguration.GatewayIp.ToString(), loading);
                sniff.ShowDialog(this);

                fastObjectListView1.SelectedObjects.Clear();

                sniff.Dispose();
                SnifferStarted = false;

                fastObjectListView1.UpdateObject(selectedDevice);

            }
            catch (ArgumentNullException)
            {
                MetroMessageBox.Show(this, "Select a device first!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (CustomExceptions.OperationInProgressException)
            {
                MetroMessageBox.Show(this, "The Packet Sniffer can't be used while the limiter is active!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (CustomExceptions.RedirectionNotActiveException)
            {
                MetroMessageBox.Show(this, "Redirection must be active for this device!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

        }

        private void metroTile2_Click(object sender, EventArgs e)
        {
            try
            {
                //No selected device
                if (fastObjectListView1.SelectedObjects.Count == 0)
                {
                    throw new ArgumentNullException();
                }

                var device = fastObjectListView1.SelectedObject as Device;

                //Check if the selected device is a gateway or own device
                if (device.IsGateway || device.IsLocalDevice)
                {
                    throw new CustomExceptions.LocalHostTargeted();
                }

                //Check if device is redirected before applying a speed limit
                if (!device.Redirected)
                    throw new CustomExceptions.RedirectionNotActiveException();


                LimiterSpeed ls = new LimiterSpeed(device);
                if (ls.ShowDialog() == DialogResult.OK)
                {
                    if (device.Limited)
                    {
                        fastObjectListView1.UpdateObject(device);
                    }
                    else
                    {
                        MetroMessageBox.Show(this, "Start redirection first!", "Error", MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }

                    ls.Dispose();
                }
            }
            catch (ArgumentNullException)
            {
                MetroMessageBox.Show(this, "Choose a device first and activate redirection for it!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (CustomExceptions.LocalHostTargeted)
            {
                MetroMessageBox.Show(this, "This operation can not target the gateway or your own ip address!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (CustomExceptions.RedirectionNotActiveException)
            {
                MetroMessageBox.Show(this, "Redirection must be active for this device!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

        }

        #endregion

        #region List Event Handlers

        private void FastObjectListView1_MouseDown(object sender, MouseEventArgs e)
        {
            var item = fastObjectListView1.GetItemAt(e.X, e.Y);
            if (item == null)
            {
                fastObjectListView1.ContextMenu = null;
                fastObjectListView1.SelectedObjects.Clear();
            }
        }

        private void FastObjectListView1_ItemsAdding(object sender, ItemsAddingEventArgs e)
        {
            if (fastObjectListView1.Items.Count >= 8 && !resizeDone)
            {
                olvColumn7.MaximumWidth = 83;
                olvColumn7.MinimumWidth = 83;
                olvColumn7.Width = 83;
                resizeDone = true;
            }

            if (WindowState == FormWindowState.Minimized && Properties.Settings.Default.SuppressN == "False")
            {
                var Ad = e.ObjectsToAdd.Cast<Device>().ToList();
                if (Ad.Count > 0)
                {
                    Device Device = Ad[0];
                    NotificationAPI Napi = new NotificationAPI(Device);
                    Napi.CreateNotification();
                    Napi.AttachHandlers();
                    Napi.ShowToast();
                }

            }
        }

        private void FastObjectListView1_SubItemChecking(object sender, SubItemCheckingEventArgs e)
        {
            try
            {
                //Don't allow blocking / redirection while the sniffer is active.
                if (SnifferStarted)
                    throw new CustomExceptions.OperationInProgressException();

                if (!CheckboxActive)
                {
                    e.Canceled = true;
                    return;
                }

                //Get the device in the selected row
                fastObjectListView1.SelectObject(e.RowObject);
                Device device = e.RowObject as Device;

                if (device.IsGateway || device.IsLocalDevice)
                    throw new CustomExceptions.GatewayTargeted();

                if (e.NewValue == CheckState.Checked && e.Column.Index == 6 && !device.Blocked && !device.Redirected)
                {
                    //Update device state in list
                    var listDevice = Devices.FirstOrDefault(D => D.MAC == device.MAC);
                    if (listDevice == null)
                        throw new CustomExceptions.DeviceNotInListException("Device was not found in the list of targeted devices.");

                    listDevice.Blocked = true;

                    //Update device state in UI
                    device.Blocked = true;
                    device.DeviceStatus = "Offline";
                    fastObjectListView1.UpdateObject(device);
                    pictureBox1.Image = NetStalker.Properties.Resources.icons8_ok_96;

                    //Activate the BR if it's not already active
                    if (!Blocker_Redirector.BRMainSwitch)
                    {
                        Blocker_Redirector.BRMainSwitch = true;
                        Blocker_Redirector.BlockAndRedirect();
                    }
                }
                else if (e.NewValue == CheckState.Checked && e.Column.Index == 5 && !device.Blocked && !device.Redirected)
                {
                    //Update device state in list
                    var listDevice = Devices.FirstOrDefault(D => D.MAC == device.MAC);
                    if (listDevice == null)
                        throw new CustomExceptions.DeviceNotInListException("Device was not found in the list of targeted devices.");

                    listDevice.Blocked = true;
                    listDevice.Redirected = true;

                    //Update device state in UI
                    device.Blocked = true;
                    device.Redirected = true;
                    device.DownloadCap = 0;
                    device.UploadCap = 0;
                    fastObjectListView1.UpdateObject(device);
                    pictureBox1.Image = NetStalker.Properties.Resources.icons8_ok_96;

                    //Activate the BR if it's not already active
                    if (!Blocker_Redirector.BRMainSwitch)
                    {
                        Blocker_Redirector.BRMainSwitch = true;
                        Blocker_Redirector.BlockAndRedirect();
                    }

                    //Start value counter if it's not already started
                    if (!ValuesTimer.Enabled)
                    {
                        ValuesTimer.Enabled = true;
                    }
                }
                else if (e.NewValue == CheckState.Unchecked && e.Column.Index == 6 && device.Blocked)
                {
                    //Update device state in list
                    var listDevice = Devices.FirstOrDefault(D => D.MAC == device.MAC);
                    if (listDevice == null)
                        throw new CustomExceptions.DeviceNotInListException("Device was not found in the list of targeted devices.");

                    listDevice.Blocked = false;

                    //Update device state in UI
                    device.Blocked = false;
                    device.DeviceStatus = "Online";
                    fastObjectListView1.UpdateObject(device);

                    //Checks if there are any devices left with active targeting
                    if (!Devices.Any(D => D.Blocked == true))
                    {
                        pictureBox1.Image = NetStalker.Properties.Resources.icons8_ok_96px;
                        Blocker_Redirector.BRMainSwitch = false;
                    }
                }
                else if (e.NewValue == CheckState.Unchecked && e.Column.Index == 5 && device.Redirected)
                {
                    //Update device state in list
                    var listDevice = Devices.FirstOrDefault(D => D.MAC == device.MAC);
                    if (listDevice == null)
                        throw new CustomExceptions.DeviceNotInListException("Device was not found in the list of targeted devices.");

                    listDevice.Blocked = false;
                    listDevice.Redirected = false;

                    device.Blocked = false;
                    device.Redirected = false;
                    device.DownloadCap = 0;
                    device.UploadCap = 0;
                    device.DownloadSpeed = "";
                    device.UploadSpeed = "";
                    fastObjectListView1.UpdateObject(device);

                    //Checks if there are any devices left with the Redirected switch
                    if (!Devices.Any(D => D.Redirected == true))
                    {
                        pictureBox1.Image = NetStalker.Properties.Resources.icons8_ok_96px;
                        Blocker_Redirector.BRMainSwitch = false;
                        ValuesTimer.Enabled = false;
                        ValuesTimer.Stop();
                    }
                }
                else
                {
                    //The user action didn't hit any of our conditions so we cancel it and reset the value
                    e.Canceled = true;
                    e.NewValue = e.CurrentValue;
                }

            }
            catch (CustomExceptions.GatewayTargeted)
            {
                MetroMessageBox.Show(this, "This operation can not target the gateway or your own ip address!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                e.Canceled = true;
            }
            catch (CustomExceptions.OperationInProgressException)
            {
                MetroMessageBox.Show(this, "The Speed Limiter can't be used while the sniffer is active!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (CustomExceptions.DeviceNotInListException)
            {
                MetroMessageBox.Show(this, "The selected device was not found in the list or targets", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Tooltip Event Handlers

        private void ToolTip1_Draw(object sender, DrawToolTipEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(51, 51, 51)), e.Bounds);//Background color

            e.Graphics.DrawRectangle(new Pen(Color.FromArgb(204, 204, 204), 1), new Rectangle(e.Bounds.X, e.Bounds.Y,
                e.Bounds.Width - 1, e.Bounds.Height - 1));//The white bounds

            e.Graphics.DrawString(e.ToolTipText, new Font("Roboto", 9), new SolidBrush(Color.FromArgb(204, 204, 204)), e.Bounds.X + 8, e.Bounds.Y + 7); //Text with image location
        }

        private void ToolTip1_Popup(object sender, PopupEventArgs e)
        {
            e.ToolTipSize = new Size(e.ToolTipSize.Width - 7, e.ToolTipSize.Height - 5);
        }

        #endregion

        #region Notify Icon Event Handlers

        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            notifyIcon1.Visible = false;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        #endregion  

        #endregion
    }
}
