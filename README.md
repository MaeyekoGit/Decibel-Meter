# Decibel Meter

**Decibel Meter** is a Windows desktop application for monitoring audio input levels in real time. It provides a visual and audio warning system when sound exceeds a user-defined threshold, and can display a transparent overlay image on any monitor as an additional alert.

Originally created due to my headphones isolating too well and thus my speaking volume increasing without me noticing.

(I'm aware it's somewhat sphaghetti code but this my first time creating a desktop application)

## Features

- **Real-Time Decibel Monitoring:**  
  Continuously measures audio input from any available recording device.

- **Customizable Threshold:**  
  Set the decibel level at which warnings are triggered.

- **Visual Feedback:**  
  - Live decibel level bar with a threshold indicator.
  - Output text color changes when threshold is exceeded.

- **Audio Warning:**  
  - Play a custom warning sound when the threshold is crossed.
  - Adjustable warning sound volume.

- **Overlay Alert:**  
  - Displays a transparent, borderless overlay image on the selected monitor when the threshold is exceeded.
  - Overlay image is loaded from a WPF resource (e.g., `overlay.png`).

- **Multi-Monitor Support:**  
  Choose which monitor displays the overlay.

- **Persistent Settings:**  
  Remembers last used device, threshold, monitor, overlay image, and warning sound.

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
   Enter the decibel value at which you want to trigger warnings.

3. **Select Monitor:**  
   Choose which monitor will display the overlay image.

4. **Select Warning Sound (optional):**  
   Click "Select Warning Sound" to choose a custom audio file (`.wav`, `.mp3`, `.ogg`).

5. **Adjust Warning Volume:**  
   Set the warning sound volume (0–200%).

6. **Start Monitoring:**  
   Click "Start" to begin monitoring.  
   - When the input exceeds threshold:
     - The overlay image appears on the selected monitor.
     - A warning sound plays.
     - UI bar and text indicate the warning state.

7. **Stop Monitoring:**  
   Click "Stop" to end monitoring and hide the overlay.

### Overlay Image

- The overlay image is embedded as a WPF resource not meant to be changed by the user.

## Configuration

Settings are saved automatically and loaded on startup, including:
- Selected audio device
- Threshold value
- Selected monitor
- Last used warning sound and volume

## Dependencies

- [NAudio](https://github.com/naudio/NAudio) – for audio input and playback

---

**Decibel Meter** is designed for anyone needing a simple, customizable audio level monitor with visual and audio alerts.  