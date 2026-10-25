using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using PolarRecorder.Services;
using Rectangle = System.Drawing.Rectangle;
using Screen = System.Windows.Forms.Screen;
namespace PolarRecorder;
public partial class MainWindow : Window
{
    private RecordingSession? session;
    private Hotkeys? hotkeys;
    private bool busy, initialized;
    private bool desktopPcAudio = true, windowAudioMode, changingModeAudio;
    private readonly DispatcherTimer noticeTimer = new();
    private string? lastNotice;
    private bool lastNoticeIsError;
    private Rectangle? region;
    private CancellationTokenSource? countdown;
    private string? lastVideo;
    private int libraryGeneration;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PolarRec", "settings.json");
    private sealed record Settings(string Folder, bool Pc, bool Mic, bool Cursor, bool? DesktopPc = null);
    private static string EnginePath => Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe");
    public sealed record RecordingItem(string Path, string Name, string Detail);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
    public MainWindow()
    {
        InitializeComponent();
        noticeTimer.Tick += (_, _) => {
            HideNotice();
        };
        Closed += (_, _) => noticeTimer.Stop();
        string videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        string legacyFolder = Path.Combine(videos, "Polar Recorder");
        FolderBox.Text = Directory.Exists(legacyFolder) ? legacyFolder : Path.Combine(videos, "PolarRec");
        string legacySettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PolarRecorder", "settings.json");
        string loadPath = File.Exists(settingsPath) ? settingsPath : legacySettings;
        try {
            if (File.Exists(loadPath)) {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(loadPath));
                if (s != null) { FolderBox.Text = s.Folder; desktopPcAudio = s.DesktopPc ?? true; SystemAudio.IsChecked = desktopPcAudio; MicAudio.IsChecked = s.Mic; CursorOption.IsChecked = s.Cursor; if (loadPath != settingsPath) TryPersist(); }
            }
        } catch (Exception ex) { Notify("설정을 불러오지 못해 기본값을 사용합니다. " + ex.Message); }
        var screens = Screen.AllScreens;
        ScreenList.ItemsSource = screens.Select((s, i) => $"모니터 {i + 1} · {s.Bounds.Width} × {s.Bounds.Height}" + (s.Primary ? " · 기본" : "")).ToArray();
        ScreenList.SelectedIndex = Math.Max(0, Array.FindIndex(screens, s => s.Primary));
        ScreenList.SelectionChanged += (_, _) => { region = null; InvalidatePreview(); UpdateSummary(); SetControls(); };
        SourceInitialized += (_, _) => {
            hotkeys = new Hotkeys(this);
            bool start = hotkeys.Register(1, 0x78), pause = hotkeys.Register(2, 0x79);
            if (!start || !pause) Notify("다른 앱이 단축키를 사용 중입니다. 창의 녹화·저장 버튼을 이용하세요.");
            hotkeys.Pressed += async id => { if (id == 1) { if (session == null) await StartAsync(); else await SaveAsync(); } else await TogglePauseAsync(); };
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && countdown != null) { countdown.Cancel(); e.Handled = true; } };
        timer.Tick += async (_, _) => {
            if (session == null) return;
            var d = session.Duration;
            ClockLabel.Text = $"{(int)d.TotalHours:00}:{d.Minutes:00}:{d.Seconds:00}";
            if (!busy && session.IsCapturing && session.TargetUnavailable is string targetReason) {
                await TogglePauseAsync(); Notify(targetReason);
            }
            else if (!busy && session.CaptureFailed) await SaveAsync();
            else if (!busy && session.SegmentSeconds > 3600) {
                await TogglePauseAsync(); Notify("60분 구간을 녹화해 자동으로 일시정지했습니다. 계속 녹화로 이어서 담을 수 있습니다.");
            }
        };
        initialized = true;
        FolderLabel.Text = FolderBox.Text;
        UpdateSummary(); ShowPage("record"); SetControls(); timer.Start();
    }
    private void PersistSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var s = new Settings(FolderBox.Text, desktopPcAudio, MicAudio.IsChecked == true, CursorOption.IsChecked == true, desktopPcAudio);
        // Replace atomically so a partial write cannot corrupt a previously saved preference file.
        string temp = settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(s)); File.Move(temp, settingsPath, true);
    }
    private void TryPersist() { try { PersistSettings(); } catch (Exception ex) { Notify("설정을 저장하지 못했습니다. " + ex.Message); } }
    private void Notify(string message, bool isError = false)
    {
        noticeTimer.Stop(); lastNotice = message; lastNoticeIsError = isError;
        NoticeLabel.Text = message;
        NoticePanel.Background = (Brush)FindResource(isError ? "SurfaceBrush" : "IceBrush");
        NoticePanel.BorderBrush = (Brush)FindResource(isError ? "ErrorBrush" : "AccentBrush");
        NoticeLabel.Foreground = (Brush)FindResource(isError ? "ErrorBrush" : "InkBrush");
        LastNoticeButton.Visibility = Visibility.Visible;
        NoticePanel.Visibility = Visibility.Visible;
        NoticePanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        NoticeOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-16, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        noticeTimer.Interval = TimeSpan.FromSeconds(3); noticeTimer.Start();
    }
    private void HideNotice() { noticeTimer.Stop(); NoticePanel.Visibility = Visibility.Collapsed; }
    private void LastNoticeClick(object sender, RoutedEventArgs e) { if (lastNotice != null) Notify(lastNotice, lastNoticeIsError); }
    private void SetState(string title, string hint, string color = "#245D85")
    { StatusLabel.Text = title; HintLabel.Text = hint; StateDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); }
    private void SetControls()
    {
        if (!initialized) return;
        bool engine = File.Exists(EnginePath);
        bool validArea = WindowMode.IsChecked == true ? WindowTargets.Supported && WindowList.SelectedItem is WindowTarget : RegionMode.IsChecked != true || region.HasValue;
        StartButton.IsEnabled = !busy && session == null && engine && validArea;
        StartButton.Visibility = session == null && countdown == null ? Visibility.Visible : Visibility.Collapsed;
        PauseButton.Visibility = StopButton.Visibility = session == null ? Visibility.Collapsed : Visibility.Visible;
        PauseButton.IsEnabled = StopButton.IsEnabled = !busy && session != null;
        CancelButton.Visibility = countdown == null ? Visibility.Collapsed : Visibility.Visible;
        PauseButton.Content = session?.IsCapturing == true ? "Ⅱ  일시정지" : "▶  계속 녹화";
        SettingsPanel.IsEnabled = PreferencesEditor.IsEnabled = PreviewButton.IsEnabled = FolderButton.IsEnabled = !busy && session == null;
        SetupBanner.Visibility = engine ? Visibility.Collapsed : Visibility.Visible;
        EngineStatus.Text = engine ? "녹화 기능이 준비되어 있습니다. 별도 설치나 설정은 필요하지 않습니다." : "앱 구성 파일이 누락되었습니다. 설치 프로그램을 다시 실행해 주세요.";
        LastVideoButton.Visibility = lastVideo != null && session == null && !busy ? Visibility.Visible : Visibility.Collapsed;
        if (session == null && !busy && lastVideo == null) {
            if (!engine) SetState("앱 파일을 확인해 주세요", "설치 프로그램을 다시 실행하면 복구할 수 있습니다.");
            else if (!validArea) SetState(WindowMode.IsChecked == true ? "녹화할 창을 선택하세요" : "영역을 지정해 주세요", WindowMode.IsChecked == true ? "창을 열고 창 목록 새로고침을 누르세요." : "녹화할 부분을 마우스로 드래그하세요.");
            else if (StatusLabel.Text != "작업을 완료하지 못했습니다") SetState("녹화 준비", "3초 준비 후 녹화를 시작합니다.");
        }
    }
    private Rectangle SelectedBounds()
    {
        if (WindowMode.IsChecked == true) {
            if (!WindowTargets.Supported) throw new InvalidOperationException("창 녹화는 Windows 10 1903 이상 또는 Windows 11에서 사용할 수 있습니다.");
            return WindowTargets.GetBounds(SelectedWindow());
        }
        var screens = Screen.AllScreens;
        if (ScreenList.SelectedIndex < 0 || ScreenList.SelectedIndex >= screens.Length) throw new InvalidOperationException("모니터 구성이 변경되었습니다. 앱을 다시 시작하세요.");
        var monitor = screens[ScreenList.SelectedIndex].Bounds;
        Rectangle bounds = RegionMode.IsChecked == true ? region ?? throw new InvalidOperationException("먼저 녹화 영역을 지정하세요.") : monitor;
        if (!monitor.Contains(bounds)) throw new InvalidOperationException("화면 구성이 달라졌습니다. 녹화 영역을 다시 지정하세요.");
        bounds.Width = bounds.Width / 2 * 2; bounds.Height = bounds.Height / 2 * 2;
        return bounds;
    }
    private void UpdateSummary()
    {
        if (!initialized) return;
        bool custom = RegionMode.IsChecked == true;
        bool window = WindowMode.IsChecked == true;
        WindowPicker.Visibility = window ? Visibility.Visible : Visibility.Collapsed;
        MonitorLabel.Visibility = ScreenList.Visibility = window ? Visibility.Collapsed : Visibility.Visible;
        SelectRegionButton.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        SelectRegionButton.Content = region.HasValue ? "영역 다시 지정" : "영역 지정하기";
        RegionLabel.Text = window ? "탭을 바꾸면 바뀐 탭이 녹화됩니다. 최소화하면 일시정지합니다." : custom ? (region.HasValue ? $"{region.Value.Width} × {region.Value.Height} px · 선택됨" : "영역 지정하기를 눌러 드래그하세요.") : "선택한 모니터 전체를 녹화합니다.";
        try { var b = SelectedBounds(); CaptureSummary.Text = $"{(window ? "선택한 창" : custom ? "선택 영역" : "전체 화면")} · {b.Width} × {b.Height}"; } catch { CaptureSummary.Text = window ? "아직 창을 선택하지 않았어요" : "아직 영역을 지정하지 않았어요"; }
        AudioSummary.Text = $"PC 전체 소리 {(SystemAudio.IsChecked == true ? "켜짐" : "꺼짐")} · 마이크 {(MicAudio.IsChecked == true ? "켜짐" : "꺼짐")}";
    }
    private void InvalidatePreview() { if (!initialized) return; PreviewImage.Source = null; PreviewPlaceholder.Visibility = Visibility.Visible; }
    private void CaptureModeChanged(object sender, RoutedEventArgs e) {
        if (!initialized) return;
        bool enteringWindow = WindowMode.IsChecked == true;
        if (enteringWindow != windowAudioMode) {
            changingModeAudio = true;
            try {
                if (enteringWindow) desktopPcAudio = SystemAudio.IsChecked == true;
                windowAudioMode = enteringWindow;
                SystemAudio.IsChecked = enteringWindow ? false : desktopPcAudio;
            } finally { changingModeAudio = false; }
            if (enteringWindow) {
                Notify("창 녹화에서는 PC 소리를 기본으로 끕니다. 필요하면 직접 켤 수 있습니다.");
                RefreshWindows();
            } else Notify(desktopPcAudio ? "화면 녹화로 전환해 PC 소리를 다시 켰습니다." : "화면 녹화로 전환했습니다. 직접 꺼둔 PC 소리 설정을 유지합니다.");
        }
        InvalidatePreview(); UpdateSummary(); SetControls();
    }
    private WindowTarget SelectedWindow() => WindowList.SelectedItem as WindowTarget ?? throw new InvalidOperationException("먼저 녹화할 창을 선택하세요.");
    private void RefreshWindows() {
        var previous = WindowList.SelectedItem as WindowTarget;
        var items = WindowTargets.List();
        WindowList.ItemsSource = items;
        WindowList.SelectedItem = items.FirstOrDefault(x => x.Handle == previous?.Handle && x.ProcessStarted == previous.ProcessStarted);
        if (items.Count == 0) Notify("선택 가능한 창이 없습니다. 앱 또는 브라우저 창을 열고 최소화를 해제한 뒤 새로고침하세요.");
    }
    private void RefreshWindowsClick(object sender, RoutedEventArgs e) { if (!busy && session == null) { RefreshWindows(); UpdateSummary(); SetControls(); } }
    private void WindowSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { if (initialized) { InvalidatePreview(); UpdateSummary(); SetControls(); } }
    private void OptionChanged(object sender, RoutedEventArgs e) {
        if (!initialized || changingModeAudio) return;
        if (ReferenceEquals(sender, SystemAudio) && !windowAudioMode) desktopPcAudio = SystemAudio.IsChecked == true;
        UpdateSummary(); TryPersist();
    }
    private void RestoreWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    private async Task StartAsync()
    {
        if (busy || session != null) return;
        busy = true; lastVideo = null; SetControls();
        try {
            if (!File.Exists(EnginePath)) { ShowPage("settings"); throw new FileNotFoundException("앱 구성 파일이 누락되었습니다. 설치 프로그램을 다시 실행해 주세요."); }
            Rectangle bounds = SelectedBounds();
            Directory.CreateDirectory(FolderBox.Text);
            string probe = Path.Combine(FolderBox.Text, ".polar-write-" + Guid.NewGuid().ToString("N")); File.WriteAllText(probe, ""); File.Delete(probe);
            PersistSettings(); HideNotice(); ClockLabel.Text = "00:00:00"; ShowPage("record");
            using (var cts = new CancellationTokenSource()) {
                countdown = cts; SetControls();
                try { for (int n = 3; n > 0; n--) { SetState($"{n}초 후 시작", "준비가 안 됐다면 Esc 또는 시작 취소를 누르세요."); await Task.Delay(1000, cts.Token); } }
                finally { countdown = null; }
            }
            session = new RecordingSession(new RecordingOptions(EnginePath, FolderBox.Text, bounds, SystemAudio.IsChecked == true, MicAudio.IsChecked == true, CursorOption.IsChecked == true, WindowMode.IsChecked == true ? SelectedWindow() : null));
            SetState("녹화를 시작하는 중", "잠시만 기다려 주세요."); SetControls();
            WindowState = WindowState.Minimized; await Task.Delay(300); await session.ResumeAsync();
            SetState("녹화 중", "Ctrl + Shift + F9 저장 · Ctrl + Shift + F10 일시정지", "#B84242");
        } catch (OperationCanceledException) { SetState("녹화 준비", "시작을 취소했습니다. 파일은 생성하지 않았습니다."); }
        catch (Exception ex) { string? path = session?.WorkDirectory; session = null; RestoreWindow(); ShowError(ex, path); }
        finally { busy = false; countdown = null; SetControls(); }
    }
    private async Task TogglePauseAsync()
    {
        if (busy || session == null) return;
        busy = true; SetControls();
        try {
            if (session.IsCapturing) { SetState("녹화 구간 정리 중", "영상과 소리를 정리하고 있습니다."); await session.PauseAsync(); RestoreWindow(); SetState("일시정지", "멈춘 시간은 영상에 포함되지 않습니다.", "#956013"); }
            else { SetState("다시 시작하는 중", "잠시 후 창이 최소화됩니다."); WindowState = WindowState.Minimized; await Task.Delay(300); await session.ResumeAsync(); SetState("녹화 중", "Ctrl + Shift + F9 저장 · Ctrl + Shift + F10 일시정지", "#B84242"); }
        } catch (Exception ex) { RestoreWindow(); ShowError(ex, session?.WorkDirectory); }
        finally { busy = false; SetControls(); }
    }
    private async Task SaveAsync()
    {
        if (busy || session == null) return;
        busy = true; SetControls();
        try {
            SetState("MP4 저장 중", "저장이 끝날 때까지 앱을 열어 두세요.");
            string recovery = session.WorkDirectory;
            string output = await session.SaveAsync(); session = null; lastVideo = output; RestoreWindow();
            SetState("저장 완료", Path.GetFileName(output) + " · 영상 열기로 바로 확인하세요.");
            if (Directory.Exists(recovery)) Notify("확정된 구간을 저장했습니다. 이전 실패 구간 또는 임시 파일을 확인하세요: " + recovery);
            if (LibraryPage.Visibility == Visibility.Visible) await LoadLibraryAsync();
        } catch (Exception ex) { RestoreWindow(); ShowError(ex, session?.WorkDirectory); }
        finally { busy = false; SetControls(); }
    }
    private void ShowError(Exception ex, string? path) { SetState("작업을 완료하지 못했습니다", "안내를 확인한 후 다시 시도하세요.", "#A53939"); Notify(ex.Message + (path == null ? "" : "\n복구용 파일: " + path), isError: true); }
    private async void StartClick(object sender, RoutedEventArgs e) => await StartAsync();
    private async void PauseClick(object sender, RoutedEventArgs e) => await TogglePauseAsync();
    private async void StopClick(object sender, RoutedEventArgs e) => await SaveAsync();
    private void CancelStartClick(object sender, RoutedEventArgs e) => countdown?.Cancel();
    private void DismissNotice(object sender, RoutedEventArgs e) => HideNotice();
    private void SelectRegionClick(object sender, RoutedEventArgs e)
    {
        if (busy || session != null) return;
        var screens = Screen.AllScreens;
        if (ScreenList.SelectedIndex < 0 || ScreenList.SelectedIndex >= screens.Length) return;
        Hide();
        try { var picker = new RegionPicker(screens[ScreenList.SelectedIndex].Bounds); if (picker.ShowDialog() == true) { region = picker.SelectedRegion; RegionMode.IsChecked = true; InvalidatePreview(); UpdateSummary(); SetControls(); } }
        finally { Show(); Activate(); }
    }
    private async void PreviewClick(object sender, RoutedEventArgs e)
    {
        if (busy || session != null) return;
        busy = true; SetControls();
        try {
            Rectangle b = SelectedBounds();
            if (WindowMode.IsChecked == true) {
                string temp = Path.Combine(Path.GetTempPath(), "PolarRec-preview-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                try {
                    string file = Path.Combine(temp, "preview.png");
                    await Ffmpeg.RunAsync(EnginePath, new[] { "-hide_banner", "-y", "-filter_complex", CaptureArguments.WindowFilter(SelectedWindow().Handle, b, CursorOption.IsChecked == true), "-frames:v", "1", "-threads", "1", file }, Path.Combine(temp, "preview.log"), TimeSpan.FromSeconds(12));
                    var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(file); image.EndInit(); image.Freeze();
                    PreviewImage.Source = image; PreviewPlaceholder.Visibility = Visibility.Collapsed;
                } catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException) {
                    string diagnostic = "";
                    try {
                        string logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PolarRec", "Logs");
                        Directory.CreateDirectory(logs);
                        string destination = Path.Combine(logs, "preview-" + Guid.NewGuid().ToString("N") + ".log");
                        if (File.Exists(Path.Combine(temp, "preview.log"))) {
                            File.Copy(Path.Combine(temp, "preview.log"), destination);
                            diagnostic = "\n진단 로그: " + destination;
                        }
                    } catch (Exception logError) when (logError is IOException or UnauthorizedAccessException) { }
                    throw new InvalidOperationException(WindowTargetPolicy.CaptureFailure(ex) + diagnostic, ex);
                } finally { try { Directory.Delete(temp, true); } catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException) { } }
                return;
            }
            Hide(); await Task.Delay(180);
            using var bitmap = new System.Drawing.Bitmap(b.Width, b.Height);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) graphics.CopyFromScreen(b.Location, System.Drawing.Point.Empty, b.Size);
            IntPtr handle = bitmap.GetHbitmap();
            try { var image = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); image.Freeze(); PreviewImage.Source = image; PreviewPlaceholder.Visibility = Visibility.Collapsed; }
            finally { DeleteObject(handle); }
        } catch (Exception ex) { ShowError(ex, null); }
        finally { Show(); Activate(); busy = false; SetControls(); }
    }
    private void FolderClick(object sender, RoutedEventArgs e)
    {
        if (busy || session != null) return;
        var d = new OpenFolderDialog { Title = "녹화 저장 폴더" };
        if (d.ShowDialog(this) == true) { FolderBox.Text = FolderLabel.Text = d.FolderName; TryPersist(); }
    }
    private void RepairHelpClick(object sender, RoutedEventArgs e)
    {
        Notify("설치 프로그램을 다시 실행해 주세요. 구성 파일은 자동으로 복구되며 FFmpeg 경로를 지정할 필요가 없습니다. 보안 프로그램이 파일을 격리했다면 격리 내역을 확인하고, 회사 PC에서는 담당자에게 문의해 주세요.");
    }
    private void OpenPath(string path) { try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception ex) { ShowError(ex, null); } }
    private void OpenFolderClick(object sender, RoutedEventArgs e) { try { Directory.CreateDirectory(FolderBox.Text); OpenPath(FolderBox.Text); } catch (Exception ex) { ShowError(ex, null); } }
    private void LastVideoClick(object sender, RoutedEventArgs e) { if (lastVideo != null) OpenPath(lastVideo); }
    private void ShowPage(string name)
    {
        RecordPage.Visibility = name == "record" ? Visibility.Visible : Visibility.Collapsed;
        LibraryPage.Visibility = name == "library" ? Visibility.Visible : Visibility.Collapsed;
        PreferencesPage.Visibility = name == "settings" ? Visibility.Visible : Visibility.Collapsed;
        foreach (var pair in new[] { (RecordNav, "record"), (LibraryNav, "library"), (SettingsNav, "settings") }) {
            pair.Item1.Background = (Brush)FindResource(pair.Item2 == name ? "IceBrush" : "SurfaceBrush");
            pair.Item1.Foreground = (Brush)FindResource(pair.Item2 == name ? "ActionBrush" : "MutedBrush");
        }
    }
    private void RecordNavClick(object sender, RoutedEventArgs e) => ShowPage("record");
    private void SettingsNavClick(object sender, RoutedEventArgs e) => ShowPage("settings");
    private async void LibraryNavClick(object sender, RoutedEventArgs e) { ShowPage("library"); await LoadLibraryAsync(); }
    private async Task LoadLibraryAsync()
    {
        int generation = ++libraryGeneration; string folder = FolderBox.Text;
        try {
            var items = await Task.Run(() => Directory.Exists(folder) ? new DirectoryInfo(folder).EnumerateFiles("*.mp4").OrderByDescending(f => f.LastWriteTime).Take(50).Select(f => new RecordingItem(f.FullName, f.Name, $"{f.LastWriteTime:yyyy.MM.dd HH:mm}  ·  {f.Length / 1048576.0:0.0} MB")).ToArray() : Array.Empty<RecordingItem>());
            if (generation != libraryGeneration) return;
            RecordingsList.ItemsSource = items; RecordingsList.Visibility = items.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            EmptyLibrary.Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        } catch (Exception ex) { Notify("저장 폴더를 읽지 못했습니다. " + ex.Message); }
    }
    private void OpenRecordingClick(object sender, RoutedEventArgs e) { if (sender is System.Windows.Controls.Button b && b.Tag is string path) OpenPath(path); }
    private void RecordingDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // A double-click on the explicit Open button must not launch the player again via the row.
        var origin = e.OriginalSource as DependencyObject;
        while (origin != null && origin != RecordingsList) {
            if (origin is System.Windows.Controls.Button) return;
            origin = origin is Visual ? VisualTreeHelper.GetParent(origin) : null;
        }
        if (RecordingsList.SelectedItem is RecordingItem item) OpenPath(item.Path);
    }
    private void RecordingKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && RecordingsList.SelectedItem is RecordingItem item) { OpenPath(item.Path); e.Handled = true; } }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (busy) { e.Cancel = true; countdown?.Cancel(); return; }
        if (session != null) {
            e.Cancel = true;
            if (MessageBox.Show(this, "녹화를 저장하고 종료할까요?", "진행 중인 녹화", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) {
                await SaveAsync(); if (session == null) Close();
                else if (!session.IsCapturing && MessageBox.Show(this, "저장에 실패했습니다. 복구용 파일을 남겨두고 종료할까요?", "파일 보존 후 종료", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) { session = null; Close(); }
            }
            return;
        }
        TryPersist(); timer.Stop(); hotkeys?.Dispose();
    }
}
