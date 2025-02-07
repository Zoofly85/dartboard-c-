using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Drawing;              // For System.Drawing.Point and PointF
using System.Drawing.Imaging;      // For PixelFormat

namespace DartboardWPF
{
    public partial class MainWindow : Window
    {
        // ======================================
        // Configuration Constants
        // ======================================
        private const int IMAGE_WIDTH = 1280;
        private const int IMAGE_HEIGHT = 720;

        // Dartboard dimensions (in mm)
        private const double DARTBOARD_DIAMETER_MM = 451;
        private const double BULLSEYE_RADIUS_MM = 6.35;
        private const double OUTER_BULL_RADIUS_MM = 15.9;
        private const double TRIPLE_RING_INNER_RADIUS_MM = 99;
        private const double TRIPLE_RING_OUTER_RADIUS_MM = 107;
        private const double DOUBLE_RING_INNER_RADIUS_MM = 162;
        private const double DOUBLE_RING_OUTER_RADIUS_MM = 170;

        // Computed conversion and radii (pixels)
        private double PIXELS_PER_MM = IMAGE_HEIGHT / DARTBOARD_DIAMETER_MM;
        private int BULLSEYE_RADIUS_PX, OUTER_BULL_RADIUS_PX, TRIPLE_RING_INNER_RADIUS_PX, TRIPLE_RING_OUTER_RADIUS_PX,
                    DOUBLE_RING_INNER_RADIUS_PX, DOUBLE_RING_OUTER_RADIUS_PX;
        private System.Drawing.Point center;

        // Destination points (warped dartboard is centered and scaled)
        // Order: Top, Right, Bottom, Left.
        private System.Drawing.PointF[] dstPoints;

        // EmguCV Mats for the images
        private Mat? originalImage;
        private Mat? warpedImage;

        // List to store the 4 points selected on the original image.
        private readonly List<System.Drawing.PointF> selectedPoints = new List<System.Drawing.PointF>();

        // Mode flags:
        // If false, we are still selecting the 4 points.
        // If true, we have warped the image and now process dart clicks.
        private bool modeWarped;

        public MainWindow()
        {
            InitializeComponent();
            InitializeConstants();
            InstructionTextBlock.Text = "Load an image and select 4 points (Top, Right, Bottom, Left).";
        }

        /// <summary>
        /// Compute constants and destination points.
        /// </summary>
        private void InitializeConstants()
        {
            PIXELS_PER_MM = (double)IMAGE_HEIGHT / DARTBOARD_DIAMETER_MM;
            BULLSEYE_RADIUS_PX = (int)(BULLSEYE_RADIUS_MM * PIXELS_PER_MM);
            OUTER_BULL_RADIUS_PX = (int)(OUTER_BULL_RADIUS_MM * PIXELS_PER_MM);
            TRIPLE_RING_INNER_RADIUS_PX = (int)(TRIPLE_RING_INNER_RADIUS_MM * PIXELS_PER_MM);
            TRIPLE_RING_OUTER_RADIUS_PX = (int)(TRIPLE_RING_OUTER_RADIUS_MM * PIXELS_PER_MM);
            DOUBLE_RING_INNER_RADIUS_PX = (int)(DOUBLE_RING_INNER_RADIUS_MM * PIXELS_PER_MM);
            DOUBLE_RING_OUTER_RADIUS_PX = (int)(DOUBLE_RING_OUTER_RADIUS_MM * PIXELS_PER_MM);
            center = new System.Drawing.Point(IMAGE_WIDTH / 2, IMAGE_HEIGHT / 2);

            dstPoints = new System.Drawing.PointF[]
            {
                new System.Drawing.PointF(center.X, center.Y - DOUBLE_RING_OUTER_RADIUS_PX), // Top
                new System.Drawing.PointF(center.X + DOUBLE_RING_OUTER_RADIUS_PX, center.Y), // Right
                new System.Drawing.PointF(center.X, center.Y + DOUBLE_RING_OUTER_RADIUS_PX), // Bottom
                new System.Drawing.PointF(center.X - DOUBLE_RING_OUTER_RADIUS_PX, center.Y)  // Left
            };
        }

        // -------------------------------
        // UI Button Click Handlers
        // -------------------------------

