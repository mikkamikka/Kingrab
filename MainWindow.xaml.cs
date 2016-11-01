//------------------------------------------------------------------------------
// 
//     Author: Mikhail Chernov  (https://github.com/mikkamikka/)
// 
//------------------------------------------------------------------------------

namespace Kingrab
{
    using System;
    using System.Threading;
    using System.Windows.Threading;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using Accord;
    using Accord.Video;
    using Accord.Video.FFMPEG;

    using System.Drawing;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Map depth range to byte range
        /// </summary>
        /// 

        private int MaxRange = 2000;

        private int MapDepthToByte = 2500 / 256;
        
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        KinectSensor kinectSensor = null;

        /// <summary>
        /// Reader for depth frames
        /// </summary>
        private DepthFrameReader depthFrameReader = null;
        private DepthFrameReader depthFrameReader2 = null;

        /// <summary>
        /// Description of the data contained in the depth frame
        /// </summary>
        private FrameDescription depthFrameDescription = null;
        static FrameDescription depthFrameDescription2 = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        static WriteableBitmap depthBitmap = null;
        //static WriteableBitmap depthBitmap2 = null;

        static Bitmap frame = null;

        /// <summary>
        /// Intermediate storage for frame data converted to color
        /// </summary>
        private byte[] depthPixels = null;
        private byte[] depthPixels2 = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        // create instance of video writer
        static VideoFileWriter writer = new VideoFileWriter();

        static bool isCaptureActive = false;

        Stopwatch stopwatch = new Stopwatch();

        private int bitrate;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();

            // open the reader for the depth frames
            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();

            // wire handler for frame arrival
            this.depthFrameReader.FrameArrived += this.Reader_FrameArrived;

            // get FrameDescription from DepthFrameSource
            this.depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
            depthFrameDescription2 = this.kinectSensor.DepthFrameSource.FrameDescription;

