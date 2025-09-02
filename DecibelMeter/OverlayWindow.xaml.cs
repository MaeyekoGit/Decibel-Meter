using System.Windows;
using System.Windows.Media.Imaging;

namespace DecibelMeter
{
    // OverlayWindow displays a transparent overlay image on the screen
    public partial class OverlayWindow : Window
    {
        // Initializes new instance of OverlayWindow with specified image
        // <param name="imageFileName"> Filename of overlay image to display</param>
        // Passed by MainWindow.xaml.cs as arg upon creating new instance
        public OverlayWindow(string imageFileName)
        {
            InitializeComponent();
            ConfigureWindow();           // Set up window properties for overlay behavior
            LoadOverlayImage(imageFileName); // Load and display overlay image
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
        private void LoadOverlayImage(string imageFileName)
        {
            try
            {
                // Build pack URI for the resource
                var uri = new Uri($"pack://application:,,,/{imageFileName}", UriKind.Absolute);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = uri;
                bmp.EndInit();
                bmp.Freeze();

                OverlayImage.Source = bmp;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load overlay image resource:\n{ex.Message}");
            }
        }
    }
}
