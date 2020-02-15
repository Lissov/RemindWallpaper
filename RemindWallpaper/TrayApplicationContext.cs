using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using RemindWallpaper.Properties;

namespace RemindWallpaper
{
    public class TrayApplicationContext : ApplicationContext
    {
        private System.ComponentModel.Container _components;
        private NotifyIcon _notifyIcon;
        private WallpaperChanger changer;
        
        public TrayApplicationContext()
        {
            InitializeContext();
            changer = new WallpaperChanger();
            if (changer.Configured)
                changer.Start();
            else
                ShowSetupForm();
        }

        private void InitializeContext()
        {
            _components = new System.ComponentModel.Container();
            _notifyIcon = new NotifyIcon(_components)
            {
                ContextMenuStrip = new ContextMenuStrip(),
                Icon = Resources.Tray,
                Text = Resources.TrayText,
                Visible = true
            };
            BuildContextMenu();
            _notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;
            _notifyIcon.DoubleClick += notifyIcon_DoubleClick;
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowSetupForm();
        }

        private ToolStripItem _startMenuItem;
        private ToolStripItem _stopMenuItem;
        private ToolStripItem _skipMenuItem;
        private ToolStripItem _startStopSeparator;
        private ToolStripItem _setupMenuItem;
        private SetupForm _setupForm = null;

        private void BuildContextMenu()
        {
            _notifyIcon.ContextMenuStrip.Items.Add(
                _startMenuItem = new ToolStripMenuItem(Resources.Tray_Start, null, (a, b) => changer.Start()));
            _notifyIcon.ContextMenuStrip.Items.Add(
                _stopMenuItem = new ToolStripMenuItem(Resources.Tray_Stop, null, (a, b) => changer.Stop()));
            _notifyIcon.ContextMenuStrip.Items.Add(
                _skipMenuItem = new ToolStripMenuItem(Resources.Tray_Skip, null, (a, b) => changer.Start()));

            _notifyIcon.ContextMenuStrip.Items.Add(_startStopSeparator = new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add(
                _setupMenuItem = new ToolStripMenuItem(Resources.Tray_Setup, null, TraySetup_Click));
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add(
                new ToolStripMenuItem(Resources.Tray_Close, null, TrayClose_Click));
        }

        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = false;

            bool showStart = changer.CanStart && !changer.IsRunning;
            bool showStop = changer.IsRunning;

            _startMenuItem.Visible = showStart;
            _stopMenuItem.Visible = showStop;
            _skipMenuItem.Visible = showStop;
            _startStopSeparator.Visible = showStart || showStop;

            var f = new Font(_setupMenuItem.Font, 
                (!showStart && !showStop) ? FontStyle.Bold : FontStyle.Regular);
            _setupMenuItem.Font = f;
        }

        private void TraySetup_Click(object sender, EventArgs e)
        {
            ShowSetupForm();
        }

        private void ShowSetupForm()
        {
            if (_setupForm == null)
            {
                _setupForm = new SetupForm(changer);
                _setupForm.Closed += (e, v) => _setupForm = null;
                _setupForm.Show();
            }
            else
            {
                _setupForm.Activate();
            }
        }

        private void TrayClose_Click(object sender, EventArgs e)
        {
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _components?.Dispose(); }
        }

        protected override void ExitThreadCore()
        {
            _setupForm?.Close();
            _notifyIcon.Visible = false;
            base.ExitThreadCore();
        }
    }
}
