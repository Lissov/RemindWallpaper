using System;
using System.Linq;
using System.Windows.Forms;

namespace RemindWallpaper
{
    public partial class SetupForm : Form
    {
        private readonly WallpaperChanger _changer;

        public SetupForm(WallpaperChanger changer)
        {
            _changer = changer;
            InitializeComponent();
        }

        private void SetupForm_Load(object sender, EventArgs e)
        {
            ShowData();
            _changer.Updated += (s, v) => ShowData();
        }

        private void ShowData() {
            btnStart.Enabled = _changer.CanStart && !_changer.IsRunning;
            btnStop.Enabled = _changer.IsRunning;
            tbPaths.Lines = _changer.PhotosPaths;
            tbExcludeRegex.Text = _changer.ExcludeRegex;
            nmInterval.Value = Math.Round(_changer.Interval / 1000m);
            tbStatus.Text = _changer.IsRunning ? "Running" : "Stopped";
            btnStart.Enabled = _changer.CanStart;
            btnStop.Enabled = _changer.IsRunning;
            lbCountAvailable.Text = _changer.AvailableToShow.ToString();
            tbDisplayed.Text = _changer.NowShowing;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _changer.PhotosPaths = tbPaths.Lines
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToArray();
            _changer.ExcludeRegex = tbExcludeRegex.Text;
            _changer.Interval = (int)Math.Round(nmInterval.Value) * 1000;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            _changer.Start();
            //ShowData();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _changer.Stop();
            //ShowData();
        }
    }
}
