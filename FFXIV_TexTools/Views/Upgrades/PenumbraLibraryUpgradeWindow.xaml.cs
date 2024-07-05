﻿using FFXIV_TexTools.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WK.Libraries.BetterFolderBrowserNS;
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods.DataContainers;

namespace FFXIV_TexTools.Views.Upgrades
{
    /// <summary>
    /// Interaction logic for PenumbraLibraryUpgradeWindow.xaml
    /// </summary>
    public partial class PenumbraLibraryUpgradeWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _PenumbraPath;
        public string PenumbraPath
        {
            get => _PenumbraPath;
            set
            {
                _PenumbraPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PenumbraPath)));
            }
        }

        private string _DestinationPath;
        public string DestinationPath
        {
            get => _DestinationPath;
            set
            {
                _DestinationPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DestinationPath)));
            }
        }

        private string _ContinuePauseText;
        public string ContinuePauseText
        {
            get => _ContinuePauseText;
            set
            {
                _ContinuePauseText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContinuePauseText)));
            }
        }

        private string _StatusText;
        public string StatusText
        {
            get => _StatusText;
            set
            {
                _StatusText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            }
        }
        private bool _PathEnabled;
        public bool PathEnabled
        {
            get => _PathEnabled;
            set
            {
                _PathEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PathEnabled)));
            }
        }
        private bool _ContinuePauseEnabled;
        public bool ContinuePauseEnabled
        {
            get => _ContinuePauseEnabled;
            set
            {
                _ContinuePauseEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContinuePauseEnabled)));
            }
        }

        PenumbraUpgradeStatus Results;


        const string _JsonName = "upgrade_status.json";
        string JsonPath {
            get
            {
                if (string.IsNullOrEmpty(DestinationPath)) return "";
                return Path.GetFullPath(Path.Combine(DestinationPath, _JsonName));
            }
        }

        public ObservableCollection<KeyValuePair<string, string>> RemainingMods { get; set; } = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> ProcessedMods { get; set; } = new ObservableCollection<KeyValuePair<string, string>>();

        public enum UpgradeState
        {
            Stopped,
            Working,
            Paused,
            Completed
        };


        private UpgradeState _State;
        public UpgradeState State {
            get => _State;
            set
            {
                _State = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));

                if(State == UpgradeState.Stopped)
                {
                    ContinuePauseText = "Start";
                    PathEnabled = true;
                } else if(State == UpgradeState.Paused)
                {
                    ContinuePauseText = "Resume";
                    PathEnabled = true;
                }
                else if (State == UpgradeState.Working)
                {
                    ContinuePauseText = "Pause";
                    PathEnabled = false;
                }
                else if (State == UpgradeState.Completed)
                {
                    ContinuePauseText = "Complete";
                    ContinuePauseEnabled = false;
                    PathEnabled = false;
                }
            }
        }

        public PenumbraLibraryUpgradeWindow()
        {
            DataContext = this;
            InitializeComponent();

            State = UpgradeState.Stopped;
            Closing += PenumbraLibraryUpgradeWindow_Closing;
            CheckPaths();
        }

        private void PenumbraLibraryUpgradeWindow_Closing(object sender, CancelEventArgs e)
        {
            Owner?.Activate();
        }

        private async void ContinuePause_Click(object sender, RoutedEventArgs e)
        {
            if(State == UpgradeState.Working)
            {
                await Stop();
            } else if (State == UpgradeState.Stopped || State == UpgradeState.Paused)
            {
                await Start();
            }
        }

        private void SelectPenumbraPath_Click(object sender, RoutedEventArgs e)
        {
            var bfb = new BetterFolderBrowser() { Title = "Select Penumbra Library..." };
            if(bfb.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            PenumbraPath = bfb.SelectedFolder;
            CheckPaths();
        }

        private void SelectDestinationPath_Click(object sender, RoutedEventArgs e)
        {
            var bfb = new BetterFolderBrowser() { Title = "Select Destination Folder..." };
            if (bfb.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            DestinationPath = bfb.SelectedFolder;
            CheckPaths();
        }

        private void CheckPaths()
        {
            if (string.IsNullOrWhiteSpace(PenumbraPath) || string.IsNullOrWhiteSpace(DestinationPath)
                || PenumbraPath.Contains(DestinationPath + Path.DirectorySeparatorChar) || DestinationPath.Contains(PenumbraPath + Path.DirectorySeparatorChar)
                || DestinationPath == PenumbraPath || !Directory.Exists(PenumbraPath))
            {
                ContinuePauseEnabled = false;
                Results = null;
                return;
            }

            ContinuePauseEnabled = true;

            Directory.CreateDirectory(DestinationPath);

            var di = new DirectoryInfo(DestinationPath);

            LoadOrCreateJson();
            UpdateLists();
        }

        private void LoadOrCreateJson()
        {

            if (File.Exists(JsonPath))
            {
                try
                {
                    Results = JsonConvert.DeserializeObject<PenumbraUpgradeStatus>(File.ReadAllText(JsonPath));
                }
                catch
                {
                    Results = new PenumbraUpgradeStatus();
                }
            }
            else
            {
                Results = new PenumbraUpgradeStatus();
            }

            var di = new DirectoryInfo(PenumbraPath);
            var children = di.EnumerateDirectories().ToList();

            foreach(var c in children)
            {
                if (!Results.Upgrades.ContainsKey(c.Name))
                {
                    var metaPath = Path.GetFullPath(Path.Combine(c.FullName, "meta.json"));
                    if (File.Exists(metaPath))
                    {
                        Results.Upgrades.Add(c.Name, PenumbraUpgradeStatus.EUpgradeResult.NotStarted);
                    }
                }
            }

            if (Results.Upgrades.Any(x => x.Value != PenumbraUpgradeStatus.EUpgradeResult.NotStarted))
            {
                State = UpgradeState.Paused;
            } else
            {
                State = UpgradeState.Stopped;
            }

            File.WriteAllText(JsonPath, JsonConvert.SerializeObject(Results));
        }

        private void SaveJson()
        {
            if (Results == null || string.IsNullOrEmpty(DestinationPath)) return;
            File.WriteAllText(JsonPath, JsonConvert.SerializeObject(Results));
        }
        private void UpdateLists()
        {
            RemainingMods.Clear();
            ProcessedMods.Clear();

            if (Results == null) return;

            foreach(var m in Results.Upgrades)
            {
                if(m.Value == PenumbraUpgradeStatus.EUpgradeResult.NotStarted
                    || m.Value == PenumbraUpgradeStatus.EUpgradeResult.InProgress)
                {
                    RemainingMods.Add(new KeyValuePair<string, string>(m.Key, m.Key));
                } else if (m.Value == PenumbraUpgradeStatus.EUpgradeResult.Success)
                {
                    ProcessedMods.Add(new KeyValuePair<string, string>("✓ " + m.Key, m.Key));
                } else if(m.Value == PenumbraUpgradeStatus.EUpgradeResult.Failure)
                {
                    ProcessedMods.Add(new KeyValuePair<string, string>("𝑿 " + m.Key, m.Key));
                }
            }

            Trace.WriteLine("ASDF");
        }


        private bool _RequestStop = false;
        private async Task Start()
        {
            if (Results == null) return;
            State = UpgradeState.Working;
            _RequestStop = false;
            ContinuePauseEnabled = true;

            await Task.Run(async () =>
            {
                var workerState = XivCache.CacheWorkerEnabled;
                await XivCache.SetCacheWorkerState(false);
                var nextMod = Results.Upgrades.FirstOrDefault(x => x.Value == PenumbraUpgradeStatus.EUpgradeResult.NotStarted);
                while (!string.IsNullOrWhiteSpace(nextMod.Key) && !_RequestStop)
                {
                    var mod = nextMod.Key;
                    Dispatcher.Invoke(() =>
                    {
                        StatusText = "Processing Mod: " + mod;
                    });

                    var res = await Results.ProcessMod(PenumbraPath, DestinationPath, mod);

                    // Always clear the temp folder between actions.
                    IOUtil.ClearTempFolder();

                    SaveJson();

                    Dispatcher.Invoke(() =>
                    {
                        UpdateLists();
                    });
                    nextMod = Results.Upgrades.FirstOrDefault(x => x.Value == PenumbraUpgradeStatus.EUpgradeResult.NotStarted);
                }

                if (_RequestStop)
                {
                    State = UpgradeState.Paused;
                    ContinuePauseEnabled = true;
                } else
                {
                    State = UpgradeState.Completed;
                }

                await XivCache.SetCacheWorkerState(workerState);
            });
        }
        private async Task Stop()
        {
            if (Results == null) return;
            _RequestStop = true;
            ContinuePauseEnabled = false;
        }


    }
}