        /// <summary>
        /// Loads an image file using an OpenFileDialog.
        /// </summary>
        private void LoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Image Files|*.jpg;*.jpeg;*.png|All Files|*.*";
            if (dlg.ShowDialog() == true)
            {
                originalImage = CvInvoke.Imread(dlg.FileName, ImreadModes.Color);
                if (originalImage != null)
                {
                    CvInvoke.Resize(originalImage, originalImage, new System.Drawing.Size(IMAGE_WIDTH, IMAGE_HEIGHT));
                    // Reset all state variables
                    selectedPoints.Clear();
                    modeWarped = false;
                    warpedImage = null;
                    ScoreTextBlock.Text = "";
                    InstructionTextBlock.Text = "Select 4 points on the image (Top, Right, Bottom, Left).";
                    DisplayImage.Source = ToBitmapSource(originalImage);
                }
            }
        }

        /// <summary>
        /// Resets the current selection so a new image or new selection can be made.
        /// </summary>
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (originalImage != null)
            {
                selectedPoints.Clear();
                modeWarped = false;
                warpedImage = null;
                ScoreTextBlock.Text = "";
                InstructionTextBlock.Text = "Select 4 points on the image (Top, Right, Bottom, Left).";
                DisplayImage.Source = ToBitmapSource(originalImage);
            }
        }

        // -------------------------------
        // Mouse Click Handler on the Image Control
        // -------------------------------

        /// <summary>
        /// Handles mouse clicks on the displayed image.
        /// In selection mode, records points; in warped mode, computes the score.
        /// </summary>
        private void DisplayImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Point mousePos = e.GetPosition(DisplayImage);

            if (DisplayImage.Source == null)
                return;

            // Map the mouse position from the control to the image's pixel coordinates.
            double imgControlWidth = DisplayImage.ActualWidth;
            double imgControlHeight = DisplayImage.ActualHeight;
            BitmapSource bmpSrc = DisplayImage.Source as BitmapSource;
            double imgPixelWidth = bmpSrc.PixelWidth;
            double imgPixelHeight = bmpSrc.PixelHeight;
            double scaleX = imgPixelWidth / imgControlWidth;
            double scaleY = imgPixelHeight / imgControlHeight;

            float x = (float)(mousePos.X * scaleX);
            float y = (float)(mousePos.Y * scaleY);

            if (!modeWarped)
            {
                // --- Selection mode: record the clicked points ---
                if (selectedPoints.Count < 4)
                {
                    selectedPoints.Add(new System.Drawing.PointF(x, y));
                    // Draw a small green circle on a temporary copy of the original image
                    Mat temp = originalImage.Clone();
                    foreach (var pt in selectedPoints)
                    {
                        CvInvoke.Circle(temp, new System.Drawing.Point((int)pt.X, (int)pt.Y), 5, new MCvScalar(0, 255, 0), -1);
                    }
                    DisplayImage.Source = ToBitmapSource(temp);

                    if (selectedPoints.Count == 4)
                    {
                        // When 4 points are selected, compute the perspective transform.
                        Mat perspectiveMatrix = CvInvoke.GetPerspectiveTransform(selectedPoints.ToArray(), dstPoints);
                        warpedImage = new Mat();
                        CvInvoke.WarpPerspective(originalImage, warpedImage, perspectiveMatrix, new System.Drawing.Size(IMAGE_WIDTH, IMAGE_HEIGHT));
                        // Overlay the spider (rings and radial lines)
                        DrawSpiderOverlay(warpedImage);
                        modeWarped = true;
                        InstructionTextBlock.Text = "Now click on the warped dartboard to check the score.";
                        DisplayImage.Source = ToBitmapSource(warpedImage);
                    }
                }
            }
            else
            {
                // --- Warped mode: compute the score on click ---
                int score = CalculateScore((int)x, (int)y);
                ScoreTextBlock.Text = $"Clicked at ({(int)x}, {(int)y}) Score: {score}";
                // Create a temporary copy of the warped image and draw a red circle and score text.
                Mat temp = warpedImage.Clone();
                CvInvoke.Circle(temp, new System.Drawing.Point((int)x, (int)y), 5, new MCvScalar(0, 0, 255), -1);
                CvInvoke.PutText(temp, score.ToString(), new System.Drawing.Point((int)x + 10, (int)y - 10),
                    FontFace.HersheySimplex, 1.0, new MCvScalar(0, 0, 255), 2);
                DisplayImage.Source = ToBitmapSource(temp);
            }
        }

        // -------------------------------
        // Score Calculation and Drawing Functions
        // -------------------------------

        /// <summary>
        /// Computes the dart score based on the clicked (x,y) coordinates in the warped image.
        /// </summary>
        private int CalculateScore(int x, int y)
        {
            // Compute the vector from the center
            int dx = x - center.X;
            int dy = y - center.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double angle = Math.Atan2(dy, dx);
            if (angle < 0)
                angle += 2 * Math.PI;

            // Define dartboard sectors (clockwise order)
            int[] sectorScores = new int[] { 10, 15, 2, 17, 3, 19, 7, 16, 8, 11,
                                             14, 9, 12, 5, 20, 1, 18, 4, 13, 6 };
            int sectorIndex = (int)(angle / (2 * Math.PI) * 20);
            int baseScore = sectorScores[sectorIndex];

            if (distance <= BULLSEYE_RADIUS_PX)
                return 50;
            else if (distance <= OUTER_BULL_RADIUS_PX)
                return 25;
            else if (distance > TRIPLE_RING_INNER_RADIUS_PX && distance <= TRIPLE_RING_OUTER_RADIUS_PX)
                return baseScore * 3;
            else if (distance > DOUBLE_RING_INNER_RADIUS_PX && distance <= DOUBLE_RING_OUTER_RADIUS_PX)
                return baseScore * 2;
            else if (distance <= DOUBLE_RING_OUTER_RADIUS_PX)
                return baseScore;
            else
                return 0;
        }

        /// <summary>
        /// Draws the spider overlay (rings and 20 radial lines) onto the provided image.
        /// </summary>
        private void DrawSpiderOverlay(Mat img)
        {
            // Draw concentric circles for the rings.
            CvInvoke.Circle(img, center, DOUBLE_RING_OUTER_RADIUS_PX, new MCvScalar(0, 0, 0), 2);
            CvInvoke.Circle(img, center, DOUBLE_RING_INNER_RADIUS_PX, new MCvScalar(0, 0, 0), 2);
            CvInvoke.Circle(img, center, TRIPLE_RING_OUTER_RADIUS_PX, new MCvScalar(0, 0, 0), 2);
            CvInvoke.Circle(img, center, TRIPLE_RING_INNER_RADIUS_PX, new MCvScalar(0, 0, 0), 2);
            CvInvoke.Circle(img, center, OUTER_BULL_RADIUS_PX, new MCvScalar(0, 0, 0), 2);
            CvInvoke.Circle(img, center, BULLSEYE_RADIUS_PX, new MCvScalar(0, 0, 0), 2);

            // Draw 20 radial lines.
            for (int i = 0; i < 20; i++)
            {
                double theta = i * (2 * Math.PI / 20);
                int x1 = (int)(center.X + Math.Cos(theta) * OUTER_BULL_RADIUS_PX);
                int y1 = (int)(center.Y + Math.Sin(theta) * OUTER_BULL_RADIUS_PX);
                int x2 = (int)(center.X + Math.Cos(theta) * DOUBLE_RING_OUTER_RADIUS_PX);
                int y2 = (int)(center.Y + Math.Sin(theta) * DOUBLE_RING_OUTER_RADIUS_PX);
                CvInvoke.Line(img, new System.Drawing.Point(x1, y1), new System.Drawing.Point(x2, y2), new MCvScalar(0, 0, 0), 2);
            }
        }

        // -------------------------------
        // Helper: Conversion from EmguCV Mat to BitmapSource for WPF.
        // -------------------------------
        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        public static BitmapSource ToBitmapSource(Mat mat)
        {
            var image = mat.ToImage<Bgr, byte>();
            var bitmap = image.ToBitmap();
            IntPtr ptr = bitmap.GetHbitmap(); // obtain the Hbitmap

            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    ptr, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(ptr); // release the HBitmap
                bitmap.Dispose();
                image.Dispose();
            }
        }
    }
}
