using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using DecibelMeter.Models;
using NAudio.Wave;
using System.Collections.Generic;

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

        // Initializes new instance of MainWindow and sets up UI and config
        public MainWindow()
        {
            // Load config first
            config = ConfigService.Load();

            InitializeComponent();

            // Apply config before wiring events to avoid premature handler execution
            ApplyConfigToUi();

            // Now wire events (approach 1)
            ActivateSoundCheckBox.Checked += ActivateSoundCheckBox_Changed;
            ActivateSoundCheckBox.Unchecked += ActivateSoundCheckBox_Changed;
            ActivateOverlayCheckBox.Checked += ActivateOverlayCheckBox_Changed;
            ActivateOverlayCheckBox.Unchecked += ActivateOverlayCheckBox_Changed;

            InitializeOverlay();
            LoadLastWarningSound();
        }

        private void ApplyConfigToUi()
        {
            // Apply config to UI
            PopulateDeviceList();
            PopulateMonitorList();
            ThresholdBox.Text = config.ThresholdPercent.ToString();
            VolumeBox.Text = config.WarningSoundVolume.ToString();

            // Feature toggles
            ActivateSoundCheckBox.IsChecked = config.EnableWarningSound;
            ActivateOverlayCheckBox.IsChecked = config.EnableOverlay;

            // Always keep these interactive
            // VolumeBox.IsEnabled = config.EnableWarningSound;  (removed)
            SelectSoundButton.IsEnabled = true;
            SelectOverlayButton.IsEnabled = true;

            // Hide/show volume controls based on sound activation
            UpdateVolumeVisibility(config.EnableWarningSound);

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

        private void UpdateVolumeVisibility(bool visible)
        {
            var vis = visible ? Visibility.Visible : Visibility.Collapsed;
            VolumeLabel.Visibility = vis;
            VolumeBox.Visibility = vis;
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
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                DeviceComboBox.Items.Add(caps.ProductName);
            }
            if (!string.IsNullOrEmpty(config.SelectedDevice))
                DeviceComboBox.SelectedItem = config.SelectedDevice;
        }

        // Populates monitor ComboBox with available screens
        private void PopulateMonitorList()
        {
            foreach (var screen in Screen.AllScreens)
                MonitorComboBox.Items.Add(screen.DeviceName);
            MonitorComboBox.SelectedIndex = config.SelectedMonitor < Screen.AllScreens.Length
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

        // Handles Start Monitoring button click
        // <param name="sender">Button that was clicked</param>
        // <param name="e">Event args</param>
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            config.SelectedDevice = DeviceComboBox.SelectedItem?.ToString() ?? "";
            config.ThresholdPercent = int.TryParse(ThresholdBox.Text, out var th) ? th : 60;
            config.WarningSoundVolume = int.TryParse(VolumeBox.Text, out var vol) ? vol : 100;
            config.SelectedMonitor = MonitorComboBox.SelectedIndex;
            ConfigService.Save(config);

            audioService = new AudioService();
            audioService.VolumeMeasured += OnVolumeMeasured;
            audioService.Start(config.SelectedDevice);
        }

        // Handles Stop Monitoring button click
        // <param name="sender">Button that was clicked</param>
        // <param name="e">Event args</param>
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            audioService?.Dispose();
            audioService = null;
            OutputText.Text = "Monitoring stopped.";
            HideOverlay();

            // Hide the threshold line
            ThresholdLine.Visibility = Visibility.Collapsed;
            ThresholdValueLabel.Visibility = Visibility.Collapsed;
        }

        // Receives percent (0-100)
        private void OnVolumeMeasured(double percent)
        {
            Dispatcher.Invoke(() =>
            {
                percentBuffer.Add((DateTime.UtcNow, percent));

                // Remove values older than 2 seconds
                DateTime cutoff = DateTime.UtcNow.AddSeconds(-2);
                percentBuffer.RemoveAll(p => p.Timestamp < cutoff);

                // Compute average over last 2 seconds
                // Only if average is above for 2 seconds, trigger warning
                double avgPercent = percentBuffer.Count > 0 ? percentBuffer.Average(p => p.Value) : 0.0;

                OutputText.Text = $"Level: {percent:F0}% (Avg: {avgPercent:F0}%)";
                double thresholdPercent = config.ThresholdPercent;
                OutputText.Foreground = avgPercent > thresholdPercent ? Brushes.Red : Brushes.White;

                UpdateBarLevel(percent);
                UpdateThresholdLine(thresholdPercent);

                if (avgPercent > thresholdPercent)
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
                    volInput = Math.Clamp(volInput, 0, 200);
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

        // Shows or repositions overlay window on selected monitor
        private void ShowOrRepositionOverlay()
        {
            if (overlay == null) return;
            var selectedScreen = Screen.AllScreens[
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
            if (!config.EnableOverlay)
                HideOverlay();
            ConfigService.Save(config);
        }

        // Handles window closing event, disposes resources and closes overlay
        // <param name="e">CancelEventArgs for closing event</param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
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
