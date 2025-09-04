using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DecibelMeter.Models;
using NAudio.Wave;
using WinForms = System.Windows.Forms;

namespace DecibelMeter
{
    // MainWindow is the primary UI for the percentage level meter application
    public partial class MainWindow : Window
    {
        private AudioService? audioService;
        private Config config;
        private WaveOutEvent? warningOutputDevice;
        private AudioFileReader? warningAudioFile;
        private OverlayWindow? overlay;
        private bool isWarningPlaying = false;

        private readonly List<(DateTime Timestamp, double Value)> percentBuffer = new();
        private string overlayImagePath = "Assets\\default_overlay.png";

        // Validation ranges
        private const int MinThreshold = 0; // Min threshold percent
        private const int MaxThreshold = 100; // Max threshold percent
        private const double MinAvg = 0.0; // Min average window (0 = instant)
        private const double MaxAvg = 5.0; // Max average window in seconds
        private const int MinVolume = 0; // Min volume percent
        private const int MaxVolume = 200; // Max volume percent (200% allowed)

        // Validation state
        private bool _thresholdValid = true;
        private bool _avgValid = true;
        private bool _volumeValid = true;

        // Brushes for valid/invalid textbox borders
        private readonly Brush _validBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
        private readonly Brush _invalidBorderBrush = new SolidColorBrush(Color.FromRgb(170, 51, 51));

        // Prevent validation-triggered saves during initial UI population
        private bool _initializing = true;

        // Convenience flag indicating monitoring state
        private bool IsMonitoring => audioService != null;

        // Initializes new instance of MainWindow and sets up UI and config
        public MainWindow()
        {
            // Load config first to ensure event handlers do not see null state
            config = ConfigService.Load();

            // Clamp persisted average window to new allowed range if prior versions exceeded it
            if (config.AverageWindowSeconds > MaxAvg)
            {
                config.AverageWindowSeconds = MaxAvg;
                ConfigService.Save(config);
            }
            if (config.AverageWindowSeconds < 0)
            {
                config.AverageWindowSeconds = 0;
                ConfigService.Save(config);
            }

            InitializeComponent();

            // Apply loaded config values to UI controls
            ApplyConfigToUi();

            // Wire feature toggle handlers (ensures they cannot execute before config load)
            ActivateSoundCheckBox.Checked += ActivateSoundCheckBox_Changed;
            ActivateSoundCheckBox.Unchecked += ActivateSoundCheckBox_Changed;
            ActivateOverlayCheckBox.Checked += ActivateOverlayCheckBox_Changed;
            ActivateOverlayCheckBox.Unchecked += ActivateOverlayCheckBox_Changed;

            // Ensure input validation handlers are attached (defensive detach first)
            VolumeBox.TextChanged -= VolumeBox_TextChanged;
            VolumeBox.TextChanged += VolumeBox_TextChanged;
            VolumeBox.LostFocus -= VolumeBox_LostFocus;
            VolumeBox.LostFocus += VolumeBox_LostFocus;

            ThresholdBox.TextChanged -= ThresholdBox_TextChanged;
            ThresholdBox.TextChanged += ThresholdBox_TextChanged;

            AverageWindowBox.TextChanged -= AverageWindowBox_TextChanged;
            AverageWindowBox.TextChanged += AverageWindowBox_TextChanged;

            // Prepare overlay (hidden by default) + load previous warning sound if present
            InitializeOverlay();
            LoadLastWarningSound();

            // Perform initial validation to sync visual state and Start button
            ValidateThreshold();
            ValidateAverageWindow();
            ValidateVolume();
            UpdateStartButtonEnabled();
            UpdateMonitoringButtons();

            _initializing = false; // Allow subsequent validation to persist changes
        }

