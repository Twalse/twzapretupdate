using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using DiscordRPC;
using NHotkey;
using NHotkey.Wpf;

using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using ProgressBar = System.Windows.Controls.ProgressBar;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using Image = System.Windows.Controls.Image;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;

namespace TwZapret
{
    public class AppSettings
    {
        public string ActiveFile { get; set; } = "";
        public int ThemeIndex { get; set; } = 0;
        public int HotkeyIndex { get; set; } = 0;
        public int DiscordIndex { get; set; } = 0;
        public bool AutoStart { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
    }

    public partial class MainWindow : Window
    {
        private bool isRunning = false;
        private string activeFilePath = "";
        private string zapretFolder = "";
        private bool isAppLoaded = false;
        private string configPath = "";
        private AppSettings currentSettings = new AppSettings();

        private System.Windows.Forms.NotifyIcon notifyIcon = null!;
        private DiscordRpcClient discordClient = null!;
        private DispatcherTimer discordTimer = null!;
        private DispatcherTimer monitorTimer = null!;
        private DispatcherTimer statusTimer = null!;
        private PerformanceCounter cpuCounter = null!;
        private PerformanceCounter ramCounter = null!;

        public MainWindow()
        {
            InitializeComponent();
            SetupAppDirectories();
            SetupTrayIcon();
            InitDiscordRpc();
            InitHardwareMonitor();
            InitStatusMonitor();
            LoadSettings();
            isAppLoaded = true;

            // Trigger manual selection for first launch to ensure activeFilePath is set
            if (string.IsNullOrEmpty(activeFilePath) && StrategyCombo != null && StrategyCombo.Items.Count > 0)
            {
                StrategyCombo_SelectionChanged(StrategyCombo, null!);
            }

            if (AppTitle != null) AppTitle.Text = "TwZapret - Главная - 🏠︎";

            CheckAutostart();
        }

        private void CheckAutostart()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Contains("--autostart"))
            {
                WindowState = WindowState.Minimized;
                Hide();
                if (currentSettings.AutoStart && !string.IsNullOrEmpty(activeFilePath) && File.Exists(activeFilePath))
                {
                    try
                    {
                        string? dir = Path.GetDirectoryName(activeFilePath);
                        if (dir != null)
                        {
                            ProcessStartInfo psi = new ProcessStartInfo
                            {
                                FileName = activeFilePath,
                                WorkingDirectory = Path.GetFullPath(dir),
                                UseShellExecute = true,
                                Verb = "runas",
                                WindowStyle = ProcessWindowStyle.Hidden
                            };
                            Process.Start(psi);
                            StatusTimer_Tick(null, EventArgs.Empty);
                        }
                    }
                    catch { }
                }
            }
        }

        private void InitStatusMonitor()
        {
            statusTimer = new DispatcherTimer();
            statusTimer.Interval = TimeSpan.FromSeconds(1.5);
            statusTimer.Tick += StatusTimer_Tick;
            statusTimer.Start();
        }

        private void StatusTimer_Tick(object? sender, EventArgs e)
        {
            bool isWinwsRunning = Process.GetProcessesByName("winws").Length > 0;
            if (isWinwsRunning != isRunning)
            {
                isRunning = isWinwsRunning;
                if (ToggleBtn != null)
                {
                    if (isRunning)
                    {
                        ToggleBtn.Content = "ВЫКЛЮЧИТЬ";
                        ToggleBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#da373c"));
                    }
                    else
                    {
                        ToggleBtn.Content = "ВКЛЮЧИТЬ";
                        ToggleBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#23a559"));
                    }
                }
                DiscordTimer_Tick(null, null);
            }
        }

