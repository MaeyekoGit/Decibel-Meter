# Decibel Meter

**Decibel Meter** is a Windows desktop application for monitoring audio input levels in real time. It provides a visual and audio warning system when sound exceeds a user-defined threshold, and can display a transparent overlay image on any monitor as an additional alert.

Originally created due to my headphones isolating too well and thus my speaking volume increasing without me noticing.

(I'm aware it's somewhat spaghetti code but this my first time creating a desktop application)

---

## Features

- **Real-Time Audio Level Monitoring (Percent-based):**  
  Continuously measures audio input from any available recording device and displays the level as a normalized percentage (0–100%).

- **Customizable Threshold (Percent):**  
  Set the percentage level at which warnings are triggered. The threshold is based on a normalized percent scale, making it more intuitive and consistent across devices.

- **Averaged Trigger Logic:**  
  The warning (audio and overlay) is only triggered if the average input level over the last 2 seconds exceeds the threshold, reducing false alarms from short spikes.

- **Sensitivity Adjustment:**  
  Internal sensitivity scaling ensures typical microphones can reach 100% with loud input. (No calibration required.)

- **Visual Feedback:**  
  - Live percentage level bar with a threshold indicator.
  - Output text color changes when the average level exceeds the threshold.
  - **Bar Visual Aids:** 0% and 100% labels above the bar, and a dynamic threshold value label positioned above the threshold indicator.

- **Audio Warning:**  
  - Play a custom warning sound when the threshold is crossed.
  - Adjustable warning sound volume.

- **Overlay Alert:**  
  - Displays a transparent, borderless overlay image on the selected monitor when the threshold is exceeded.
  - Overlay image fades out smoothly when the level drops below the threshold.
  - Overlay image is loaded from a WPF resource (e.g., `overlay.png`).

- **Multi-Monitor Support:**  
  Choose which monitor displays the overlay.

- **Persistent Settings:**  
  Remembers last used device, threshold, monitor, overlay image, and warning sound.

---

## Getting Started

### Prerequisites

- Windows 10 or later
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (recommended)

### Installation

1. **Clone the repository:**

2. **Open the solution in Visual Studio 2022.**

3. **Restore NuGet packages** (NAudio is required).

4. **Build and run the project.**

### Usage

1. **Select Input Device:**  
   Choose your preferred audio input (microphone) from the dropdown.

2. **Set Threshold:**  
   Enter the percentage value at which you want to trigger warnings.

3. **Select Monitor:**  
   Choose which monitor will display the overlay image.

4. **Select Warning Sound (optional):**  
   Click "Select Warning Sound" to choose a custom audio file (`.wav`, `.mp3`, `.ogg`).

5. **Adjust Warning Volume:**  
   Set the warning sound volume (0–200%).

6. **Start Monitoring:**  
   Click "Start" to begin monitoring.  
   - When the average input exceeds threshold:
     - The overlay image appears on the selected monitor and fades out smoothly when the level drops.
     - A warning sound plays.
     - UI bar and text indicate the warning state.

7. **Stop Monitoring:**  
   Click "Stop" to end monitoring and hide the overlay and threshold indicator.

### Overlay Image

- The overlay image is embedded as a WPF resource not meant to be changed by the user.

---

## Configuration

Settings are saved automatically and loaded on startup, including:
- Selected audio device
- Threshold value (percent)
- Selected monitor
- Last used warning sound and volume

---

## Dependencies

- [NAudio](https://github.com/naudio/NAudio) – for audio input and playback

---

## Changelog

### Version 1.1

- **Switched from decibel to percent-based level monitoring** for more intuitive and device-independent readings.
- **Threshold is now set as a percentage** (0–100%) instead of dB.
- **Averaged trigger logic:** Warnings are only triggered if the average level over the last 2 seconds exceeds the threshold avoiding false positives.
- **Sensitivity adjustment:** Internal scaling ensures typical microphones can reach 100% with loud input.
- **Overlay fade-out:** Overlay image now fades out smoothly over 1 second instead of disappearing instantly.
- **Bar visual aids:** Added 0% and 100% labels above the bar, and a dynamic threshold value label positioned above the threshold indicator.
- **Threshold indicator is hidden until monitoring starts** to avoid visual clutter.
- **Numerous UI/UX overhaul/improvements** for clarity and usability.

---

**Decibel Meter** is designed for anyone needing a simple, customizable audio level monitor with visual and audio alerts.