        // Applies persisted configuration values to UI controls
        private void ApplyConfigToUi()
        {
            PopulateDeviceList();
            PopulateMonitorList();
            ThresholdBox.Text = config.ThresholdPercent.ToString(CultureInfo.InvariantCulture);
            AverageWindowBox.Text = config.AverageWindowSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            VolumeBox.Text = config.WarningSoundVolume.ToString(CultureInfo.InvariantCulture);

            ActivateSoundCheckBox.IsChecked = config.EnableWarningSound;
            ActivateOverlayCheckBox.IsChecked = config.EnableOverlay;

            // Always allow changing file selection even when feature disabled
            SelectSoundButton.IsEnabled = true;
            SelectOverlayButton.IsEnabled = true;

            // Hide/show volume controls based on checkbox activation
            UpdateVolumeVisibility(config.EnableWarningSound);
            UpdateOverlayDependentVisibility(config.EnableOverlay);

            // Show selected overlay image (or placeholder if missing)
            if (!string.IsNullOrWhiteSpace(config.OverlayImagePath) &&
                System.IO.File.Exists(config.OverlayImagePath))
            {
                overlayImagePath = config.OverlayImagePath;
                SelectedOverlayText.Text = System.IO.Path.GetFileName(overlayImagePath);
            }
            else
            {
                SelectedOverlayText.Text = "No file selected";
            }
        }

        // Enables/disables start / stop buttons based on monitoring & validation state
        private void UpdateMonitoringButtons()
        {
            if (StartMonitoringButton != null)
                StartMonitoringButton.IsEnabled = !IsMonitoring && AllInputsValid();
            if (StopMonitoringButton != null)
                StopMonitoringButton.IsEnabled = IsMonitoring;
        }

        // Shows or hides the volume controls depending on sound feature toggle
        private void UpdateVolumeVisibility(bool visible)
        {
            var vis = visible ? Visibility.Visible : Visibility.Collapsed;
            VolumeLabel.Visibility = vis;
            VolumeBox.Visibility = vis;
        }

        // Shows or hides overlay monitor selection based on overlay feature toggle
        private void UpdateOverlayDependentVisibility(bool overlayEnabled)
        {
            var vis = overlayEnabled ? Visibility.Visible : Visibility.Collapsed;
            MonitorLabel.Visibility = vis;
            MonitorComboBox.Visibility = vis;
        }

        // Loads previously used warning sound and prepares playback chain if file exists
        private void LoadLastWarningSound()
        {
            if (!string.IsNullOrEmpty(config.LastWarningSoundPath) &&
                System.IO.File.Exists(config.LastWarningSoundPath))
            {
                SelectedSoundText.Text = System.IO.Path.GetFileName(config.LastWarningSoundPath);

                warningAudioFile?.Dispose();
                warningOutputDevice?.Dispose();

                warningAudioFile = new AudioFileReader(config.LastWarningSoundPath);
                warningOutputDevice = new WaveOutEvent();
                warningOutputDevice.Init(warningAudioFile);
            }
            else
            {
                SelectedSoundText.Text = "No file selected";
            }
        }

        // Populates capture device list from available input devices
        private void PopulateDeviceList()
        {
            DeviceComboBox.Items.Clear();
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                DeviceComboBox.Items.Add(caps.ProductName);
            }
            if (!string.IsNullOrEmpty(config.SelectedDevice))
                DeviceComboBox.SelectedItem = config.SelectedDevice;
        }

        // Populates monitor selection with available screens
        private void PopulateMonitorList()
        {
            MonitorComboBox.Items.Clear();
            foreach (var screen in WinForms.Screen.AllScreens)
                MonitorComboBox.Items.Add(screen.DeviceName);
            MonitorComboBox.SelectedIndex = config.SelectedMonitor < WinForms.Screen.AllScreens.Length
                ? config.SelectedMonitor
                : 0;
        }

        // Initializes overlay window with specified image, hidden by default
        private void InitializeOverlay()
        {
            overlay?.Close();
            overlay = new OverlayWindow(overlayImagePath)
            {
                Visibility = Visibility.Hidden
            };
        }

        // Handles Start Monitoring button click and begins capturing levels
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (IsMonitoring)
                return;