        private void SetupAppDirectories()
        {
            zapretFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Zapret");
            if (!Directory.Exists(zapretFolder)) Directory.CreateDirectory(zapretFolder);

            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TwZapret");
            if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);
            configPath = Path.Combine(appDataPath, "config.json");
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
                else WindowState = WindowState.Maximized;
            }
            else DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
        private void MinimizeBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void InitDiscordRpc()
        {
            discordClient = new DiscordRpcClient("1511747099002278048");
            discordClient.Initialize();
            discordTimer = new DispatcherTimer();
            discordTimer.Interval = TimeSpan.FromSeconds(5);
            discordTimer.Tick += DiscordTimer_Tick;
            discordTimer.Start();
        }

        private void DiscordTimer_Tick(object? sender, EventArgs? e)
        {
            if (discordClient == null || !discordClient.IsInitialized) return;
            string stateText = "TwZapret";
            int idx = currentSettings.DiscordIndex;
            if (idx == 0) stateText = "TwZapret Launcher";
            else if (idx == 1) stateText = "Обход DPI: Активен";
            else if (idx == 2) stateText = "В сети (Скрытый режим)";
            else if (idx == 3) stateText = "Играю в Rust";
            else if (idx == 4) stateText = "TW: Разработка";

            discordClient.SetPresence(new RichPresence()
            {
                Details = isRunning ? "Zapret: ВКЛЮЧЕН" : "Zapret: ВЫКЛЮЧЕН",
                State = stateText,
                Assets = new Assets() { LargeImageKey = "icon", LargeImageText = "TwZapret" }
            });
        }

        private void InitHardwareMonitor()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                monitorTimer = new DispatcherTimer();
                monitorTimer.Interval = TimeSpan.FromSeconds(2);
                monitorTimer.Tick += MonitorTimer_Tick;
                monitorTimer.Start();
            }
            catch { }
        }

        private void MonitorTimer_Tick(object? sender, EventArgs? e)
        {
            if (Tab4_Monitor.Visibility == Visibility.Visible && cpuCounter != null && ramCounter != null)
            {
                float cpu = cpuCounter.NextValue();
                float ram = ramCounter.NextValue();
                if (CpuText != null) CpuText.Text = $"{(int)cpu} %";
                if (CpuProgress != null) CpuProgress.Value = cpu;
                if (RamText != null) RamText.Text = $"{(int)ram} MB";
            }
        }

        private void SetupTrayIcon()
        {
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Icon = System.Drawing.SystemIcons.Shield;
            notifyIcon.Visible = true;
            notifyIcon.Text = "TwZapret";
            notifyIcon.DoubleClick += (s, args) => {
                this.Show();
                this.WindowState = WindowState.Normal;
            };
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (currentSettings.MinimizeToTray)
            {
                e.Cancel = true;
                this.Hide();
                notifyIcon.ShowBalloonTip(2000, "TwZapret", "Программа свернута в трей", System.Windows.Forms.ToolTipIcon.Info);
            }
            else
            {
                discordClient?.Dispose();
                notifyIcon.Dispose();
                if (cpuCounter != null) cpuCounter.Dispose();
                if (ramCounter != null) ramCounter.Dispose();
                base.OnClosing(e);
            }
        }

        private void ToggleAutoStart(bool enable)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (key == null) return;
                    string appName = "TwZapret";
                    if (enable)
                    {
                        string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                        if (exePath != null) key.SetValue(appName, $"\"{exePath}\" --autostart");
                    }
                    else { key.DeleteValue(appName, false); }
                }
            }
            catch { }
        }

        private void RegisterHotkey(int index)
        {
            try
            {
                HotkeyManager.Current.Remove("ToggleZapret");
                switch (index)
                {
                    case 0: HotkeyManager.Current.AddOrReplace("ToggleZapret", Key.Z, ModifierKeys.Control | ModifierKeys.Shift, OnToggleHotkey); break;
                    case 1: HotkeyManager.Current.AddOrReplace("ToggleZapret", Key.X, ModifierKeys.Control | ModifierKeys.Shift, OnToggleHotkey); break;
                    case 2: HotkeyManager.Current.AddOrReplace("ToggleZapret", Key.Z, ModifierKeys.Alt, OnToggleHotkey); break;
                    case 3: HotkeyManager.Current.AddOrReplace("ToggleZapret", Key.F9, ModifierKeys.None, OnToggleHotkey); break;
                }
            }
            catch { }
        }

        private void OnToggleHotkey(object? sender, HotkeyEventArgs e)
        {
            ToggleBtn_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }

        private void LoadSettings()
        {
            if (File.Exists(configPath))
            {
                try
                {
                    AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(configPath));
                    if (loaded != null) currentSettings = loaded;
                    activeFilePath = currentSettings.ActiveFile;
                    if (ThemeCombo != null) ThemeCombo.SelectedIndex = currentSettings.ThemeIndex;
                    if (DiscordCombo != null) DiscordCombo.SelectedIndex = currentSettings.DiscordIndex;
                    if (HotkeyCombo != null) HotkeyCombo.SelectedIndex = currentSettings.HotkeyIndex;
                    if (AutoStartCheck != null) AutoStartCheck.IsChecked = currentSettings.AutoStart;
                    if (MinimizeTrayCheck != null) MinimizeTrayCheck.IsChecked = currentSettings.MinimizeToTray;
                    ToggleAutoStart(currentSettings.AutoStart);
                    RegisterHotkey(currentSettings.HotkeyIndex);
                    UpdateFilePathUI();
                    RefreshFolderFiles();
                }
                catch { }
            }
            else
            {
                if (ThemeCombo != null) ThemeCombo.SelectedIndex = 0;
                if (DiscordCombo != null) DiscordCombo.SelectedIndex = 0;
                if (HotkeyCombo != null) HotkeyCombo.SelectedIndex = 0;
                if (AutoStartCheck != null) AutoStartCheck.IsChecked = false;
                if (MinimizeTrayCheck != null) MinimizeTrayCheck.IsChecked = true;
                RegisterHotkey(0);
                RefreshFolderFiles();
            }
        }

        private void SaveSettings()
        {
            if (!isAppLoaded) return;
            currentSettings.ActiveFile = activeFilePath;
            if (ThemeCombo != null) currentSettings.ThemeIndex = ThemeCombo.SelectedIndex;
            if (DiscordCombo != null) currentSettings.DiscordIndex = DiscordCombo.SelectedIndex;
            if (HotkeyCombo != null) currentSettings.HotkeyIndex = HotkeyCombo.SelectedIndex;
            if (AutoStartCheck != null) currentSettings.AutoStart = AutoStartCheck.IsChecked ?? false;
            if (MinimizeTrayCheck != null) currentSettings.MinimizeToTray = MinimizeTrayCheck.IsChecked ?? true;
            ToggleAutoStart(currentSettings.AutoStart);
            RegisterHotkey(currentSettings.HotkeyIndex);
            DiscordTimer_Tick(null, null);
            File.WriteAllText(configPath, JsonSerializer.Serialize(currentSettings));
        }

        private void SettingsCheck_Changed(object sender, RoutedEventArgs e) => SaveSettings();
        private void HotkeyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SaveSettings();
        private void DiscordCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SaveSettings();

        private void ToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(activeFilePath) || !File.Exists(activeFilePath))
            {
                MessageBox.Show("Сначала выберите файл из списка на Вкладке 5!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!isRunning)
            {
                try
                {
                    string? dir = Path.GetDirectoryName(activeFilePath);
                    if (dir == null) return;
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = activeFilePath,
                        WorkingDirectory = Path.GetFullPath(dir),
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Normal
                    };
                    Process.Start(psi);
                    StatusTimer_Tick(null, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка");
                }
            }
            else
            {
                try
                {
                    ProcessStartInfo killPsi = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = "/F /IM winws.exe /T",
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(killPsi);
                    StatusTimer_Tick(null, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка остановки: {ex.Message}", "Ошибка");
                }
            }
        }

        private void PingPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isAppLoaded || PingAddressInput == null || PingPresetCombo == null) return;
            int idx = PingPresetCombo.SelectedIndex;
            PingAddressInput.IsReadOnly = true;
            if (idx == 0) { PingAddressInput.IsReadOnly = false; }
            else if (idx == 1) PingAddressInput.Text = "8.8.8.8";
            else if (idx == 2) PingAddressInput.Text = "1.1.1.1";
            else if (idx == 3) PingAddressInput.Text = "youtube.com";
            else if (idx == 4) PingAddressInput.Text = "discord.com";
            else if (idx == 5) PingAddressInput.Text = "steampowered.com";
        }

        private async void PingBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PingAddressInput == null || PingResultText == null || PingBtn == null) return;
            string address = PingAddressInput.Text;
            if (string.IsNullOrWhiteSpace(address)) return;
            PingResultText.Text = $"Пингуем {address}...\n";
            PingBtn.IsEnabled = false;
            try
            {
                Ping pingSender = new Ping();
                for (int i = 0; i < 4; i++)
                {
                    PingReply reply = await pingSender.SendPingAsync(address, 2000);
                    if (reply.Status == IPStatus.Success) { PingResultText.Text += $"Ответ от {reply.Address}: время={reply.RoundtripTime}мс TTL={reply.Options?.Ttl ?? 0}\n"; }
                    else { PingResultText.Text += $"Превышен интервал ожидания.\n"; }
                }
            }
            catch { PingResultText.Text += $"\nОшибка: Не удалось проверить узел."; }
            PingBtn.IsEnabled = true;
        }

        private void FlushDnsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c ipconfig /flushdns") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi);
                if (PingResultText != null) PingResultText.Text = "Кэш DNS успешно очищен!";
            }
            catch (Exception ex) { if (PingResultText != null) PingResultText.Text = $"Ошибка очистки DNS: {ex.Message}"; }
        }

        private void XboxDnsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                          (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                                          ni.GetIPProperties().GatewayAddresses.Any());

                if (activeInterface == null)
                {
                    if (PingResultText != null) PingResultText.Text = "Ошибка: Активная сеть не найдена.";
                    return;
                }

                string adapterName = activeInterface.Name;
                string cmdArgs = $"/c netsh interface ipv4 set dns name=\"{adapterName}\" static 111.88.96.50 & netsh interface ipv4 add dns name=\"{adapterName}\" 111.88.96.51 index=2 & ipconfig /flushdns";

                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmdArgs)
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);

                if (PingResultText != null) PingResultText.Text = $"Xbox DNS (111.88.96.50 / 111.88.96.51) успешно установлен для адаптера: {adapterName}";
            }
            catch (Exception ex)
            {
                if (PingResultText != null) PingResultText.Text = $"Ошибка установки DNS: {ex.Message}";
            }
        }

        private void RefreshFolderFiles()
        {
            if (FilesListPanel == null) return;
            FilesListPanel.Children.Clear();
            var strategyItems = new System.Collections.Generic.Dictionary<string, string>();
            if (StrategyCombo != null) StrategyCombo.ItemsSource = null;

            if (string.IsNullOrEmpty(zapretFolder) || !Directory.Exists(zapretFolder))
            {
                FilesListPanel.Children.Add(new TextBlock { Text = "Создайте папку Zapret рядом с exe и положите туда скрипты.", Foreground = Brushes.Gray, Margin = new Thickness(10) });
                return;
            }
            string[] files = Directory.GetFiles(zapretFolder, "*.*").Where(s => s.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (files.Length == 0)
            {
                FilesListPanel.Children.Add(new TextBlock { Text = "Скрипты .bat или .cmd не найдены в папке Zapret.", Foreground = Brushes.Gray, Margin = new Thickness(10) });
                return;
            }

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                strategyItems.Add(fileName, file);

                Border itemBorder = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1e1f22")), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(15) };
                Grid itemGrid = new Grid();
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                TextBlock nameText = new TextBlock { Text = fileName, Foreground = Brushes.White, FontSize = 16, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold };
                Grid.SetColumn(nameText, 0);
                Button selectBtn = new Button { Content = "Выбрать", Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5865F2")), Foreground = Brushes.White, Padding = new Thickness(15, 5, 15, 5), Cursor = Cursors.Hand };
                selectBtn.Click += (s, e) => { activeFilePath = file; UpdateFilePathUI(); SaveSettings(); if (FindName("Tab1Btn") is Button t1) Tab_Click(t1, new RoutedEventArgs()); };
                Grid.SetColumn(selectBtn, 1);
                itemGrid.Children.Add(nameText);
                itemGrid.Children.Add(selectBtn);
                itemBorder.Child = itemGrid;
                FilesListPanel.Children.Add(itemBorder);
            }

            if (StrategyCombo != null)
            {
                StrategyCombo.ItemsSource = strategyItems;
                if (!string.IsNullOrEmpty(activeFilePath) && strategyItems.ContainsValue(activeFilePath))
                {
                    StrategyCombo.SelectedValue = activeFilePath;
                }
                else if (strategyItems.Count > 0)
                {
                    StrategyCombo.SelectedIndex = 0;
                }
            }
        }

        private void UpdateFilePathUI()
        {
            if (!string.IsNullOrEmpty(activeFilePath))
            {
                if (FilePathText != null) FilePathText.Text = $"Путь: Встроенная папка Zapret";
                if (StrategyCombo != null && StrategyCombo.SelectedValue as string != activeFilePath)
                {
                    StrategyCombo.SelectedValue = activeFilePath;
                }
            }
        }

        private void StrategyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isAppLoaded || StrategyCombo == null) return;
            if (StrategyCombo.SelectedValue is string selectedPath)
            {
                activeFilePath = selectedPath;
                UpdateFilePathUI();
                SaveSettings();
            }
        }

        private void Tab_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button clickedBtn) return;
            string? tag = clickedBtn.Tag?.ToString();
            if (tag == null || MainGrid == null) return;
            foreach (var child in MainGrid.Children) { if (child is Grid grid && grid.Name.StartsWith("Tab")) { grid.Visibility = Visibility.Hidden; } }
            switch (tag) {
                case "1":
                    if (FindName("Tab1_Main") is Grid t1) t1.Visibility = Visibility.Visible;
                    if (AppTitle != null) AppTitle.Text = "TwZapret - Главная - 🏠︎";
                    break;
                case "2":
                    if (FindName("Tab2_VPN") is Grid t2) t2.Visibility = Visibility.Visible;
                    if (AppTitle != null) AppTitle.Text = "TwZapret - VPN - 🌐";
                    break;
                case "3":
                    if (FindName("Tab3_Ping") is Grid t3) t3.Visibility = Visibility.Visible;
                    if (AppTitle != null) AppTitle.Text = "TwZapret - Сеть - 📡";
                    break;
                case "4":
                    if (FindName("Tab4_Monitor") is Grid t4) t4.Visibility = Visibility.Visible;
                    if (AppTitle != null) AppTitle.Text = "TwZapret - Мониторинг - 📊";
                    break;
                case "5":
                    if (FindName("Tab5_Hub") is Grid t5) t5.Visibility = Visibility.Visible;
                    if (AppTitle != null) AppTitle.Text = "TwZapret - Файлы - 📁";
                    break;
                case "6":
                    if (FindName("Tab6_Settings") is Grid t6) t6.Visibility = Visibility.Visible;
                    if (AppTitle != null) AppTitle.Text = "TwZapret - Настройки - ⚙️";
                    break;
            }
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isAppLoaded || MainGrid == null || ThemeCombo == null) return;
            LinearGradientBrush newBrush = new LinearGradientBrush();
            newBrush.StartPoint = new Point(0, 0);
            newBrush.EndPoint = new Point(1, 1);
            int index = ThemeCombo.SelectedIndex;
            if (index == 0) { newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0b0c10"), 0.0)); newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1f2833"), 0.5)); newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#450a5c"), 1.0)); }
            else if (index == 1) { newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#111214"), 0.0)); newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1e1f22"), 1.0)); }
            else if (index == 2) { newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1e1f22"), 0.0)); newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#132a1e"), 1.0)); }
            else if (index == 3) { newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1e1f22"), 0.0)); newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2b1b3d"), 1.0)); }
            else if (index == 4) { newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0f2027"), 0.0)); newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#203a43"), 0.5)); newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2c5364"), 1.0)); }
            else if (index == 5) { newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3a1c71"), 0.0)); newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#d76d77"), 0.5)); newBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#ffaf7b"), 1.0)); }
            MainGrid.Background = newBrush;
            SaveSettings();
        }
    }
}