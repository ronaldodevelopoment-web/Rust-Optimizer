using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace RustOptimizer
{
    public partial class MainWindow : Window
    {
        private string _rustInstallPath = string.Empty;
        private string _currentLang = "RU";
        private bool _isLoadingFinished;
        private string _cpuName = "Не определён";
        private string _gpuName = "Не определена";
        private int _ramGb = 8;
        private readonly int _cpuThreads = Environment.ProcessorCount;

        private const string OriginalBackupName = "client.rustoptimizer.original.cfg";
        private const string MissingConfigMarkerName = "client.rustoptimizer.config_was_missing";

        public MainWindow()
        {
            InitializeComponent();
            LoadCustomFontFromRoot();
            ApplyLanguage("RU");
            Loaded += MainWindow_Loaded;
        }

        private void LoadCustomFontFromRoot()
        {
            try
            {
                string fontFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Font.otf");
                if (!File.Exists(fontFile)) return;

                var glyphTypeface = new GlyphTypeface(new Uri(fontFile, UriKind.Absolute));
                string familyName = glyphTypeface.FamilyNames.TryGetValue(
                    System.Globalization.CultureInfo.GetCultureInfo("en-US"), out string englishName)
                    ? englishName
                    : (glyphTypeface.FamilyNames.Count > 0 ? new List<string>(glyphTypeface.FamilyNames.Values)[0] : "Segoe UI");
                FontFamily = new FontFamily(new Uri(fontFile, UriKind.Absolute), $"./#{familyName}");
            }
            catch
            {
                // Пользовательский шрифт необязателен: продолжаем со стандартным шрифтом Windows.
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await ScanSystemAsync();
        }

        private async Task ScanSystemAsync()
        {
            TxtStep1Title.Text = _currentLang == "RU" ? "СКАНИРОВАНИЕ СИСТЕМЫ" : "SCANNING SYSTEM";
            TxtStep1Subtitle.Text = _currentLang == "RU"
                ? "Поиск Rust и чтение характеристик ПК..."
                : "Searching for Rust and reading PC hardware...";
            TxtPercent.Text = "25%";
            ProgressFill.Width = 105;

            Task<string> pathTask = Task.Run(FindRustDirectory);
            Task hardwareTask = Task.Run(DetectPcHardware);

            await Task.WhenAll(pathTask, hardwareTask);
            _rustInstallPath = pathTask.Result;

            TxtPercent.Text = "100%";
            ProgressFill.Width = 420;
            _isLoadingFinished = true;
            LoadingPanel.Visibility = Visibility.Collapsed;
            FoundPathCard.Visibility = Visibility.Visible;
            BtnContinueToOpt.Visibility = Visibility.Visible;

            if (!string.IsNullOrWhiteSpace(_rustInstallPath))
            {
                CenterVectorIcon.Data = Geometry.Parse("M6,22 L17,33 L38,10");
                CenterVectorIcon.Stroke = new SolidColorBrush(Color.FromRgb(255, 42, 56));
                CenterVectorIcon.StrokeThickness = 4.5;
                CenterVectorIcon.Fill = Brushes.Transparent;
                TxtRustPath.Text = _rustInstallPath;
                BtnContinueToOpt.IsEnabled = true;
            }
            else
            {
                TxtRustPath.Text = _currentLang == "RU"
                    ? "Rust не найден автоматически. Нажмите «Изменить...» и выберите RustClient.exe."
                    : "Rust was not found automatically. Click Change and select RustClient.exe.";
                BtnContinueToOpt.IsEnabled = false;
            }

            ApplyLanguage(_currentLang);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && !(e.OriginalSource is ButtonBase))
            {
                try { DragMove(); } catch { }
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void BtnDonate_Click(object sender, RoutedEventArgs e)
            => OpenUrl("https://www.donationalerts.com/r/melsaqadayt");

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    _currentLang == "RU" ? $"Не удалось открыть ссылку.\n{ex.Message}" : $"Could not open the link.\n{ex.Message}",
                    "Rust Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Resolve this optional control by name rather than a generated field.
        // This also prevents CS0103 if an older XAML file is temporarily used in the project.
        private System.Windows.Controls.Button GetVerifySettingsButton()
        {
            return FindName("BtnVerifySettings") as System.Windows.Controls.Button;
        }

        private void SetVerifyButtonContent(string content)
        {
            System.Windows.Controls.Button button = GetVerifySettingsButton();
            if (button != null) button.Content = content;
        }

        private void SetVerifyButtonEnabled(bool enabled)
        {
            System.Windows.Controls.Button button = GetVerifySettingsButton();
            if (button != null) button.IsEnabled = enabled;
        }

        #region Language
        private void BtnLangRu_Click(object sender, RoutedEventArgs e) => ApplyLanguage("RU");
        private void BtnLangEn_Click(object sender, RoutedEventArgs e) => ApplyLanguage("EN");

        private void ApplyLanguage(string lang)
        {
            _currentLang = lang;
            BtnLangRu.BorderBrush = new SolidColorBrush(lang == "RU" ? Color.FromRgb(255, 42, 56) : Color.FromRgb(61, 21, 26));
            BtnLangEn.BorderBrush = new SolidColorBrush(lang == "EN" ? Color.FromRgb(255, 42, 56) : Color.FromRgb(61, 21, 26));

            if (lang == "RU")
            {
                if (!_isLoadingFinished)
                {
                    TxtStep1Title.Text = "СКАНИРОВАНИЕ СИСТЕМЫ";
                    TxtStep1Subtitle.Text = "Поиск установленной игры Rust и анализ характеристик ПК...";
                }
                else
                {
                    TxtStep1Title.Text = string.IsNullOrEmpty(_rustInstallPath) ? "ИГРА НЕ НАЙДЕНА" : "ИГРА RUST ОБНАРУЖЕНА";
                    TxtStep1Subtitle.Text = string.IsNullOrEmpty(_rustInstallPath)
                        ? "Укажите папку с установленной игрой Rust вручную ниже."
                        : "Путь к игре определён. Нажмите «Продолжить», чтобы настроить client.cfg.";
                }

                TxtFoundLabel.Text = "ПАПКА ИГРЫ RUST:";
                BtnBrowseManual.Content = "Изменить...";
                BtnContinueToOpt.Content = "ПРОДОЛЖИТЬ";
                BtnBack.Content = "← Назад";
                TxtHwTitle.Text = "ХАРАКТЕРИСТИКИ ВАШЕГО ПК";
                TxtFpsTitle.Text = "ВЫБЕРИТЕ ЛИМИТ FPS";
                TxtFpsDesc.Text = "Это верхний лимит, а не обещание прироста FPS. Результат зависит от ПК и сервера.";
                RbFps30.Tag = "Меньше нагрузки в игре";
                RbFps60.Tag = "Сбалансированный режим";
                RbFps120.Tag = "Для монитора 144 Гц";
                RbFps240.Tag = "Максимальный лимит Rust";
                TxtFixedSettingsTitle.Text = "ФИКСИРОВАННЫЕ НАСТРОЙКИ";
                TxtFixedSettingsSub.Text = "Музыка: 0%  ·  FOV: 90  ·  VSync: OFF  ·  FPS в меню: 60";
                BtnApplyOptimization.Content = "ПРИМЕНИТЬ ОПТИМИЗАЦИЮ";
                BtnRestoreBackup.Content = "ВОССТАНОВИТЬ ИСХОДНЫЙ КОНФИГ";
                SetVerifyButtonContent("ПРОВЕРИТЬ КОНФИГ");
                TxtDonateBtn.Text = "Buy Me a Coffee ☕";
            }
            else
            {
                if (!_isLoadingFinished)
                {
                    TxtStep1Title.Text = "SCANNING SYSTEM";
                    TxtStep1Subtitle.Text = "Searching for Rust and analyzing PC hardware...";
                }
                else
                {
                    TxtStep1Title.Text = string.IsNullOrEmpty(_rustInstallPath) ? "RUST NOT FOUND" : "RUST INSTALLATION FOUND";
                    TxtStep1Subtitle.Text = string.IsNullOrEmpty(_rustInstallPath)
                        ? "Select your Rust installation directory manually below."
                        : "Installation found. Click Continue to configure client.cfg.";
                }

                TxtFoundLabel.Text = "RUST DIRECTORY:";
                BtnBrowseManual.Content = "Change...";
                BtnContinueToOpt.Content = "CONTINUE";
                BtnBack.Content = "← Back";
                TxtHwTitle.Text = "YOUR PC SPECIFICATIONS";
                TxtFpsTitle.Text = "SELECT FPS LIMIT";
                TxtFpsDesc.Text = "This is a frame cap, not a promise of extra FPS. Results depend on your PC and server.";
                RbFps30.Tag = "Lower load in-game";
                RbFps60.Tag = "Balanced profile";
                RbFps120.Tag = "For a 144 Hz monitor";
                RbFps240.Tag = "Rust's maximum cap";
                TxtFixedSettingsTitle.Text = "SETTINGS APPLIED BY THE OPTIMIZER";
                TxtFixedSettingsSub.Text = "Music: 0%  ·  FOV: 90  ·  VSync: OFF  ·  Menu FPS: 60";
                BtnApplyOptimization.Content = "APPLY OPTIMIZATION";
                BtnRestoreBackup.Content = "RESTORE ORIGINAL CONFIG";
                SetVerifyButtonContent("VERIFY CONFIG");
                TxtDonateBtn.Text = "Buy Me a Coffee ☕";
            }

            UpdateRecommendedProfileText();
        }

        private void UpdateRecommendedProfileText()
        {
            string tier = _ramGb >= 16 ? "High-End PvP" : (_ramGb >= 12 ? "Balanced Gaming" : "Low-Spec Boost");
            TxtRecommendedProfile.Text = _currentLang == "RU"
                ? $"Профиль по ОЗУ: {tier} ({_cpuThreads} потоков)"
                : $"RAM profile: {tier} ({_cpuThreads} threads)";
        }
        #endregion

        #region Rust and hardware discovery
        private string FindRustDirectory()
        {
            var candidateLibraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                    ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string
                    ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;

                if (!string.IsNullOrWhiteSpace(steamPath))
                {
                    steamPath = steamPath.Replace('/', '\\');
                    candidateLibraries.Add(steamPath);
                    string vdfFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdfFile))
                    {
                        string content = File.ReadAllText(vdfFile);
                        foreach (Match match in Regex.Matches(content, "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\""))
                        {
                            string library = match.Groups[1].Value.Replace("\\\\", "\\");
                            if (!string.IsNullOrWhiteSpace(library)) candidateLibraries.Add(library);
                        }
                    }
                }
            }
            catch { /* Continue with common Steam library locations. */ }

            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    candidateLibraries.Add(Path.Combine(drive.Name, "SteamLibrary"));
                    candidateLibraries.Add(Path.Combine(drive.Name, "Program Files (x86)", "Steam"));
                    candidateLibraries.Add(Path.Combine(drive.Name, "Steam"));
                }
            }
            catch { }

            foreach (string library in candidateLibraries)
            {
                try
                {
                    string rustDir = Path.Combine(library, "steamapps", "common", "Rust");
                    if (Directory.Exists(rustDir) && IsRustDirectory(rustDir)) return rustDir;
                }
                catch { }
            }
            return string.Empty;
        }

        private static bool IsRustDirectory(string path)
        {
            return File.Exists(Path.Combine(path, "RustClient.exe"))
                || File.Exists(Path.Combine(path, "Rust.exe"));
        }

        private void DetectPcHardware()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                {
                    foreach (ManagementBaseObject item in searcher.Get())
                    {
                        string name = item["Name"] == null ? null : item["Name"].ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(name)) { _cpuName = name; break; }
                    }
                }
            }
            catch { }

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    foreach (ManagementBaseObject item in searcher.Get())
                    {
                        string name = item["Name"] == null ? null : item["Name"].ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(name) && name.IndexOf("Microsoft Basic", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            _gpuName = name;
                            break;
                        }
                    }
                }
            }
            catch { }

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementBaseObject item in searcher.Get())
                    {
                        if (item["TotalPhysicalMemory"] != null)
                        {
                            long bytes = Convert.ToInt64(item["TotalPhysicalMemory"]);
                            _ramGb = Math.Max(1, (int)Math.Round(bytes / (1024d * 1024d * 1024d)));
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private void BtnBrowseManual_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = _currentLang == "RU" ? "Выберите RustClient.exe в папке игры" : "Select RustClient.exe in the game folder",
                Filter = "Rust executable (RustClient.exe;Rust.exe)|RustClient.exe;Rust.exe|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
            {
                string selectedDirectory = Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
                if (!IsRustDirectory(selectedDirectory))
                {
                    MessageBox.Show(
                        _currentLang == "RU" ? "В выбранной папке не найдены файлы Rust." : "Rust files were not found in the selected directory.",
                        "Rust Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _rustInstallPath = selectedDirectory;
                TxtRustPath.Text = _rustInstallPath;
                BtnContinueToOpt.IsEnabled = true;
                ApplyLanguage(_currentLang);
            }
        }

        private void BtnContinueToOpt_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_rustInstallPath) || !Directory.Exists(_rustInstallPath))
            {
                MessageBox.Show(_currentLang == "RU" ? "Сначала укажите папку Rust." : "Select your Rust folder first.",
                    "Rust Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TxtCpuName.Text = _cpuName;
            TxtGpuName.Text = _gpuName;
            TxtRamSize.Text = $"{_ramGb} GB RAM";
            UpdateRecommendedProfileText();
            ScreenSearch.Visibility = Visibility.Collapsed;
            ScreenOptimize.Visibility = Visibility.Visible;
            TxtConsoleLog.Text = _currentLang == "RU"
                ? "> Готов. Перед применением закрой Rust. Настройки будут записаны в:" + Environment.NewLine + Path.Combine(_rustInstallPath, "cfg", "client.cfg")
                : "> Ready. Close Rust before applying settings. The target file is:" + Environment.NewLine + Path.Combine(_rustInstallPath, "cfg", "client.cfg");
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            ScreenOptimize.Visibility = Visibility.Collapsed;
            ScreenSearch.Visibility = Visibility.Visible;
        }
        #endregion

        #region Apply and restore
        private int GetSelectedFpsCap()
        {
            if (RbFps30.IsChecked == true) return 60;
            if (RbFps60.IsChecked == true) return 120;
            if (RbFps120.IsChecked == true) return 144;
            return 240;
        }

        private bool IsRustRunning()
        {
            foreach (string processName in new[] { "RustClient", "Rust" })
            {
                try
                {
                    Process[] processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        foreach (Process process in processes) process.Dispose();
                        return true;
                    }
                    foreach (Process process in processes) process.Dispose();
                }
                catch { }
            }
            return false;
        }

        private async void BtnApplyOptimization_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_rustInstallPath) || !Directory.Exists(_rustInstallPath))
            {
                MessageBox.Show(_currentLang == "RU" ? "Сначала выберите папку Rust." : "Select the Rust folder first.",
                    "Rust Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsRustRunning())
            {
                AppendLog(_currentLang == "RU"
                    ? "> ОТМЕНЕНО: Rust запущен. Закройте игру вручную и повторите; программа не будет убивать процесс."
                    : "> CANCELLED: Rust is running. Close the game manually and retry; the optimizer will not terminate it.");
                MessageBox.Show(
                    _currentLang == "RU"
                        ? "Перед применением закрой Rust вручную. Программа не закрывает игру принудительно, чтобы не потерять игровой процесс."
                        : "Close Rust manually before applying settings. The optimizer will not force-terminate the game.",
                    "Rust Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BtnApplyOptimization.IsEnabled = false;
            BtnRestoreBackup.IsEnabled = false;
            SetVerifyButtonEnabled(false);
            BtnApplyOptimization.Content = _currentLang == "RU" ? "ПРИМЕНЯЮ..." : "APPLYING...";
            TxtConsoleLog.Text = string.Empty;

            string cfgFolder = Path.Combine(_rustInstallPath, "cfg");
            string clientCfgPath = Path.Combine(cfgFolder, "client.cfg");
            int fpsCap = GetSelectedFpsCap();

            try
            {
                Directory.CreateDirectory(cfgFolder);
                AppendLog(_currentLang == "RU" ? "> 1/4: Создаю резервные копии client.cfg..." : "> 1/4: Creating client.cfg backups...");
                CreateBackups(clientCfgPath, cfgFolder);
                await Task.Delay(80);

                AppendLog(_currentLang == "RU"
                    ? $"> 2/4: Записываю профиль с лимитом {fpsCap} FPS и графическими настройками..."
                    : $"> 2/4: Writing the {fpsCap} FPS cap and graphics profile...");
                Dictionary<string, string> settings = BuildSettings(fpsCap);
                ApplySmartRustConfig(clientCfgPath, settings);
                await Task.Delay(80);

                AppendLog(_currentLang == "RU"
                    ? "> 3/4: Повторно читаю файл и сверяю все записанные параметры..."
                    : "> 3/4: Re-reading the file and checking every written setting...");
                List<string> verificationIssues = VerifyAppliedSettings(clientCfgPath, settings);
                if (verificationIssues.Count > 0)
                    throw new IOException("Config verification failed: " + string.Join("; ", verificationIssues));
                AppendLog(_currentLang == "RU"
                    ? $"> Проверено значений: {settings.Count}; расхождений и дубликатов управляемых ключей нет."
                    : $"> Verified {settings.Count} values; no mismatches or duplicate managed keys.");
                await Task.Delay(80);

                AppendLog(_currentLang == "RU"
                    ? "> 4/4: ФАЙЛ ПРОВЕРЕН. Записанные значения совпали с ожидаемыми."
                    : "> 4/4: FILE VERIFIED. Saved values match expected values.");
                AppendLog(_currentLang == "RU"
                    ? "ВАЖНО: это проверка файла, а не измерение FPS и не чтение переменных из памяти игры. Перезапустите Rust. Если музыка или настройки вернутся, нажмите ПРОВЕРИТЬ после закрытия игры — программа покажет, что именно сбросилось. 240 FPS — только потолок; он не гарантирует 240 кадров."
                    : "IMPORTANT: this verifies the config file, not live game memory or measured FPS. Restart Rust. If music/settings return, close the game and click VERIFY to see which values were reset. 240 FPS is a cap, not a guarantee.");

                BtnApplyOptimization.Content = _currentLang == "RU" ? "ОПТИМИЗАЦИЯ ПРИМЕНЕНА ✓" : "OPTIMIZATION APPLIED ✓";
                SetVerifyButtonContent(_currentLang == "RU" ? "ПРОВЕРКА ПРОЙДЕНА ✓" : "CONFIG VERIFIED ✓");
            }
            catch (Exception ex)
            {
                AppendLog($"> ERROR: {ex.Message}");
                MessageBox.Show(
                    _currentLang == "RU"
                        ? $"Не удалось полностью применить настройки. Проверьте права доступа к папке Rust.\n\n{ex.Message}"
                        : $"Could not fully apply settings. Check write access to the Rust folder.\n\n{ex.Message}",
                    "Rust Optimizer", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnApplyOptimization.Content = _currentLang == "RU" ? "ПОВТОРИТЬ ПРИМЕНЕНИЕ" : "RETRY APPLY";
                SetVerifyButtonContent(_currentLang == "RU" ? "ПРОВЕРИТЬ КОНФИГ" : "VERIFY CONFIG");
            }
            finally
            {
                BtnApplyOptimization.IsEnabled = true;
                BtnRestoreBackup.IsEnabled = true;
                SetVerifyButtonEnabled(true);
            }
        }

        private void CreateBackups(string clientCfgPath, string cfgFolder)
        {
            string originalBackup = Path.Combine(cfgFolder, OriginalBackupName);
            string missingMarker = Path.Combine(cfgFolder, MissingConfigMarkerName);

            if (File.Exists(clientCfgPath))
            {
                // Если изначально client.cfg не существовал, не объявляем первый созданный нами файл оригиналом.
                if (!File.Exists(originalBackup) && !File.Exists(missingMarker))
                    File.Copy(clientCfgPath, originalBackup, false);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string timestampBackup = Path.Combine(cfgFolder, $"client.rustoptimizer.{timestamp}.backup.cfg");
                if (!File.Exists(timestampBackup)) File.Copy(clientCfgPath, timestampBackup, false);
                File.SetAttributes(clientCfgPath, FileAttributes.Normal);
            }
            else if (!File.Exists(originalBackup) && !File.Exists(missingMarker))
            {
                File.WriteAllText(missingMarker, "client.cfg did not exist before Rust Optimizer created it.", new UTF8Encoding(false));
            }
        }

        private Dictionary<string, string> BuildSettings(int fpsCap)
        {
            // Profile values are deliberately limited to Rust client variables used by current guides/convar references.
            // fps.limit is a CAP; it cannot force the CPU/GPU to render at that rate.
            int drawDistance;
            switch (fpsCap)
            {
                case 60: drawDistance = 2200; break;
                case 120: drawDistance = 2000; break;
                case 144: drawDistance = 1800; break;
                default: drawDistance = 1500; break;
            }

            int meshQuality;
            switch (fpsCap)
            {
                case 60: meshQuality = 200; break;
                case 120: meshQuality = 150; break;
                case 144: meshQuality = 125; break;
                default: meshQuality = 100; break;
            }

            int treeQuality;
            switch (fpsCap)
            {
                case 60: treeQuality = 150; break;
                case 120: treeQuality = 125; break;
                case 144: treeQuality = 100; break;
                default: treeQuality = 100; break;
            }

            int terrainQuality;
            switch (fpsCap)
            {
                case 60: terrainQuality = 35; break;
                case 120: terrainQuality = 20; break;
                case 144: terrainQuality = 10; break;
                default: terrainQuality = 5; break;
            }

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Music: game background tracks, menu tracks, instruments and Voice Props.
                // Both menu-volume spellings are included for compatibility with different client builds.
                ["music.enabled"] = "False",
                ["audio.musicvolume"] = "0",
                ["audio.musicvolumemenu"] = "0",
                ["audio.instruments"] = "0",
                ["audio.voiceprops"] = "0",

                // Core client controls.
                ["graphics.fov"] = "90",
                ["graphics.vsync"] = "0",
                ["fps.limit"] = fpsCap.ToString(System.Globalization.CultureInfo.InvariantCulture),
                // Rust treats these two values as toggles; fps.limitinmenu=1 activates its built-in menu cap.
                ["fps.limitinmenu"] = "1",
                ["fps.limitinbackground"] = "1",
                ["global.perf"] = "1",

                // Known graphics/performance variables; no unsupported render-scale switch.
                ["graphics.drawdistance"] = drawDistance.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["graphics.shaderlod"] = "100",
                ["graphics.af"] = "1",
                ["graphics.lodbias"] = fpsCap >= 144 ? "0.5" : "0.75",
                ["graphics.parallax"] = "0",
                ["graphics.dof"] = "False",
                ["graphics.contactshadows"] = "False",
                ["graphics.shadowcascades"] = "1",
                ["graphics.shadowdistance"] = "50",
                ["graphics.shadowlights"] = "1",
                ["graphics.shadowmode"] = "1",
                ["graphics.grassshadowsenabled"] = "False",
                // Newer client-side graphics-settings variables also used by the current renderer.
                ["graphicssettings.anisotropicfiltering"] = "0",
                ["graphicssettings.enablelodcrossfade"] = "False",
                ["graphicssettings.numshadowcascades"] = "1",
                ["graphicssettings.pixellightcount"] = "2",
                ["graphicssettings.shadowdistance"] = "50",
                ["graphicssettings.shadowmapresolution"] = "0",
                ["graphicssettings.shadowqualitypreset"] = "0",
                ["graphicssettings.softparticles"] = "False",

                ["effects.antialiasing"] = "0",
                ["effects.ao"] = "False",
                ["effects.bloom"] = "False",
                ["effects.lensdirt"] = "False",
                ["effects.motionblur"] = "False",
                ["effects.shafts"] = "False",
                ["effects.vignet"] = "False",
                ["effects.maxgibs"] = "0",
                ["effects.sharpen"] = "True",

                ["grass.displace"] = "False",
                ["grass.quality"] = "0",
                ["tree.quality"] = treeQuality.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["mesh.quality"] = meshQuality.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["terrain.quality"] = terrainQuality.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["particle.quality"] = "0",
                ["water.quality"] = "0",
                ["water.reflections"] = "0",
                ["decor.quality"] = "0"
            };
        }

        private void ApplySmartRustConfig(string cfgPath, Dictionary<string, string> settings)
        {
            var outputLines = new List<string>();
            var seenManagedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var managedKeys = new HashSet<string>(settings.Keys, StringComparer.OrdinalIgnoreCase);

            if (File.Exists(cfgPath))
            {
                foreach (string line in File.ReadAllLines(cfgPath, Encoding.UTF8))
                {
                    Match keyMatch = Regex.Match(line, @"^\s*(?<key>\S+)");
                    if (keyMatch.Success && managedKeys.Contains(keyMatch.Groups["key"].Value))
                    {
                        string key = keyMatch.Groups["key"].Value;
                        // Replace the first instance with a canonical key/value and remove duplicate managed entries.
                        if (seenManagedKeys.Add(key))
                            outputLines.Add($"{key} \"{settings[key]}\"");
                        continue;
                    }
                    outputLines.Add(line);
                }
            }

            foreach (KeyValuePair<string, string> setting in settings)
            {
                if (seenManagedKeys.Add(setting.Key))
                    outputLines.Add($"{setting.Key} \"{setting.Value}\"");
            }

            string tempPath = cfgPath + ".rustoptimizer.tmp";
            try
            {
                File.WriteAllLines(tempPath, outputLines, new UTF8Encoding(false));
                if (File.Exists(cfgPath))
                {
                    File.SetAttributes(cfgPath, FileAttributes.Normal);
                    File.Replace(tempPath, cfgPath, null);
                }
                else
                {
                    File.Move(tempPath, cfgPath);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        private static Dictionary<string, string> ReadConfigValues(string cfgPath, out Dictionary<string, int> counts)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string line in File.ReadAllLines(cfgPath, Encoding.UTF8))
            {
                Match match = Regex.Match(line, @"^\s*(?<key>\S+)\s+(?:""(?<quoted>[^""]*)""|(?<plain>\S+))\s*$");
                if (!match.Success || match.Groups["key"].Value.StartsWith("//", StringComparison.Ordinal))
                    continue;

                string key = match.Groups["key"].Value;
                string value = match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value;
                values[key] = value.Trim();
                counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
            }
            return values;
        }

        private List<string> VerifyAppliedSettings(string cfgPath, Dictionary<string, string> expected)
        {
            var issues = new List<string>();
            if (!File.Exists(cfgPath))
            {
                issues.Add(_currentLang == "RU" ? "client.cfg не существует." : "client.cfg does not exist.");
                return issues;
            }

            Dictionary<string, int> counts;
            Dictionary<string, string> actual = ReadConfigValues(cfgPath, out counts);
            foreach (KeyValuePair<string, string> setting in expected)
            {
                if (!actual.TryGetValue(setting.Key, out string value))
                {
                    issues.Add($"{setting.Key}: НЕ НАЙДЕН (ожидалось {setting.Value})");
                    continue;
                }

                if (!string.Equals(value, setting.Value, StringComparison.OrdinalIgnoreCase))
                    issues.Add($"{setting.Key}: значение {value}, ожидалось {setting.Value}");

                if (counts.TryGetValue(setting.Key, out int count) && count > 1)
                    issues.Add($"{setting.Key}: найдено дубликатов строк ({count})");
            }

            return issues;
        }

        private void BtnVerifySettings_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_rustInstallPath))
            {
                MessageBox.Show(_currentLang == "RU" ? "Сначала выберите папку Rust." : "Select the Rust folder first.",
                    "Rust Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cfgPath = Path.Combine(_rustInstallPath, "cfg", "client.cfg");
            if (!File.Exists(cfgPath))
            {
                AppendLog(_currentLang == "RU" ? "> ПРОВЕРКА: cfg/client.cfg не найден. Сначала примените настройки." : "> VERIFY: cfg/client.cfg not found. Apply settings first.");
                return;
            }

            try
            {
                Dictionary<string, string> expected = BuildSettings(GetSelectedFpsCap());
                List<string> issues = VerifyAppliedSettings(cfgPath, expected);
                AppendLog(_currentLang == "RU" ? $"> Проверка файла: {cfgPath}" : $"> Config file: {cfgPath}");
                if (issues.Count == 0)
                {
                    AppendLog(_currentLang == "RU"
                        ? $"> УСПЕХ: все {expected.Count} проверяемых значений совпадают с выбранным профилем."
                        : $"> PASS: all {expected.Count} checked values match the selected profile.");
                }
                else
                {
                    AppendLog(_currentLang == "RU"
                        ? $"> ПРОВЕРКА НЕ ПРОЙДЕНА: {issues.Count} расхождений. Значения ниже требуют исправления:"
                        : $"> VERIFY FAILED: {issues.Count} mismatch(es). These values need attention:");
                    foreach (string issue in issues) AppendLog($"  ! {issue}");
                }

                if (IsRustRunning())
                {
                    AppendLog(_currentLang == "RU"
                        ? "> Rust сейчас запущен: проверка файла НЕ доказывает, что эти значения уже действуют в памяти игры."
                        : "> Rust is running: file verification does NOT prove these settings are active in game memory.");
                }
                else
                {
                    AppendLog(_currentLang == "RU"
                        ? "> Rust закрыт: файл готов к чтению при следующем запуске."
                        : "> Rust is closed: the file is ready to be read on next launch.");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"> VERIFY ERROR: {ex.Message}");
                MessageBox.Show(ex.Message, "Rust Optimizer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_rustInstallPath)) return;
            if (IsRustRunning())
            {
                MessageBox.Show(_currentLang == "RU" ? "Сначала закройте Rust вручную." : "Close Rust manually first.",
                    "Rust Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cfgFolder = Path.Combine(_rustInstallPath, "cfg");
            string clientCfgPath = Path.Combine(cfgFolder, "client.cfg");
            string originalBackup = Path.Combine(cfgFolder, OriginalBackupName);
            string missingMarker = Path.Combine(cfgFolder, MissingConfigMarkerName);

            if (MessageBox.Show(
                _currentLang == "RU" ? "Восстановить исходный client.cfg из резервной копии? Текущая версия останется в timestamp-backup." : "Restore the original client.cfg backup? The current version will remain in the timestamp backup.",
                "Rust Optimizer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                if (File.Exists(originalBackup))
                {
                    if (File.Exists(clientCfgPath)) File.SetAttributes(clientCfgPath, FileAttributes.Normal);
                    File.Copy(originalBackup, clientCfgPath, true);
                    File.SetAttributes(clientCfgPath, FileAttributes.Normal);
                    AppendLog(_currentLang == "RU" ? "> Исходный client.cfg восстановлен из резервной копии." : "> Original client.cfg restored from backup.");
                }
                else if (File.Exists(missingMarker))
                {
                    if (File.Exists(clientCfgPath))
                    {
                        File.SetAttributes(clientCfgPath, FileAttributes.Normal);
                        File.Delete(clientCfgPath);
                    }
                    File.Delete(missingMarker);
                    AppendLog(_currentLang == "RU" ? "> Удалён созданный оптимизатором client.cfg; до этого файла не существовало." : "> Removed optimizer-created client.cfg; no config existed before the optimizer ran.");
                }
                else
                {
                    MessageBox.Show(_currentLang == "RU" ? "Резервная копия ещё не создана." : "No backup has been created yet.",
                        "Rust Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"> ERROR: {ex.Message}");
                MessageBox.Show(ex.Message, "Rust Optimizer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        private void AppendLog(string message)
        {
            TxtConsoleLog.Text += (string.IsNullOrEmpty(TxtConsoleLog.Text) ? string.Empty : Environment.NewLine) + message;
            LogScroll.ScrollToEnd();
        }
    }
}
