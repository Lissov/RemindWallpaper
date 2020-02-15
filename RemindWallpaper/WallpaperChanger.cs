using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RemindWallpaper
{
    public class WallpaperChanger
    {
        [DllImport("User32", CharSet = CharSet.Auto)]
        public static extern int SystemParametersInfo(int uiAction, int uiParam, string pvParam, uint fWinIni);

        public WallpaperChanger()
        {
            _running = false;
            _photosPaths = (ConfigurationManager.AppSettings.Get("PhotosPaths") ?? "")
                .Split(new [] {';'}, StringSplitOptions.RemoveEmptyEntries);
            var s = ConfigurationManager.AppSettings.Get("ExcludeRegex");
            if (!string.IsNullOrEmpty(s)) _excludeRegex = new Regex(s);
            if (int.TryParse(ConfigurationManager.AppSettings.Get("Interval"), out var i))
                _interval = i;
            _rand = new Random(DateTime.Now.Millisecond);
            LoadPhotos();
        }

        private string[] _photosPaths;
        private Regex _excludeRegex;
        private readonly List<PhotoFolder> _folders = new List<PhotoFolder>();
        private readonly object _foldersLock = new object();
        private bool _running;
        private CancellationTokenSource _currentCancellation;
        private readonly Random _rand;

        public int AvailableToShow { get; set; }
        public string NowShowing { get; set; }
        public event EventHandler Updated;

        private int _interval = 30000;
        public int Interval
        {
            get => _interval;
            set
            {
                _interval = value;
                SaveConfigs();
            }
        }

        public string[] PhotosPaths
        {
            get => _photosPaths;
            set
            {
                _photosPaths = value;
                SaveConfigs();
                LoadPhotos();
            }
        }

        public string ExcludeRegex
        {
            get => _excludeRegex?.ToString();
            set
            {
                if (value == _excludeRegex?.ToString()
                    || (string.IsNullOrEmpty(value) && _excludeRegex == null))
                {
                    return;
                }
                _excludeRegex = !string.IsNullOrEmpty(value) ? new Regex(value) : null;
                SaveConfigs();
                LoadPhotos(forceReload: true);
            }
        }

        private IEnumerable<FileInfo> GetPhotos()
        {
            return _folders.SelectMany(f => f.Files);
        }

        public void Execution(CancellationToken cancellation)
        {
            _running = true;
            Task.Run(() =>
            {
                while (!cancellation.IsCancellationRequested)
                {
                    var available = GetPhotos().ToList();
                    AvailableToShow = available.Count;
                    if (available.Any())
                    {
                        var num = _rand.Next(available.Count);
                        var photoPath = available[num].FullName;
                        SystemParametersInfo(0x0014, 0, photoPath, 0x0001);
                        NowShowing = photoPath;
                        Updated?.Invoke(this, null);
                    }
                    CheckSwitchPhotoset();
                    Thread.Sleep(_interval);
                }
            }, cancellation);
        }

        private void CheckSwitchPhotoset()
        {
            var files = GetPhotos().ToList();
            if (files.Any(f => !Matches(f)))
            {
                if (!files.Any(Matches))
                {
                    // no photos from proper folder, ask to load 
                    LoadPhotos(false);
                }
                else
                {
                    // some proper photos already there, clear invalid
                    lock (_foldersLock)
                    {
                        _folders.ForEach(f => f.Files.RemoveAll(fi => !Matches(fi)));
                        _folders.RemoveAll(f => !f.Files.Any());
                    }
                }
            }
        }

        private void SaveConfigs()
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            AppSettingsSection app = config.AppSettings;
            app.Settings["PhotosPaths"].Value = string.Join(";", _photosPaths);
            app.Settings["ExcludeRegex"].Value = ExcludeRegex ?? "";
            app.Settings["Interval"].Value = _interval.ToString();
            config.Save(ConfigurationSaveMode.Modified);
        }

        public void Start()
        {
            _currentCancellation?.CancelAfter(1);

            _running = true;
            _currentCancellation = new CancellationTokenSource();
            Execution(_currentCancellation.Token);
            Updated?.Invoke(this, null);
        }

        public void Stop()
        {
            _currentCancellation.CancelAfter(1);
            _running = false;
            Updated?.Invoke(this, null);
        }

        public bool CanStart => Configured && GetPhotos().Any();
        public bool Configured => PhotosPaths.Any();

        private bool Matches(FileInfo fileInfo)
        {
            var m = DateTime.Now.Month.ToString();
            if (m.Length == 1) m = "0" + m;
            return fileInfo.FullName.Contains($"{DateTime.Now.Year - 1}_{m}");
        }

        public bool IsRunning => _running;
        private Guid _runId;
        private void LoadPhotos(bool forceReload = false)
        {
            lock (_foldersLock)
            {
                _runId = Guid.NewGuid();
                _folders.RemoveAll(f => forceReload || !_photosPaths.Contains(f.Path));
            }

            foreach (var path in _photosPaths)
            {
                var thisRun = _runId;
                Task.Run(() =>
                {
                    var files = new DirectoryInfo(path).GetFiles("*.jpg", SearchOption.AllDirectories)
                        //.Where(f => f.CreationTime.Year == (DateTime.Now.Year-1) && f.CreationTime.Month == DateTime.Now.Month)
                        .Where(Matches)
                        .Where(f => !(_excludeRegex?.IsMatch(f.FullName.ToLower()) ?? false))
                        .ToList();
                    lock (_foldersLock)
                    {
                        if (thisRun == _runId) // otherwize the task is "cancelled"
                        {
                            var f = _folders.SingleOrDefault(a => a.Path == path);
                            if (f == null)
                            {
                                f = new PhotoFolder { Path = path };
                                _folders.Add(f);
                            }

                            f.Files = files;
                        }
                    }
                });
            }
        }

        private class PhotoFolder
        {
            public string Path { get; set; }
            public List<FileInfo> Files { get; set; }
        }
    }
}
