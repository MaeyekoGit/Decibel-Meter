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
        private bool overlayShown = false;
        private string warningSoundPath = "";
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

        private readonly Brush _validBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
        private readonly Brush _invalidBorderBrush = new SolidColorBrush(Color.FromRgb(170, 51, 51));

        // Prevent validation-triggered saves during initial UI population
        private bool _initializing = true;

        // Convenience flag
        private bool IsMonitoring => audioService != null;

        // Initializes new instance of MainWindow and sets up UI and config
        public MainWindow()
        {
            // Load config first
            config = ConfigService.Load();
            // Clamp stored config if it was previously > 5
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

            // Apply config before wiring events to avoid premature handler execution
            ApplyConfigToUi();

            // Wire events
            ActivateSoundCheckBox.Checked += ActivateSoundCheckBox_Changed;
            ActivateSoundCheckBox.Unchecked += ActivateSoundCheckBox_Changed;
            ActivateOverlayCheckBox.Checked += ActivateOverlayCheckBox_Changed;
            ActivateOverlayCheckBox.Unchecked += ActivateOverlayCheckBox_Changed;

            // Ensure textbox handlers wired
            VolumeBox.TextChanged -= VolumeBox_TextChanged;
            VolumeBox.TextChanged += VolumeBox_TextChanged;
            VolumeBox.LostFocus -= VolumeBox_LostFocus;
            VolumeBox.LostFocus += VolumeBox_LostFocus;

            ThresholdBox.TextChanged -= ThresholdBox_TextChanged;
            ThresholdBox.TextChanged += ThresholdBox_TextChanged;

            AverageWindowBox.TextChanged -= AverageWindowBox_TextChanged;
            AverageWindowBox.TextChanged += AverageWindowBox_TextChanged;

            InitializeOverlay();
            LoadLastWarningSound();

            // Initial validation pass
            ValidateThreshold();
            ValidateAverageWindow();
            ValidateVolume();
            UpdateStartButtonEnabled();
            UpdateMonitoringButtons();

            _initializing = false; // allow saves from now on
        }

        private void ApplyConfigToUi()
        {
            // Apply config to UI
            PopulateDeviceList();
            PopulateMonitorList();
            ThresholdBox.Text = config.ThresholdPercent.ToString(CultureInfo.InvariantCulture);
            AverageWindowBox.Text = config.AverageWindowSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            VolumeBox.Text = config.WarningSoundVolume.ToString(CultureInfo.InvariantCulture);

            // Feature toggles
            ActivateSoundCheckBox.IsChecked = config.EnableWarningSound;
            ActivateOverlayCheckBox.IsChecked = config.EnableOverlay;

            // Always keep these interactive
            // VolumeBox.IsEnabled = config.EnableWarningSound;  (removed)
            SelectSoundButton.IsEnabled = true;
            SelectOverlayButton.IsEnabled = true;

            // Hide/show volume controls based on sound activation
            UpdateVolumeVisibility(config.EnableWarningSound);
            UpdateOverlayDependentVisibility(config.EnableOverlay);

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

        private void UpdateMonitoringButtons()
        {
            if (StartMonitoringButton != null)
                StartMonitoringButton.IsEnabled = !IsMonitoring && AllInputsValid();
            if (StopMonitoringButton != null)
                StopMonitoringButton.IsEnabled = IsMonitoring;
        }

        private void UpdateVolumeVisibility(bool visible)
        {
            var vis = visible ? Visibility.Visible : Visibility.Collapsed;
            VolumeLabel.Visibility = vis;
            VolumeBox.Visibility = vis;
        }

        private void UpdateOverlayDependentVisibility(bool overlayEnabled)
        {
            var vis = overlayEnabled ? Visibility.Visible : Visibility.Collapsed;
            MonitorLabel.Visibility = vis;
            MonitorComboBox.Visibility = vis;
        }

        private void LoadLastWarningSound()
        {
            if (!string.IsNullOrEmpty(config.LastWarningSoundPath) &&
                System.IO.File.Exists(config.LastWarningSoundPath))
            {
                warningSoundPath = config.LastWarningSoundPath;
                SelectedSoundText.Text = System.IO.Path.GetFileName(warningSoundPath);

                warningAudioFile?.Dispose();
                warningOutputDevice?.Dispose();

                warningAudioFile = new AudioFileReader(warningSoundPath);
                warningOutputDevice = new WaveOutEvent();
                warningOutputDevice.Init(warningAudioFile);
            }
            else
            {
                SelectedSoundText.Text = "No file selected";
            }
        }

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

            // Final commit pass
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
                // Cleanup if start failed
                audioService.Dispose();
                audioService = null;
                throw;
            }

            UpdateMonitoringButtons();
        }

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

        private void OnVolumeMeasured(double percent)
        {
            Dispatcher.Invoke(() =>
            {
                double windowSec = Math.Clamp(config.AverageWindowSeconds, MinAvg, MaxAvg);

                // Collect sample
                percentBuffer.Add((DateTime.UtcNow, percent));

                // Retain only last MaxAverageWindowSeconds seconds (now 5)
                DateTime retentionCutoff = DateTime.UtcNow - TimeSpan.FromSeconds(MaxAvg);
                percentBuffer.RemoveAll(p => p.Timestamp < retentionCutoff);

                double avgPercent;
                if (windowSec <= 0.0000001)
                {
                    // No averaging mode
                    avgPercent = percent;
                }
                else
                {
                    DateTime cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(windowSec);
                    var slice = percentBuffer.Where(p => p.Timestamp >= cutoff).ToList();
                    avgPercent = slice.Count > 0 ? slice.Average(p => p.Value) : percent;
                }

                OutputText.Text = windowSec <= 0.0000001
                    ? $"Level: {percent:F0}% (Instant)"
                    : $"Level: {percent:F0}% (Avg: {avgPercent:F0}% / {windowSec:0.###}s)";

                double thresholdPercent = config.ThresholdPercent;
                OutputText.Foreground = avgPercent > thresholdPercent ? Brushes.Red : Brushes.White;

                UpdateBarLevel(percent);
                UpdateThresholdLine(thresholdPercent);

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

        // Updates visual bar to reflect current decibel level
        // <param name="percent">Measured percent value</param>
        private void UpdateBarLevel(double percent)
        {
            var ratio = Math.Clamp(percent / 100.0, 0, 1);
            var gridWidth = BarBackground.ActualWidth;
            if (gridWidth > 0)
                BarLevel.Width = gridWidth * ratio;
        }

        // Updates threshold line position in the visual bar
        // <param name="thresholdPercent">Threshold percent value</param>
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

                // Update threshold label value and position
                ThresholdValueLabel.Text = $"{thresholdPercent:F0}%";
                // Center the label horizontally above the threshold line
                double labelWidth = ThresholdValueLabel.ActualWidth;
                // If label width is not available yet, use a default estimate (e.g., 24)
                if (labelWidth == 0) labelWidth = 24;
                // Offset so label is centered above the line
                double offset = x - (labelWidth / 2);
                ThresholdValueLabel.RenderTransform = new TranslateTransform(offset, 0);
            }
        }

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
                        System.Threading.Thread.Sleep(50);
                    isWarningPlaying = false;
                });
            }
        }

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
            overlayShown = true;
        }

        // Hides overlay window with fade out effect
        private void HideOverlay()
        {
            if (overlay != null && overlay.Visibility == Visibility.Visible)
            {
                overlay.FadeOutAndHide();
                overlayShown = false;
            }
        }

        // Gets DPI scaling factors for current window for proper calculation of center
        // <returns>Tuple of (dpiX, dpiY)</returns>
        private (double dpiX, double dpiY) GetDpi()
        {
            var source = PresentationSource.FromVisual(this);
            if (source != null)
                return (source.CompositionTarget.TransformToDevice.M11,
                        source.CompositionTarget.TransformToDevice.M22);
            return (1.0, 1.0);
        }

        // Handles Select Warning Sound button click, opens file dialog for audio file
        // <param name="sender">Button that was clicked</param>
        // <param name="e">Event args</param>
        private void SelectSound_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audio Files|*.wav;*.mp3;*.ogg"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                warningSoundPath = openFileDialog.FileName;
                SelectedSoundText.Text = System.IO.Path.GetFileName(warningSoundPath);

                warningAudioFile?.Dispose();
                warningOutputDevice?.Dispose();

                warningAudioFile = new AudioFileReader(warningSoundPath);
                warningOutputDevice = new WaveOutEvent();
                warningOutputDevice.Init(warningAudioFile);

                // Save selected warning sound path to config
                config.LastWarningSoundPath = warningSoundPath;
                ConfigService.Save(config);
            }
        }

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

        private void ActivateSoundCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            config.EnableWarningSound = ActivateSoundCheckBox.IsChecked == true;
            UpdateVolumeVisibility(config.EnableWarningSound);
            ConfigService.Save(config);
        }

        private void ActivateOverlayCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            config.EnableOverlay = ActivateOverlayCheckBox.IsChecked == true;
            UpdateOverlayDependentVisibility(config.EnableOverlay);
            if (!config.EnableOverlay)
                HideOverlay();
            ConfigService.Save(config);
        }

        // --- Validation Handlers ---

        private void ThresholdBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateThreshold();
            UpdateStartButtonEnabled();
            UpdateMonitoringButtons();
        }

        private void AverageWindowBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateAverageWindow();
            UpdateStartButtonEnabled();
            UpdateMonitoringButtons();
        }

        private void VolumeBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateVolume();
            UpdateStartButtonEnabled();
            UpdateMonitoringButtons();
        }

        private void VolumeBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            ValidateVolume();
            UpdateStartButtonEnabled();
            UpdateMonitoringButtons();
        }

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

        private bool AllInputsValid() => _thresholdValid && _avgValid && _volumeValid;

        private void UpdateStartButtonEnabled()
        {
            if (StartMonitoringButton != null && !IsMonitoring)
                StartMonitoringButton.IsEnabled = AllInputsValid();
        }

        // Handles window closing event, disposes resources and closes overlay
        // <param name="e">CancelEventArgs for closing event</param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Final commit attempt before exit
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
                overlayShown = false;
            }

            base.OnClosing(e);
        }
    }
}