            if (!AllInputsValid())
            {
                System.Windows.MessageBox.Show("Cannot start monitoring. Fix invalid inputs.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Final validation commit before start
            ValidateThreshold();
            ValidateAverageWindow();
            ValidateVolume();

            config.SelectedDevice = DeviceComboBox.SelectedItem?.ToString() ?? "";
            config.SelectedMonitor = MonitorComboBox.SelectedIndex;
            ConfigService.Save(config);

            audioService = new AudioService();
            audioService.VolumeMeasured += OnVolumeMeasured;

            try
            {
                audioService.Start(config.SelectedDevice);
            }
            catch
            {
                audioService.Dispose();
                audioService = null;
                throw;
            }

            UpdateMonitoringButtons();
        }

        // Handles Stop Monitoring button click and ends capture session
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            audioService?.Dispose();
            audioService = null;
            OutputText.Text = "Monitoring stopped.";
            HideOverlay();
            ThresholdLine.Visibility = Visibility.Collapsed;
            ThresholdValueLabel.Visibility = Visibility.Collapsed;

            UpdateMonitoringButtons();
        }

        // Called when AudioService reports a new instantaneous volume percentage
        private void OnVolumeMeasured(double percent)
        {
            Dispatcher.Invoke(() =>
            {
                double windowSec = Math.Clamp(config.AverageWindowSeconds, MinAvg, MaxAvg);

                // Add new sample
                percentBuffer.Add((DateTime.UtcNow, percent));

                // Retain only samples within max retention horizon
                DateTime retentionCutoff = DateTime.UtcNow - TimeSpan.FromSeconds(MaxAvg);
                percentBuffer.RemoveAll(p => p.Timestamp < retentionCutoff);

                // Compute average over configured window (or use instant if zero)
                double avgPercent;
                if (windowSec <= 0.0000001)
                {
                    avgPercent = percent;
                }
                else
                {
                    DateTime cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(windowSec);
                    var slice = percentBuffer.Where(p => p.Timestamp >= cutoff).ToList();
                    avgPercent = slice.Count > 0 ? slice.Average(p => p.Value) : percent;
                }

                // Update textual feedback
                OutputText.Text = windowSec <= 0.0000001
                    ? $"Level: {percent:F0}% (Instant)"
                    : $"Level: {percent:F0}% (Avg: {avgPercent:F0}% / {windowSec:0.###}s)";

                double thresholdPercent = config.ThresholdPercent;
                OutputText.Foreground = avgPercent > thresholdPercent ? Brushes.Red : Brushes.White;

                // Update visual bar + threshold line
                UpdateBarLevel(percent);
                UpdateThresholdLine(thresholdPercent);

                // Handle warning triggering logic
                bool over = avgPercent > thresholdPercent;
                if (over)
                {
                    if (config.EnableWarningSound)
                        PlayWarningSound();
                    if (config.EnableOverlay)
                        ShowOrRepositionOverlay();
                    else
                        HideOverlay();
                }
                else
                {
                    HideOverlay();
                }
            });
        }

        // Updates visual bar width to reflect current level
        private void UpdateBarLevel(double percent)
        {
            var ratio = Math.Clamp(percent / 100.0, 0, 1);
            var gridWidth = BarBackground.ActualWidth;
            if (gridWidth > 0)
                BarLevel.Width = gridWidth * ratio;
        }

        // Updates threshold line and label position/value
        private void UpdateThresholdLine(double thresholdPercent)
        {
            var gridWidth = BarBackground.ActualWidth;
            if (gridWidth > 0)
            {
                var ratio = Math.Clamp(thresholdPercent / 100.0, 0, 1);
                var x = gridWidth * ratio;
                ThresholdLine.X1 = x;
                ThresholdLine.X2 = x;
                ThresholdLine.Visibility = Visibility.Visible;
                ThresholdValueLabel.Visibility = Visibility.Visible;

                ThresholdValueLabel.Text = $"{thresholdPercent:F0}%";
                double labelWidth = ThresholdValueLabel.ActualWidth;
                if (labelWidth == 0) labelWidth = 24;
                double offset = x - (labelWidth / 2);
                ThresholdValueLabel.RenderTransform = new TranslateTransform(offset, 0);
            }
        }

