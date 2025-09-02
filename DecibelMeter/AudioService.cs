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
            double sum = 0;
            int samples = e.BytesRecorded / 2; // 16-bit audio
            for (int i = 0; i < samples; i++)
            {
                short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                double sample32 = sample / 32768.0;
                sum += sample32 * sample32;
            }

            double rms = Math.Sqrt(sum / samples);
            double reference = 0.001; // adjust based on mic sensitivity
            double decibels = 20 * Math.Log10(rms / reference);

            // Clamp to 0 minimum for regualr decibel readings
            decibels = Math.Max(decibels, 0);

            VolumeMeasured?.Invoke(decibels);
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
            waveIn?.StopRecording();
            waveIn?.Dispose();
        }
    }
}
