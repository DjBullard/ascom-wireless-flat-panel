/*
 * SetupDialogForm.cs
 * Copyright (C) 2022 - Present, Julien Lecomte - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using System.Diagnostics;
using Windows.Devices.Bluetooth.Advertisement;
using System.Linq;
using System.Drawing;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace ASCOM.DarkSkyGeek
{
    // Form not registered for COM!
    [ComVisible(false)]

    public partial class SetupDialogForm : Form
    {
        // How much taller the dialog gets when the live log is toggled on,
        // to reveal the (otherwise hidden) logTextBox without disturbing the
        // rest of the layout, which is all anchored to the bottom edge.
        private const int LOG_PANEL_HEIGHT_DELTA = 128;

        WirelessFlatPanel wirelessFlatPanel;
        BluetoothLEAdvertisementWatcher watcher;
        ulong bleDeviceAddress;

        public SetupDialogForm(WirelessFlatPanel wirelessFlatPanel)
        {
            InitializeComponent();
            this.wirelessFlatPanel = wirelessFlatPanel;
            this.Text = $"{this.Text} (v{wirelessFlatPanel.DriverVersion})";
        }

        private void AppendLog(string message)
        {
            logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        private void chkShowLog_CheckedChanged(object sender, EventArgs e)
        {
            logTextBox.Visible = chkShowLog.Checked;
            this.Height += chkShowLog.Checked ? LOG_PANEL_HEIGHT_DELTA : -LOG_PANEL_HEIGHT_DELTA;
        }

        private void linkTraceHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            MessageBox.Show(
                "Trace logging records detailed diagnostic information about this driver's " +
                "activity (connecting, disconnecting, sending brightness commands, etc.) to a " +
                "log file.\n\n" +
                "It's off by default since it isn't needed for day-to-day use. Turn it on if " +
                "you're troubleshooting a connection problem, or if asked to provide logs when " +
                "reporting an issue. Look under your ASCOM Logs folder (see the ASCOM " +
                "Diagnostics tool if you're not sure where that is) for the resulting file.",
                "About Trace Logging",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private string getFormattedBluetoothAddress(ulong address)
        {
            string hexValue = address.ToString("X");
            if (hexValue.Length == 12)
            {
                return hexValue.Insert(10, ":").Insert(8, ":").Insert(6, ":").Insert(4, ":").Insert(2, ":");
            }
            throw new Exception("Invalid Bluetooth Address: " + address);
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            wirelessFlatPanel.tl.Enabled = chkTrace.Checked;
            wirelessFlatPanel.bleDeviceAddress = bleDeviceAddress;
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void BrowseToHomepage(object sender, EventArgs e)
        {
            try
            {
                Process.Start("https://github.com/jlecomte/wireless-flat-panel");
            }
            catch (System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        private void SetupDialogForm_Load(object sender, EventArgs e)
        {
            chkTrace.Checked = wirelessFlatPanel.tl.Enabled;
            bleDeviceAddress = wirelessFlatPanel.bleDeviceAddress;

            // Adjust the position of the label so it looks decent...
            pairedDeviceAddrValue.Location = new Point(pairedDeviceAddrLbl.Location.X + pairedDeviceAddrLbl.Width, pairedDeviceAddrLbl.Location.Y);

            try
            {
                pairedDeviceAddrValue.Text = getFormattedBluetoothAddress(wirelessFlatPanel.bleDeviceAddress);
                pairedDeviceAddrValue.ForeColor = System.Drawing.Color.Green;
            }
            catch (Exception)
            {
                pairedDeviceAddrValue.Text = "No device paired";
                pairedDeviceAddrValue.ForeColor = System.Drawing.Color.Red;
            }

            if (wirelessFlatPanel.Connected)
            {
                chkTrace.Enabled = false;
                devicesListBox.Enabled = false;
                deviceSelectionBtn.Enabled = false;
                AppendLog("Already connected; device scanning is disabled.");
            }
            else
            {
                watcher = new BluetoothLEAdvertisementWatcher
                {
                    ScanningMode = BluetoothLEScanningMode.Active
                };

                AppendLog("Scanning for nearby wireless flat panels...");

                watcher.Received += (w, args) =>
                {
                    var uuids = args.Advertisement.ServiceUuids;
                    foreach (var uuid in uuids)
                    {
                        if (uuid.Equals(WirelessFlatPanel.BLE_SERVICE_UUID))
                        {
                            ulong address = args.BluetoothAddress;
                            short rssi = args.RawSignalStrengthInDBm;
                            string formattedAddress = getFormattedBluetoothAddress(address);

                            devicesListBox.Invoke(new Action(() =>
                            {
                                // Was this device previously added to the list? Let's find out...
                                ListBoxItem existing = devicesListBox.Items.Cast<ListBoxItem>().FirstOrDefault(x => x.BluetoothAddress == address);

                                if (existing != null)
                                {
                                    // Already listed: refresh its signal strength. A non-owner-drawn
                                    // ListBox caches each row's display string when the item is added,
                                    // so mutating the item + Invalidate() would just repaint the stale
                                    // text. Reassigning the item forces the ListBox to re-query
                                    // ToString(); the framework preserves the current selection across
                                    // the reassignment.
                                    existing.Rssi = rssi;
                                    int index = devicesListBox.Items.IndexOf(existing);
                                    devicesListBox.Items[index] = existing;
                                    AppendLog($"Updated signal strength for {formattedAddress}: {rssi} dBm");
                                }
                                else
                                {
                                    // Not listed yet: add it so it can be selected.
                                    ListBoxItem item = new ListBoxItem
                                    {
                                        Address = formattedAddress,
                                        BluetoothAddress = address,
                                        Rssi = rssi
                                    };
                                    devicesListBox.Items.Add(item);
                                    AppendLog($"Found device {formattedAddress} (signal: {rssi} dBm)");
                                }
                            }));
                        }
                    }
                };

                watcher.Start();
            }
        }

        private void SetupDialogForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (watcher != null)
            {
                watcher.Stop();
            }
        }

        private void devicesListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            deviceSelectionBtn.Enabled = devicesListBox.SelectedIndex != -1;
        }

        private void deviceSelectionBtn_Click(object sender, EventArgs e)
        {
            ListBoxItem item = devicesListBox.SelectedItem as ListBoxItem;
            pairedDeviceAddrValue.Text = item.Address;
            bleDeviceAddress = item.BluetoothAddress;
            pairedDeviceAddrValue.ForeColor = System.Drawing.Color.Green;
            AppendLog($"Selected device {item.Address}");
        }
    }

    class ListBoxItem
    {
        public string Address { get; set; }
        public ulong BluetoothAddress { get; set; }
        public short Rssi { get; set; }

        public override string ToString()
        {
            return $"{Address}  (signal: {Rssi} dBm)";
        }
    }
}