        // Plays (or restarts) the warning sound once while preventing concurrent overlap
        private void PlayWarningSound()
        {
            if (!isWarningPlaying && warningOutputDevice != null)
            {
                float volume = 1.0f;
                if (float.TryParse(VolumeBox.Text, out float volInput))
                {
                    volInput = Math.Clamp(volInput, MinVolume, MaxVolume);
                    volume = volInput / 100f;
                }
                warningAudioFile!.Volume = volume;

                isWarningPlaying = true;
                Task.Run(() =>
                {
                    warningAudioFile.Position = 0;
                    warningOutputDevice.Play();
                    while (warningOutputDevice.PlaybackState == PlaybackState.Playing)
                        Thread.Sleep(50);
                    isWarningPlaying = false;
                });
            }
        }

        // Shows overlay centered on selected monitor (cancels pending fade-out)
        private void ShowOrRepositionOverlay()
        {
            if (overlay == null) return;
            var selectedScreen = WinForms.Screen.AllScreens[
                MonitorComboBox.SelectedIndex >= 0 ? MonitorComboBox.SelectedIndex : 0];
            var (dpiX, dpiY) = GetDpi();

            overlay.Left = selectedScreen.Bounds.Left / dpiX +
                           (selectedScreen.Bounds.Width / dpiX - overlay.Width) / 2;
            overlay.Top = selectedScreen.Bounds.Top / dpiY +
                          (selectedScreen.Bounds.Height / dpiY - overlay.Height) / 2;

            overlay.CancelPendingHide(); // Ensures overlay stays visible and cancels fade-out
        }

        // Hides overlay window with fade out effect (if currently visible)
        private void HideOverlay()
        {
            if (overlay != null && overlay.Visibility == Visibility.Visible)
            {
                overlay.FadeOutAndHide();
            }
        }

        // Gets DPI scaling factors for current window (used to convert device px to WPF units)
        private (double dpiX, double dpiY) GetDpi()
        {
            var source = PresentationSource.FromVisual(this);
            if (source != null)
                return (source.CompositionTarget.TransformToDevice.M11,
                        source.CompositionTarget.TransformToDevice.M22);
            return (1.0, 1.0);
        }

        // Handles Select Warning Sound button click, opens file dialog for audio file
        private void SelectSound_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audio Files|*.wav;*.mp3;*.ogg"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                var selectedPath = openFileDialog.FileName;
                SelectedSoundText.Text = System.IO.Path.GetFileName(selectedPath);

                warningAudioFile?.Dispose();
                warningOutputDevice?.Dispose();

                warningAudioFile = new AudioFileReader(selectedPath);
                warningOutputDevice = new WaveOutEvent();
                warningOutputDevice.Init(warningAudioFile);

