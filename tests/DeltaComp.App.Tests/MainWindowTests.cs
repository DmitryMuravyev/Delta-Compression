using Avalonia.Headless.XUnit;
using Xunit;

namespace DeltaComp.Tests
{
    public class MainWindowTests
    {
        private static string TestData(string name)
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "tests", "TestData"))) dir = Path.GetDirectoryName(dir);
            Assert.NotNull(dir);
            return Path.Combine(dir!, "tests", "TestData", name);
        }

        private static string TempHeader() => Path.Combine(Path.GetTempPath(), "deltacomp_" + Guid.NewGuid().ToString("N") + ".h");

        // Compress the same channels directly with the core library
        private static long CompressDirect(List<Channel> channels, ImageDataSource image, byte blockSizeBits, DeltaOption option, byte square)
        {
            long bytes = 0;
            Compressor compressor = new Compressor(channels, (ushort)image.Width, (ushort)image.Height, true, _ => bytes++, (_, _) => { });
            compressor.BlockSizeBits = blockSizeBits;
            compressor.DeltaCalculationOption = option;
            compressor.SplitToSquares = square > 1;
            compressor.WidthOfSquare = square;
            compressor.Compress(null);
            return bytes;
        }

        [AvaloniaTheory]
        [InlineData("Logo_240_Color.png", 1)]       // RGB, Auto settings
        [InlineData("Gray_Gradient_240.png", 0)]    // Grayscale, Manual settings
        [InlineData("RGB_24bits_palette_sample_image.png", 1)] // RGBA
        public async Task Image_IsCompressedAndExported(string file, int settingsMode)
        {
            MainWindow window = new MainWindow();
            window.Show();
            window.SettingsModeComboBox.SelectedIndex = settingsMode;

            await window.OpenImageAsync(TestData(file));
            Assert.True(window.CompressButton.IsEnabled);
            Assert.NotNull(window.ImageDestinationViewer.Source);

            string header = TempHeader();
            try
            {
                await window.CompressAsync(header);

                Assert.Equal("Compress", window.CompressButton.Content);
                Assert.EndsWith("%", window.CompressionEfficiencyLabel.Text);
                Assert.NotEqual("0.00%", window.CompressionEfficiencyLabel.Text);
                string text = File.ReadAllText(header);
                Assert.Contains("0x", text);

                // The UI produces exactly what the core library produces with the shown settings
                ImageDataSource image = ImageDataSource.Load(TestData(file), null);
                List<Channel> channels = image.Planes.Select(p => (Channel)new ImageChannel(image, p.Type)).ToList();
                byte square = (window.ImageSplitIntoSquaresComboBox.SelectedItem?.ToString() is string s && s.Contains('x')) ? byte.Parse(s.Split('x')[0]) : (byte)1;
                long expected = CompressDirect(channels, image, (byte)(int)window.BlockSizeBitsComboBox.SelectedItem!,
                    (DeltaOption)window.DeltaOptionComboBox.SelectedIndex, square);
                Assert.Equal(expected, CountArrayBytes(text));
            }
            finally
            {
                File.Delete(header);
            }
        }

        [AvaloniaFact]
        public async Task Grayscale_ConversionReducesChannels()
        {
            MainWindow window = new MainWindow();
            window.Show();
            await window.OpenImageAsync(TestData("Logo_240_Color.png"));
            Assert.Equal(3, window.ImageChannelsListBox.ItemCount);

            window.ImageConvertToGrayscaleCheckBox.IsChecked = true;
            Assert.Equal(1, window.ImageChannelsListBox.ItemCount);
            Assert.Equal("168.8KB", window.ImageOriginalSizeLabel.Text); // The original image is still RGB
            Assert.Equal("56.3KB", window.ImageSourceSizeLabel.Text);

            // One step down on all channels: 8 -> 7 bits
            window.ImageBitDepthMinusButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            window.ImageUpdatePreviewButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Equal("49.2KB", window.ImageSourceSizeLabel.Text);
        }

        [AvaloniaFact]
        public async Task DataFile_IsCompressedAndExported()
        {
            MainWindow window = new MainWindow();
            window.Show();
            window.SettingsModeComboBox.SelectedIndex = 0;
            window.DataSourceTabControl.SelectedIndex = 1;

            await window.OpenDataFileAsync(TestData("MIT_Lorem_Ipsum.txt"));
            Assert.StartsWith("0x", window.FilePreviewTextBox.Text);

            string header = TempHeader();
            try
            {
                await window.CompressAsync(header);
                Assert.True(CountArrayBytes(File.ReadAllText(header)) > 0);
                Assert.NotEqual("0.00%", window.CompressionEfficiencyLabel.Text);
            }
            finally
            {
                File.Delete(header);
            }
        }

        [AvaloniaFact]
        public async Task LargeImage_IsDisplayedDownscaled()
        {
            // A 3000x2000 image is shown at 1024x682, while the channels keep the full size
            string file = Path.Combine(Path.GetTempPath(), "deltacomp_" + Guid.NewGuid().ToString("N") + ".png");
            using (var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(new Avalonia.PixelSize(3000, 2000), new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormats.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque))
            {
                bitmap.Save(file);
            }

            try
            {
                MainWindow window = new MainWindow();
                window.Show();
                await window.OpenImageAsync(file);

                Assert.Equal(new Avalonia.PixelSize(1024, 682), ((Avalonia.Media.Imaging.Bitmap)window.ImageSourceViewer.Source!).PixelSize);
                Assert.Equal(new Avalonia.PixelSize(1024, 682), ((Avalonia.Media.Imaging.Bitmap)window.ImageDestinationViewer.Source!).PixelSize);
                Assert.Equal("17.17MB", window.ImageOriginalSizeLabel.Text);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [AvaloniaFact]
        public async Task Image_IsResizedAndCompressed()
        {
            MainWindow window = new MainWindow();
            window.Show();
            window.SettingsModeComboBox.SelectedIndex = 0;
            await window.OpenImageAsync(TestData("RGB_24bits_palette_sample_image.png")); // 150x200, RGBA
            Assert.Equal("150", window.ImageWidthTextBox.Text);
            Assert.Equal("200", window.ImageHeightTextBox.Text);
            Assert.Equal("150x200, RGBA 8-8-8-8, 32 bpp", window.ImagePreviewFormatLabel.Text);

            // Digits only, not more than the original size, the other side follows the aspect ratio
            SetText(window.ImageWidthTextBox, "9x99");
            Assert.Equal("150", window.ImageWidthTextBox.Text);
            Assert.Equal("200", window.ImageHeightTextBox.Text);
            SetText(window.ImageHeightTextBox, "2");
            Assert.Equal("2", window.ImageWidthTextBox.Text);
            Assert.Equal("2", window.ImageHeightTextBox.Text); // Not recalculated back from the rounded width (it would be 3)
            SetText(window.ImageHeightTextBox, "100");
            Assert.Equal("75", window.ImageWidthTextBox.Text);

            // An unfinished input keeps the size and is restored when the field loses focus
            SetText(window.ImageWidthTextBox, "");
            window.ImageWidthTextBox.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Input.InputElement.LostFocusEvent));
            Assert.Equal("75", window.ImageWidthTextBox.Text);

            // 75x100 can only be split into 5x5 squares
            Assert.Equal(new[] { "No split", "5x5" }, window.ImageSplitIntoSquaresComboBox.Items.Select(i => i!.ToString()));
            window.ImageSplitIntoSquaresComboBox.SelectedIndex = 1;

            window.ImageBitDepthMinusButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            window.ImageUpdatePreviewButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Equal("75x100, RGBA 7-7-7-7, 28 bpp", window.ImagePreviewFormatLabel.Text);
            Assert.Equal(new Avalonia.PixelSize(75, 100), ((Avalonia.Media.Imaging.Bitmap)window.ImageDestinationViewer.Source!).PixelSize);

            string header = TempHeader();
            try
            {
                await window.CompressAsync(header);
                string text = File.ReadAllText(header);
                Assert.Matches(@"\b75\b", text);

                // The UI compresses the downscaled channels
                ImageDataSource image = ImageDataSource.Load(TestData("RGB_24bits_palette_sample_image.png"), null);
                List<Channel> channels = image.Planes.Select(p => (Channel)new ImageChannel(image, p.Type)).ToList();
                foreach (Channel c in channels) c.TargetBitDepth = 7;
                List<Channel> resampled = ImageResampler.Resample(channels, 150, 200, 75, 100).Cast<Channel>().ToList();
                long bytes = 0;
                Compressor compressor = new Compressor(resampled, 75, 100, true, _ => bytes++, (_, _) => { });
                compressor.BlockSizeBits = (byte)(int)window.BlockSizeBitsComboBox.SelectedItem!;
                compressor.DeltaCalculationOption = (DeltaOption)window.DeltaOptionComboBox.SelectedIndex;
                compressor.SplitToSquares = true;
                compressor.WidthOfSquare = 5;
                compressor.Compress(null);
                Assert.Equal(bytes, CountArrayBytes(text));
            }
            finally
            {
                File.Delete(header);
            }
        }

        [Fact]
        public void Resampler_AveragesAreasWithAlphaWeights()
        {
            // 3x2 RGBA image downscaled to 2x1: every result pixel covers 1.5 source pixels of each row
            ImagePlane red = new ImagePlane(ChannelType.Red, 8, 6);
            ImagePlane alpha = new ImagePlane(ChannelType.Alpha, 8, 6);
            uint[] redValues = [ 0, 90, 200, 30, 60, 100 ];
            uint[] alphaValues = [ 255, 255, 0, 255, 255, 255 ];
            for (int i = 0; i < 6; i++) { red[i] = redValues[i]; alpha[i] = alphaValues[i]; }
            ImageDataSource image = new ImageDataSource(3, 2, [ red, alpha ], "test");
            List<Channel> channels = [ new ImageChannel(image, ChannelType.Red), new ImageChannel(image, ChannelType.Alpha) ];

            List<ResampledChannel> result = ImageResampler.Resample(channels, 3, 2, 2, 1);

            // Left: weights 1, 0.5 in both rows (all opaque): (0 + 45 + 30 + 30) / 3 = 35
            Assert.Equal(35u, result[0].GetOriginalValueForFrame(0));
            // Right: weights 0.5, 1 in both rows, the transparent 200 gives no color: (45 + 30 + 100) / 2 = 87.5
            Assert.Equal(88u, result[0].GetOriginalValueForFrame(1));
            // The alpha is a plain average: (127.5 + 0 + 127.5 + 255) / 3 = 170
            Assert.Equal(255u, result[1].GetOriginalValueForFrame(0));
            Assert.Equal(170u, result[1].GetOriginalValueForFrame(1));

            // The bit depth is selected for the source channel
            channels[0].TargetBitDepth = 4;
            Assert.Equal(4, result[0].TargetBitDepth);
        }

        // Type into a text box (the TextChanged event is raised through the dispatcher)
        private static void SetText(Avalonia.Controls.TextBox textBox, string text)
        {
            textBox.Text = text;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        // Number of 0x.. elements in the exported array
        private static long CountArrayBytes(string header)
        {
            return System.Text.RegularExpressions.Regex.Matches(header, @"0x[0-9A-Fa-f]{2}\b").Count;
        }
    }
}
