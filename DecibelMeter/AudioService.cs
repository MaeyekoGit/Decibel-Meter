using NAudio.Wave;

namespace DecibelMeter
{
    public class AudioService : IDisposable
    {
        private WaveInEvent? waveIn;
        public event Action<double>? VolumeMeasured;

        public void Start(string deviceName)
        {
            int deviceIndex = FindDeviceIndex(deviceName);
            if (deviceIndex < 0) deviceIndex = 0;

            waveIn = new WaveInEvent
            {
                DeviceNumber = deviceIndex,
                WaveFormat = new WaveFormat(44100, 1)
            };

            waveIn.DataAvailable += OnDataAvailable;
            waveIn.StartRecording();
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            int samples = e.BytesRecorded / 2; // 16-bit mono
            if (samples <= 0) return;

            double sum = 0;
            for (int i = 0; i < samples; i++)
            {
                short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                double sample32 = sample / 32768.0; // -1..+1
                sum += sample32 * sample32;
            }

            double rms = Math.Sqrt(sum / samples);                // 0..1
            const double fullScaleSineRms = 1.0 / 1.41421356237;   // ≈0.70710678
            const double sensitivity = 2.5; // Increase or decrease as needed to adjust sensitivity
            double percent = (rms / fullScaleSineRms) * 100.0 * sensitivity;
            percent = Math.Clamp(percent, 0.0, 100.0);

            VolumeMeasured?.Invoke(percent);
        }

        private int FindDeviceIndex(string deviceName)
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                if (caps.ProductName == deviceName)
                    return i;
            }
            return -1;
        }

        public void Dispose()
        {
            if (waveIn != null)
            {
                waveIn.DataAvailable -= OnDataAvailable;
                waveIn.StopRecording();
                waveIn.Dispose();
            }
        }
    }
}