            // allocate space to put the pixels being received and converted
            this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];
            this.depthPixels2 = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];

            // create the bitmap to display
            depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);
            //depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray32Float, null);


            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            //writer.FrameRate = 30;
            //writer.Codec = "WMV3";
            //writer.Quality = 50;

            maxRangeLabel.Text = slider.Value.ToString();

            MapDepthToByte = (int)slider.Value / 256;

            bitrate = 3000;

            Thread worker = new Thread( () => WriterAVI( kinectSensor ) );

            worker.Start();
        }

        int scale( int valueIn, int baseMin, int baseMax, int limitMin, int limitMax) {
			return ((limitMax - limitMin) * (valueIn - baseMin) / (baseMax - baseMin)) + limitMin;
		}
        float scale(float valueIn, float baseMin, float baseMax, float limitMin, float limitMax)
        {
            return ((limitMax - limitMin) * (valueIn - baseMin) / (baseMax - baseMin)) + limitMin;
        }

        void WriterAVI( KinectSensor kinectSensor )
        {
            DepthFrameReader depthFrameReader2 = null;
            depthFrameReader2 = kinectSensor.DepthFrameSource.OpenReader();

            // wire handler for frame arrival
            depthFrameReader2.FrameArrived += Reader_FrameArrived2;


        }

        void Reader_FrameArrived2(object sender, DepthFrameArrivedEventArgs e)
        {
            if (isCaptureActive)
            {
                bool depthFrameProcessed = false;

                WriteableBitmap depthBitmap2 = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);

                using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
                {
                    if (depthFrame != null)
                    {
                        // the fastest way to process the body index data is to directly access 
                        // the underlying buffer
                        using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                        {
                            // verify data and write the color data to the display bitmap
                            if (((depthFrameDescription2.Width * depthFrameDescription2.Height) == (depthBuffer.Size / depthFrameDescription2.BytesPerPixel)) &&
                                (depthFrameDescription2.Width == depthBitmap2.PixelWidth) && (depthFrameDescription2.Height == depthBitmap2.PixelHeight))
                            {
                                // Note: In order to see the full range of depth (including the less reliable far field depth)
                                // we are setting maxDepth to the extreme potential depth threshold
                                //ushort maxDepth = ushort.MaxValue;
                                ushort maxDepth = (ushort)MaxRange;

                                // If you wish to filter by reliable depth distance, uncomment the following line:
                                //// maxDepth = depthFrame.DepthMaxReliableDistance

                                this.ProcessDepthFrameData2(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                                depthFrameProcessed = true;
                            }
                        }
                    }
                }

                if (depthFrameProcessed)
                {
                
                    depthBitmap2.WritePixels(
                        new Int32Rect(0, 0, depthBitmap2.PixelWidth, depthBitmap2.PixelHeight),
                        this.depthPixels2,
                        depthBitmap2.PixelWidth,
                        0);      
                

                        using (MemoryStream outStream = new MemoryStream())
                        {
                            BitmapEncoder enc = new BmpBitmapEncoder();
                            enc.Frames.Add(BitmapFrame.Create((BitmapSource)depthBitmap2));
                            enc.Save(outStream);
                            frame = new System.Drawing.Bitmap(outStream);
                        }

                    if (writer.IsOpen)
                    {
                        try
                        {
                            writer.WriteVideoFrame(frame);
                        }
                        catch (Exception)
                        {
                            //Console.Write(ToString);
                        }
                        
                    }
                    
                }
            }
        }



        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return depthBitmap;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.depthFrameReader != null)
            {
                // DepthFrameReader is IDisposable
                this.depthFrameReader.Dispose();
                this.depthFrameReader = null;
            }

            if (this.depthFrameReader2 != null)
            {
                // DepthFrameReader is IDisposable
                this.depthFrameReader2.Dispose();
                this.depthFrameReader2 = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }

            writer.Dispose();
        }

        /// <summary>
        /// Handles the user clicking on the screenshot button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void ScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            if (depthBitmap != null)
            {
                // create a png bitmap encoder which knows how to save a .png file
                BitmapEncoder encoder = new PngBitmapEncoder();

                // create frame from the writable bitmap and add to encoder
                encoder.Frames.Add(BitmapFrame.Create(depthBitmap));

                string time = System.DateTime.UtcNow.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

                string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

                string path = Path.Combine(myPhotos, "KinectScreenshot-Depth-" + time + ".png");

                // write the new file to disk
                try
                {
                    // FileStream is IDisposable
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }

                    this.StatusText = string.Format(CultureInfo.CurrentCulture, Properties.Resources.SavedScreenshotStatusTextFormat, path);
                }
                catch (IOException)
                {
                    this.StatusText = string.Format(CultureInfo.CurrentCulture, Properties.Resources.FailedScreenshotStatusTextFormat, path);
                }
            }
        }

        /// <summary>
        /// Handles the depth frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == depthBitmap.PixelHeight))
                        {
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            //ushort maxDepth = ushort.MaxValue;
                            ushort maxDepth = (ushort)MaxRange;

                            // If you wish to filter by reliable depth distance, uncomment the following line:
                            //// maxDepth = depthFrame.DepthMaxReliableDistance

                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            depthFrameProcessed = true;
                        }
                    }
                }
            }

            if (depthFrameProcessed)
            {
                this.RenderDepthPixels();

                if (isCaptureActive)
                {

                    //using (MemoryStream outStream = new MemoryStream())
                    //{
                    //    BitmapEncoder enc = new BmpBitmapEncoder();
                    //    enc.Frames.Add(BitmapFrame.Create((BitmapSource)depthBitmap));
                    //    enc.Save(outStream);
                    //    frame = new System.Drawing.Bitmap(outStream);
                    //}

                    //writer.AddFrame(frame);

                    Timing.Text = stopwatch.Elapsed.TotalSeconds.ToString();
                }
            }
        }

        /// <summary>
        /// Directly accesses the underlying image buffer of the DepthFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the depthFrameData pointer.
        /// </summary>
        /// <param name="depthFrameData">Pointer to the DepthFrame image data</param>
        /// <param name="depthFrameDataSize">Size of the DepthFrame image data</param>
        /// <param name="minDepth">The minimum reliable depth value for the frame</param>
        /// <param name="maxDepth">The maximum reliable depth value for the frame</param>
        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;

            // convert depth to a visual representation
            for (int i = 0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i)
            {
                // Get the depth for this pixel
                ushort depth = frameData[i];

                // To convert to a byte, we're mapping the depth value to the byte range.
                // Values outside the reliable depth range are mapped to 0 (black).
                //this.depthPixels[i] = (byte) (depth >= minDepth && depth <= maxDepth ? ( 255+56 - depth / MapDepthToByte ) : 0);

                //
                this.depthPixels[i] = (byte)(depth > minDepth && depth <= maxDepth ? scale(depth, 500, MaxRange, 255, 0) : 0);
                //if (depth <= minDepth && depth > 0)
                //{
                //    depthPixels[i] = (byte)255;
                //}else
                //    if (depth <= maxDepth)
                //    {
                //        depthPixels[i] = (byte)scale(depth, 500, MaxRange, 255, 0);
                //    }else
                //    {
                //        depthPixels[i] = (byte)0;
                //    }
             }
        }

        private unsafe void ProcessDepthFrameData2(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;

            // convert depth to a visual representation
            for (int i = 0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i)
            {
                // Get the depth for this pixel
                ushort depth = frameData[i];

                // To convert to a byte, we're mapping the depth value to the byte range.
                // Values outside the reliable depth range are mapped to 0 (black).
                //depthPixels2[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (255 + 56 - depth / MapDepthToByte) : 0);
                depthPixels2[i] = (byte)(depth >= minDepth && depth <= maxDepth ? scale(depth, 500, MaxRange, 255, 0) : 0);
                //if (depth < minDepth && depth > 0) depthPixels2[i] = (byte)255;
            }
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        private void RenderDepthPixels()
        {
            depthBitmap.WritePixels(
                new Int32Rect(0, 0, depthBitmap.PixelWidth, depthBitmap.PixelHeight),
                this.depthPixels,
                depthBitmap.PixelWidth,
                0);                       
        }        

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            isCaptureActive = false;

            string time = DateTime.UtcNow.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

            string myVideos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

            string path = Path.Combine(myVideos, "KinectRecords");

            bool isDirReady = createDirectory(path);

            if (isDirReady)
            {

                string rec_path = Path.Combine(path, "Kinect-Depth-" + time + ".ogg");

                writer.Open(rec_path, this.depthFrameDescription.Width, this.depthFrameDescription.Height, 30, VideoCodec.Theora, bitrate * 1024);

                stopwatch.Start();

                isCaptureActive = true;

                PathString.Text = rec_path;

            }        

        }

        private bool createDirectory(string path)
        {
            try
            {
                // Determine whether the directory exists.
                if (Directory.Exists(path))
                {
                    Console.WriteLine("That path exists already.");
                    return true;
                }

                // Try to create the directory.
                DirectoryInfo di = Directory.CreateDirectory(path);
                Console.WriteLine("The directory was created successfully at {0}.", Directory.GetCreationTime(path));

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("The process failed: {0}", e.ToString());
            }
            finally { }

            return false;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            isCaptureActive = false;
            writer.Close();

            stopwatch.Stop();
            stopwatch.Reset();

            StartButton.IsChecked = false;
        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {            

            MaxRange = (int)slider.Value;

            maxRangeLabel.Text = MaxRange.ToString();

            MapDepthToByte = MaxRange / 256;
        }

        private void BitrateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            bitrate = (int)e.NewValue;

            BitrateLabel.Text = bitrate.ToString();
        }
    }
}
