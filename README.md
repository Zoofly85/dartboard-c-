# Dartboard Score Calculator

A WPF application that uses computer vision (EmguCV) to calculate dart scores from images of a dartboard. The application allows users to calibrate a dartboard image and then calculate scores by clicking on points.

## Features

- Load and calibrate dartboard images
- Perspective transformation for accurate scoring
- Interactive point selection for calibration
- Real-time score calculation
- Visual feedback with:
  - Green dots for calibration points
  - Red dots for scoring points
  - Score display next to clicked points
  - Overlay of dartboard rings and sectors

## Requirements

- Windows OS
- .NET 8.0 or later
- Dependencies (NuGet packages):
  - Emgu.CV (4.8.1.5350)
  - Emgu.CV.runtime.windows (4.8.1.5350)
  - Emgu.CV.Bitmap (4.8.1.5350)
  - System.Drawing.Common (8.0.0)

## Installation

1. Clone the repository:
```bash
git clone https://github.com/Zoofly85/dartboard-c-
```

2. Open the solution in Visual Studio or your preferred IDE

3. Restore NuGet packages

4. Build and run the application

## Usage

1. Launch the application
2. Click "Load Image" to select a dartboard image
3. Calibrate the dartboard by clicking 4 points in this order:
   - Top edge (at the 20)
   - Right edge (at the 6)
   - Bottom edge (at the 3)
   - Left edge (at the 11)
4. After calibration, click anywhere on the transformed dartboard to calculate scores
5. Use the "Reset" button to start over with a new image or recalibrate

## Scoring System

- Bullseye: 50 points
- Outer Bull: 25 points
- Triple Ring: 3× segment value
- Double Ring: 2× segment value
- Single: segment value

## License

This project is licensed under the MIT License - see the LICENSE file for details.
