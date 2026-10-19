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

        // Number of 0x.. elements in the exported array
        private static long CountArrayBytes(string header)
        {
            return System.Text.RegularExpressions.Regex.Matches(header, @"0x[0-9A-Fa-f]{2}\b").Count;
        }
    }
}
