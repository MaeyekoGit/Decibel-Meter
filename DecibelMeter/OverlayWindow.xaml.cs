using System.Windows;
using System.IO;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace DecibelMeter
{
    // OverlayWindow displays a transparent overlay image on the screen
    public partial class OverlayWindow : Window
    {
        private bool _pendingHide = false;

        // Change constructor and LoadOverlayImage:
        public OverlayWindow(string imageFilePath)
        {
            InitializeComponent();
            ConfigureWindow();
            LoadOverlayImage(imageFilePath);
        }

        // Configure window properties to make it transparent + borderless overlay
        private void ConfigureWindow()
        {
            AllowsTransparency = true;           // Enable window transparency
            WindowStyle = WindowStyle.None;      // Remove window borders and title bar
            Background = null;                   // No background, fully transparent
            Topmost = true;                      // Keep window above all others
            ShowInTaskbar = false;               // Do not show in taskbar
            ResizeMode = ResizeMode.NoResize;    // Prevent resizing
        }

        // Loads specified image and sets it as overlay image
        private void LoadOverlayImage(string imageFilePath)
        {
            try
            {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;

                // Convert relative path to absolute if needed
                string resolvedPath = imageFilePath;
                if (!Path.IsPathRooted(imageFilePath))
                {
                    // Combine with the application's base directory
                    resolvedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imageFilePath);
                }

                if (File.Exists(resolvedPath))
                {
                    bmp.UriSource = new Uri(resolvedPath, UriKind.Absolute);
                }
                else
                {
                    MessageBox.Show($"Overlay image not found: {resolvedPath}");
                    return;
                }

                bmp.EndInit();
                bmp.Freeze();
                OverlayImage.Source = bmp;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load overlay image:\n{ex.Message}");
            }
        }

        // Fades out the overlay over 0.5 second, then hides it
        public void FadeOutAndHide()
        {
            _pendingHide = true;
            var fade = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromSeconds(0.5)));
            fade.Completed += (s, e) =>
            {
                // Only hide if not re-shown in the meantime
                if (_pendingHide)
                {
                    this.Visibility = Visibility.Hidden;
                    this.Opacity = 1.0;
                }
            };
            this.BeginAnimation(Window.OpacityProperty, fade);
        }

        // Call this when showing overlay to cancel pending hide
        public void CancelPendingHide()
        {
            _pendingHide = false;
            this.BeginAnimation(Window.OpacityProperty, null); // Cancel animation
            this.Opacity = 1.0;
            this.Visibility = Visibility.Visible;
        }
    }
}