                // Persist chosen sound path
                config.LastWarningSoundPath = selectedPath;
                ConfigService.Save(config);
            }
        }

        // Handles Select Overlay Image button click, lets user choose a custom overlay
        private void SelectOverlayImage_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                overlayImagePath = openFileDialog.FileName;
                SelectedOverlayText.Text = System.IO.Path.GetFileName(overlayImagePath);

                config.OverlayImagePath = overlayImagePath;
                ConfigService.Save(config);

                InitializeOverlay();
            }
        }

        // Handles sound feature toggle change
        private void ActivateSoundCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            config.EnableWarningSound = ActivateSoundCheckBox.IsChecked == true;
            UpdateVolumeVisibility(config.EnableWarningSound);
            ConfigService.Save(config);
        }

        // Handles overlay feature toggle change
        private void ActivateOverlayCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            config.EnableOverlay = ActivateOverlayCheckBox.IsChecked == true;
            UpdateOverlayDependentVisibility(config.EnableOverlay);
            if (!config.EnableOverlay)
                HideOverlay();
            ConfigService.Save(config);
        }

        // --- Validation Handlers ---

        // Threshold textbox changed -> validate & update UI state
        private void ThresholdBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateThreshold();
            UpdateStartButtonEnabled();
            UpdateMonitoringButtons();
        }

        // Average window textbox changed -> validate & update UI state
        private void AverageWindowBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateAverageWindow();
            UpdateStartButtonEnabled();
            UpdateMonitoringButtons();
        }

        // Volume textbox changed -> validate & update UI state
        private void VolumeBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateVolume();
            UpdateStartButtonEnabled();
            UpdateMonitoringButtons();
        }

        // Volume textbox focus lost -> ensure commit of a valid value if entered
        private void VolumeBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            ValidateVolume();
            UpdateStartButtonEnabled();
            UpdateMonitoringButtons();
        }

        // Validates threshold percent input and persists if valid
        private void ValidateThreshold()
        {
            string txt = ThresholdBox.Text.Trim();
            if (int.TryParse(txt, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) &&
                value >= MinThreshold && value <= MaxThreshold)
            {
                _thresholdValid = true;
                ThresholdBox.BorderBrush = _validBorderBrush;
                ThresholdBox.ToolTip = "Valid threshold (0 - 100)";
                if (!_initializing)
                {
                    config.ThresholdPercent = value;
                    ConfigService.Save(config);
                }
            }
            else
            {
                _thresholdValid = false;
                ThresholdBox.BorderBrush = _invalidBorderBrush;
                ThresholdBox.ToolTip = "Enter integer 0 - 100";
            }
        }

        // Validates average window seconds input and persists if valid
        private void ValidateAverageWindow()
        {
            string txt = AverageWindowBox.Text.Trim();
            if (double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
                value >= MinAvg && value <= MaxAvg)
            {
                _avgValid = true;
                AverageWindowBox.BorderBrush = _validBorderBrush;
                AverageWindowBox.ToolTip = value == 0
                    ? "Instant (no averaging). Range 0 - 5."
                    : $"Averaging {value:0.###}s (0 = instant). Range 0 - 5.";
                if (!_initializing)
                {
                    config.AverageWindowSeconds = value;
                    ConfigService.Save(config);
                }
            }
            else
            {
                _avgValid = false;
                AverageWindowBox.BorderBrush = _invalidBorderBrush;
                AverageWindowBox.ToolTip = "Enter number 0 - 5 (e.g. 0, 0.5, 2, 4.75)";
            }
        }

        // Validates warning sound volume input and persists if valid
        private void ValidateVolume()
        {
            string txt = VolumeBox.Text.Trim();
            if (int.TryParse(txt, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) &&
                value >= MinVolume && value <= MaxVolume)
            {
                _volumeValid = true;
                VolumeBox.BorderBrush = _validBorderBrush;
                VolumeBox.ToolTip = "Valid volume (0 - 200)";
                if (!_initializing)
                {
                    config.WarningSoundVolume = value;
                    ConfigService.Save(config);
                }
            }
            else
            {
                _volumeValid = false;
                VolumeBox.BorderBrush = _invalidBorderBrush;
                VolumeBox.ToolTip = "Enter integer 0 - 200";
            }
        }

        // Returns true if all input fields currently contain valid values
        private bool AllInputsValid() => _thresholdValid && _avgValid && _volumeValid;

        // Enables / disables Start button based on current validation state (when idle)
        private void UpdateStartButtonEnabled()
        {
            if (StartMonitoringButton != null && !IsMonitoring)
                StartMonitoringButton.IsEnabled = AllInputsValid();
        }

        // Handles window closing event, disposes resources and closes overlay
        // <param name="e">CancelEventArgs for closing event</param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Final commit attempt before exit (only save if all valid)
            ValidateThreshold();
            ValidateAverageWindow();
            ValidateVolume();
            if (AllInputsValid())
            {
                ConfigService.Save(config);
            }

            audioService?.Dispose();
            audioService = null;

            if (warningOutputDevice != null)
            {
                warningOutputDevice.Stop();
                warningOutputDevice.Dispose();
                warningOutputDevice = null;
            }
            warningAudioFile?.Dispose();
            warningAudioFile = null;
            isWarningPlaying = false;

            if (overlay != null)
            {
                overlay.Close();
                overlay = null;
            }

            base.OnClosing(e);
        }
    }
}
