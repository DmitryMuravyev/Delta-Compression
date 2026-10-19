using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using static DeltaComp.Compressor;


namespace DeltaComp
{
    /// <summary>
    /// Interaction logic for MainWindow.axaml
    /// There won't be too much description in this file, because it's just a UI.
    /// </summary>
    public partial class MainWindow : Window
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private constants
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // A lot of static constants
        private static class Constants
        {
            // Minimum and maximum image size limits
            public const int MinImageDimension = 8;
            public const int MaxImageDimension = 8192;

            // Minimum and maximum file size limits
            public const int MinDataFileSize = 64;
            public const int MaxDataFileSize = 1073741824;

            // Maximum number of frames shown in the data file preview
            public const int MaxPreviewFrames = 4096;

            // Messages
            public const string WaringWindowCaption = "Warning";
            public const string ErrorWindowCaption = "Error";
            public const string UnsupportedImageSizeError = "Unsupported image size: {0}x{1}.";
            public const string DefaultFileError = "Error reading file.";
            public const string UnsupportedFileSizeError = "Unsupported file size: {0}.";
            public const string LocalFilesOnlyError = "Only local files are supported.";

            // Other string constants
            public const string CompressButtonCaption = "Compress";
            public const string CancelButtonCaption = "Cancel";
            public const string NoSplitOption = "No split";
            public const string ExportFileExtension = "h";
            public const string ByteSymbol = "B";
            public const string KilobyteSymbol = "KB";
            public const string MegabyteSymbol = "MB";
            public const string DefaultSizeString = "0.00" + KilobyteSymbol;
            public const string DefaultEfficiencyString = "0.00%";
            public const string DefaultFileNameString = "-";
            public const string DefaultXmlFilename = "Header_Source.xml";
            public const string DefaultXsltFilename = "Header_Transformation.xslt";
            public const string DefaultTargetPlatform = "All";
            public const int AutoSettingsModeIndex = 1;

            // Block sizes offered in the UI and the default one
            public static readonly int[] BlockSizeBits = Enumerable.Range(2, 15).ToArray();
            public const int DefaultBlockSizeIndex = 6;

            // File dialog filters
            public static readonly FilePickerFileType ImagesFileType = new("Images")
            {
                Patterns = [ "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.ico", "*.webp" ]
            };
            public static readonly FilePickerFileType HeaderFileType = new("Header files") { Patterns = [ "*.h" ] };

            // Default progress bar color
            public static readonly Color DefaultProgressColor = Color.Parse("#FF06B025");

            // Progress bar colors for the compression stages
            public static readonly Dictionary<CompressionStage, Color> CompressionStageColors = new()
            {
                { CompressionStage.None, Colors.Transparent },
                { CompressionStage.Preparation, Colors.DodgerBlue },
                { CompressionStage.Analysis, Colors.DodgerBlue },
                { CompressionStage.Compression, DefaultProgressColor },
                { CompressionStage.Done, DefaultProgressColor },
                { CompressionStage.Cancelled, Colors.Gray },
                { CompressionStage.Failed, Colors.Crimson }
            };
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private types
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // An option of splitting the image into squares.
        private sealed record SquaringOption(byte Size, string Name)
        {
            public override string ToString() => Name;
        }

        // Description of the compression context related to one of the tabs (Image/File)
        private sealed class CompressionContext
        {
            public DataSource? Source;
            public string Filename = String.Empty;
            public long SourceSize;
            public volatile int CompressedSize;
            public bool PreviewIsUpToDate;
            public int SelectedBlockSize = Constants.DefaultBlockSizeIndex;
            public int SelectedDeltaOption;
            public int SelectedSettingsMode;
            public int SelectedPlatform;
            public volatile CompressionStage Stage = CompressionStage.None;
            public volatile float Progress;
            public List<Channel> Channels = [];
            public Action? UpdatePreview;

            public void Clean()
            {
                Source = null;
                Filename = String.Empty;
                SourceSize = 0;
                CompressedSize = 0;
                Stage = CompressionStage.None;
                Progress = 0.0f;
                PreviewIsUpToDate = false;
                UpdatePreview = null;
            }
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // A list of available square sizes into which the image can be split
        private readonly List<SquaringOption> _squaringOptions = [ new SquaringOption(0, Constants.NoSplitOption) ];

        // Array of contexts and current context
        private readonly CompressionContext[] _contexts;
        private int _currentContextIndex = 0;
        private CompressionContext CurrentContext => _contexts[_currentContextIndex];

        // When the image is loading, we must prohibit the processing of interface actions.
        private bool _dataLoading = false;

        // Source image and its preview
        private ImageDataSource? _image = null;
        private WriteableBitmap? _previewBitmap = null;
        private byte[]? _previewPixels = null;

        // Compression Task cancellation token
        private CancellationTokenSource _compressionCancellationTokenSource = new CancellationTokenSource();

        // Application settings
        private readonly AppSettings _settings = AppSettings.Load();

        // Compressor class instance
        private Compressor? _compressor = null;

        // Export class instance
        private CodeExport? _export = null;

        // The last folder used in the file dialogs
        private IStorageFolder? _lastFolder = null;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Basic preparations for work
        public MainWindow()
        {
            InitializeComponent();

            ImageSplitIntoSquaresComboBox.ItemsSource = _squaringOptions.ToList();
            ImageSplitIntoSquaresComboBox.SelectedIndex = 0;

            BlockSizeBitsComboBox.ItemsSource = Constants.BlockSizeBits;
            BlockSizeBitsComboBox.SelectedIndex = Constants.DefaultBlockSizeIndex;

            List<string> platforms = (_settings.Platforms.Count > 0) ? _settings.Platforms : [ Constants.DefaultTargetPlatform ];
            TargetPlatformComboBox.ItemsSource = platforms;
            TargetPlatformComboBox.SelectedIndex = 0;

            // Init CompressionContext for every tab
            _contexts = new CompressionContext[DataSourceTabControl.ItemCount];
            for (int i = 0; i < _contexts.Length; i++)
            {
                _contexts[i] = new CompressionContext()
                {
                    SelectedBlockSize = BlockSizeBitsComboBox.SelectedIndex,
                    SelectedDeltaOption = DeltaOptionComboBox.SelectedIndex,
                    SelectedSettingsMode = SettingsModeComboBox.SelectedIndex,
                    SelectedPlatform = TargetPlatformComboBox.SelectedIndex
                };
            }

            _currentContextIndex = Math.Max(0, DataSourceTabControl.SelectedIndex);
            UpdateProgressBarColor();
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Image tab
        // Action handlers
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Open new image
        private async void ImageOpenButton_Click(object? sender, RoutedEventArgs e)
        {
            string? selectedFile = await PickFileToOpenAsync([ Constants.ImagesFileType, FilePickerFileTypes.All ]);
            if (selectedFile != null) await OpenImageAsync(selectedFile);
        }


        // Load the image and prepare its channels
        internal async Task OpenImageAsync(string selectedFile)
        {
            CompressionContext currentContext = CurrentContext;
            CleanContext(currentContext);

            ImageSourceViewer.Source = null;
            ImageFormatLabel.Text = "";
            _image = null;
            _dataLoading = true;

            ImageUpdatePreviewButton.IsEnabled = false;
            ImageBitDepthMinusButton.IsEnabled = false;
            ImageBitDepthPlusButton.IsEnabled = false;
            ImageChannelDownButton.IsEnabled = false;
            ImageChannelUpButton.IsEnabled = false;
            ImageSplitIntoSquaresComboBox.IsEnabled = false;
            CompressButton.IsEnabled = false;

            Bitmap? displayBitmap = null;
            try
            {
                // PNG is decoded natively (exact bit depth), other formats with the platform decoder
                ImageDataSource image = ImageDataSource.Load(selectedFile, DecodeWithPlatform);

                if ((image.Width < Constants.MinImageDimension) || (image.Width > Constants.MaxImageDimension) ||
                    (image.Height < Constants.MinImageDimension) || (image.Height > Constants.MaxImageDimension))
                {
                    await DisplayMessage(MessageKind.Error, String.Format(Constants.UnsupportedImageSizeError,
                        image.Width, image.Height), Constants.ErrorWindowCaption);
                }
                else
                {
                    _image = image;
                    currentContext.Filename = Path.GetFileName(selectedFile);
                    displayBitmap = new Bitmap(selectedFile);
                }
            }
            catch (Exception ex)
            {
                await DisplayMessage(MessageKind.Error, ex.Message, Constants.ErrorWindowCaption);
            }

            // Display source image
            ImageSourceViewer.Source = displayBitmap;

            bool imageAvailable = (_image != null);
            if (imageAvailable)
            {
                ImageFormatLabel.Text = String.Format("{0}x{1}, {2}", _image!.Width, _image.Height, _image.FormatDescription);

                // Check the available sizes of squares for splitting
                _squaringOptions.Clear();
                _squaringOptions.Add(new SquaringOption(0, Constants.NoSplitOption));
                for (byte i = 2; i <= 16; i++)
                {
                    if (((_image.Width % i) == 0) && ((_image.Height % i) == 0))
                    {
                        _squaringOptions.Add(new SquaringOption(i, String.Format("{0}x{0}", i)));
                    }
                }
                ImageSplitIntoSquaresComboBox.ItemsSource = _squaringOptions.ToList();
                ImageSplitIntoSquaresComboBox.SelectedIndex = 0;
                ImageSplitIntoSquaresComboBox.IsEnabled = true;

                currentContext.Source = _image;

                // Image loaded so we can compress it
                CompressButton.IsEnabled = true;
            }

            // Update checkboxes
            ImageConvertToGrayscaleCheckBox.IsEnabled = imageAvailable && _image!.IsColor;
            ImageConvertToGrayscaleCheckBox.IsChecked = imageAvailable && !_image!.IsColor;
            ImageUseAlphaCheckBox.IsEnabled = imageAvailable && _image!.HasAlpha;
            ImageUseAlphaCheckBox.IsChecked = imageAvailable && _image!.HasAlpha;

            // Update pixel component channels list
            ImageUpdateChannels();

            _dataLoading = false;
        }


        // Update image preview
        private void ImageUpdatePreviewButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_previewBitmap == null) ImageInitializePreview();
            ImageUpdatePreview();
        }


        // One of Channels checkboxes (Convert to Grayscale or Use Alpha) changes its "isChecked" state.
        private void ImageChannelParametersChanged(object? sender, RoutedEventArgs e)
        {
            if (!_dataLoading && (_contexts != null))
            {
                ImageUpdateChannels();
            }
        }


        // Depending on the selected line (channel), we can find the possible directions of its movement.
        private void ImageChannelsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.Source != ImageChannelsListBox) return;
            ImageUpdateMoveChannelsButtons();
        }


        // One of the channel row movement buttons was pressed, so we need to find the direction of movement.
        private void ImageChannelMoveButton_Click(object? sender, RoutedEventArgs e)
        {
            int selectedRow = ImageChannelsListBox.SelectedIndex;
            if (selectedRow < 0) return;

            Channel channel = CurrentContext.Channels[selectedRow];
            selectedRow += (sender == ImageChannelDownButton) ? 1 : -1;

            // And finally move selected row...
            ImageMoveChannelToPosition(channel, selectedRow);
        }


        // One of the changing bit depth buttons was pressed
        private void ImageAllChannelsChangeBitDepthButton_Click(object? sender, RoutedEventArgs e)
        {
            // Change the bit depth of all channels
            int step = (sender == ImageBitDepthMinusButton) ? -1 : 1;
            foreach (Channel c in CurrentContext.Channels)
            {
                int newBitDepth = c.TargetBitDepth + step;
                if ((newBitDepth >= 1) && (newBitDepth <= c.BitsPerChannel)) c.TargetBitDepth = newBitDepth;
            }
            ImageRefreshChannelsList();

            // And update the "isEnabled" attribute of the bit depth change buttons.
            ImageUpdateBitDepthButtons();
            CurrentContext.PreviewIsUpToDate = false;
        }


        // The bit depth has been changed manually, so we need to update the bit depth buttons.
        private void ImageChannelBitDepthComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_contexts == null) return;
            ImageUpdateBitDepthButtons();
            CurrentContext.PreviewIsUpToDate = false;
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Image tab
        // Other procedures that serve the UI
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Decode a non-PNG image with the platform (Skia) decoder into 8-bit BGRA pixels
        private static Rgba32Image DecodeWithPlatform(string filePath)
        {
            using Bitmap bitmap = new Bitmap(filePath);
            PixelSize size = bitmap.PixelSize;
            using WriteableBitmap converted = new WriteableBitmap(size, new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Unpremul);
            byte[] pixels = new byte[size.Width * size.Height * 4];
            using (ILockedFramebuffer framebuffer = converted.Lock())
            {
                bitmap.CopyPixels(framebuffer);
                for (int y = 0; y < size.Height; y++)
                {
                    Marshal.Copy(framebuffer.Address + y * framebuffer.RowBytes, pixels, y * size.Width * 4, size.Width * 4);
                }
            }
            return new Rgba32Image(size.Width, size.Height, pixels, isBgra: true);
        }


        // Update pixel component channels list
        private void ImageUpdateChannels()
        {
            // The number and types of channels will be changed, so the preview image must be reinitialized.
            ImageDestinationViewer.Source = null;
            ImageChannelsListBox.ItemsSource = null;
            ImageOriginalSizeLabel.Text = Constants.DefaultSizeString;
            ImageSourceSizeLabel.Text = Constants.DefaultSizeString;
            _previewBitmap = null;
            _previewPixels = null;

            CompressionContext currentContext = CurrentContext;
            List<Channel> channels = currentContext.Channels;
            channels.Clear();

            // Check if source data available
            if (_image == null) return;

            int bitsPerPixel = 0;

            if (!_image.IsColor)
            {
                // Source image is Grayscale
                ImageChannel grayscaleChannel = new ImageChannel(_image, ChannelType.Grayscale);
                channels.Add(grayscaleChannel);
                bitsPerPixel += grayscaleChannel.BitsPerChannel;
            }
            else
            {
                // Source image is color...
                List<Channel> colorComponents = [];

                for (int n = (int)ChannelType.Red; n <= (int)ChannelType.Blue; n++)
                {
                    ImageChannel component = new ImageChannel(_image, (ChannelType)n);
                    colorComponents.Add(component);
                    bitsPerPixel += component.BitsPerChannel;
                }

                if (ImageConvertToGrayscaleCheckBox.IsChecked == true)
                {
                    // ...and should be converted to Grayscale (8 or 16 bits depending on the source)...
                    int grayscaleBitDepth = (_image.MaxColorBitDepth > 8) ? 16 : 8;
                    channels.Add(new GrayscaleMixerChannel(colorComponents, grayscaleBitDepth));
                }
                else
                {
                    // ...or should be processed as it is.
                    channels.AddRange(colorComponents);
                }
            }

            if (_image.HasAlpha && (ImageUseAlphaCheckBox.IsChecked == true))
            {
                // Source image contains an alpha channel
                ImageChannel alphaChannel = new ImageChannel(_image, ChannelType.Alpha);
                channels.Add(alphaChannel);
                bitsPerPixel += alphaChannel.BitsPerChannel;
            }

            // Display channels list
            ImageRefreshChannelsList();

            // Update source size label
            ImageOriginalSizeLabel.Text = GetSizeString(_image.Height * (((long)_image.Width * bitsPerPixel + 7) >> 3));

            // Initialize preview image
            ImageInitializePreview();

            // Update bit depth buttons
            ImageUpdateBitDepthButtons();
        }


        // Rebind the channels list (the channel properties have no change notifications)
        private void ImageRefreshChannelsList()
        {
            object? selected = ImageChannelsListBox.SelectedItem;
            ImageChannelsListBox.ItemsSource = null;
            ImageChannelsListBox.ItemsSource = CurrentContext.Channels.ToList();
            if (selected != null) ImageChannelsListBox.SelectedItem = selected;
            ImageUpdateMoveChannelsButtons();
        }


        // Initialize preview environment
        private void ImageInitializePreview()
        {
            // Check if source data available
            if (_image == null) return;

            CompressionContext currentContext = CurrentContext;
            currentContext.UpdatePreview = ImageUpdatePreview;
            currentContext.PreviewIsUpToDate = false;

            // The preview is always 8-bit BGRA: Grayscale is shown in all color components,
            // and a missing alpha channel is shown as opaque.
            _previewBitmap = new WriteableBitmap(new PixelSize(_image.Width, _image.Height), new Vector(96, 96),
                PixelFormats.Bgra8888, AlphaFormat.Unpremul);
            _previewPixels = new byte[_image.Width * _image.Height * 4];
            ImageUpdatePreviewButton.IsEnabled = true;
            ImageUpdatePreview();
        }


        // Update preview accordig to selected channel parameters
        private void ImageUpdatePreview()
        {
            CompressionContext currentContext = CurrentContext;

            if ((_image == null) || (_previewBitmap == null) || (_previewPixels == null)) return;

            int stride = _image.Width * 4;
            PreviewRenderer.RenderBgra32(currentContext.Channels, _image.Width, _image.Height, _previewPixels, stride);
            using (ILockedFramebuffer framebuffer = _previewBitmap.Lock())
            {
                for (int y = 0; y < _image.Height; y++)
                {
                    Marshal.Copy(_previewPixels, y * stride, framebuffer.Address + y * framebuffer.RowBytes, stride);
                }
            }

            // Force the Image control to redraw the same bitmap
            ImageDestinationViewer.Source = null;
            ImageDestinationViewer.Source = _previewBitmap;

            // Calculate the data size based on the number of channels and color depth
            int sourceBitsPerPixel = 0;
            foreach (Channel c in currentContext.Channels) sourceBitsPerPixel += c.TargetBitDepth;
            long size = ((long)sourceBitsPerPixel * _image.Width * _image.Height + 7) >> 3;
            currentContext.SourceSize = size;
            ImageSourceSizeLabel.Text = GetSizeString(size);

            currentContext.PreviewIsUpToDate = true;
        }


        // Move channel up or down and update the channels list keeping the selected row.
        private void ImageMoveChannelToPosition(Channel channel, int position)
        {
            List<Channel> channels = CurrentContext.Channels;
            if ((position < 0) || (position >= channels.Count)) return;

            channels.Remove(channel);
            channels.Insert(position, channel);
            ImageChannelsListBox.ItemsSource = channels.ToList();
            ImageChannelsListBox.SelectedItem = channel;
            ImageChannelsListBox.ScrollIntoView(channel);
            ImageUpdateMoveChannelsButtons();
            CurrentContext.PreviewIsUpToDate = false;
        }


        // If all channels are set to the minimum or maximum bit depth, then further decreasing or increasing it
        // is impossible, which means we can disable the corresponding button.
        private void ImageUpdateBitDepthButtons()
        {
            // Check if source data available and we have at least one channel with a bit depth of more than 1.
            CompressionContext currentContext = CurrentContext;
            if ((currentContext.Source != null) && (currentContext.Source.BitsPerFrame > currentContext.Source.ChannelsCount) &&
                (currentContext.Channels.Count > 0))
            {
                bool allChannelsAtMinBitDepth = true;
                bool allChannelsAtMaxBitDepth = true;
                foreach (Channel c in currentContext.Channels)
                {
                    if (c.TargetBitDepth > 1) allChannelsAtMinBitDepth = false;
                    if (c.TargetBitDepth < c.BitsPerChannel) allChannelsAtMaxBitDepth = false;
                }
                ImageBitDepthMinusButton.IsEnabled = !allChannelsAtMinBitDepth;
                ImageBitDepthPlusButton.IsEnabled = !allChannelsAtMaxBitDepth;
            }
            else
            {
                ImageBitDepthMinusButton.IsEnabled = false;
                ImageBitDepthPlusButton.IsEnabled = false;
            }
        }


        // Depending on the selected row, find which way we can move it.
        private void ImageUpdateMoveChannelsButtons()
        {
            int selectedRow = ImageChannelsListBox.SelectedIndex;
            int count = CurrentContext.Channels.Count;
            if ((selectedRow != -1) && (count > 1))
            {
                // We can move the row up if it is not the top one.
                ImageChannelUpButton.IsEnabled = selectedRow != 0;
                // And we can move the row down if it is not the bottom one.
                ImageChannelDownButton.IsEnabled = selectedRow != (count - 1);
            }
            else
            {
                ImageChannelUpButton.IsEnabled = false;
                ImageChannelDownButton.IsEnabled = false;
            }
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Custom data (File) tab
        // Action handlers
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Open file
        private async void FileOpenButton_Click(object? sender, RoutedEventArgs e)
        {
            string? selectedFile = await PickFileToOpenAsync([ FilePickerFileTypes.All ]);
            if (selectedFile != null) await OpenDataFileAsync(selectedFile);
        }


        // Open the data file and prepare its channels
        internal async Task OpenDataFileAsync(string selectedFile)
        {
            CompressionContext currentContext = CurrentContext;
            CleanContext(currentContext);

            FileChannelsCountComboBox.IsEnabled = false;
            FileBitsPerChannelComboBox.IsEnabled = false;
            FileBigEndianRadioButton.IsEnabled = false;
            FileUpdatePreviewButton.IsEnabled = false;
            FileOriginalSizeLabel.Text = Constants.DefaultSizeString;
            FileNameLabel.Text = Constants.DefaultFileNameString;
            FilePreviewTextBox.Text = "";
            CompressButton.IsEnabled = false;

            FileDataSource dataSource = new FileDataSource(selectedFile, _settings.MaxBufferSize);

            // Check if file opened
            if (dataSource.ErrorOccurred)
            {
                string message = dataSource.LastErrorMessage ?? Constants.DefaultFileError;
                await DisplayMessage(MessageKind.Error, message, Constants.ErrorWindowCaption);
                return;
            }
            // Check file size
            if ((dataSource.FileSize < Constants.MinDataFileSize) || (dataSource.FileSize > Constants.MaxDataFileSize))
            {
                await DisplayMessage(MessageKind.Error, String.Format(Constants.UnsupportedFileSizeError,
                    GetSizeString(dataSource.FileSize)), Constants.ErrorWindowCaption);
                return;
            }

            currentContext.Source = dataSource;
            currentContext.Filename = Path.GetFileName(selectedFile);
            currentContext.SourceSize = dataSource.FileSize;

            FileNameLabel.Text = currentContext.Filename;
            FileOriginalSizeLabel.Text = GetSizeString(dataSource.FileSize);
            currentContext.Channels.Clear();
            currentContext.UpdatePreview = FileUpdatePreview;

            FileUpdateChannelsOptions();
            FileUpdatePreviewButton.IsEnabled = true;
            CompressButton.IsEnabled = true;
        }


        // Channels count changed
        private void FileChannelsCountComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            FileChannelsCountChanged();
        }


        // Channels bit depth changed
        private void FileBitsPerChannelComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            FileUpdateEndiannessOption();
        }


        // File reading endianness changed
        private void FileEndiannessRadioButton_StateChanged(object? sender, RoutedEventArgs e)
        {
            FileUpdateChannels();
        }


        // Update preview
        private void FileUpdatePreviewButton_Click(object? sender, RoutedEventArgs e)
        {
            FileUpdatePreview();
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Custom data (File) tab
        // Other procedures that serve the UI
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Selected numeric value of a ComboBox made of ComboBoxItems
        private static int? GetSelectedNumber(ComboBox comboBox)
        {
            string? value = (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            return int.TryParse(value, out int number) ? number : null;
        }


        // Update a list of available file splitting (into alternating channels) options
        private void FileUpdateChannelsOptions()
        {
            int enabledOptionsCount = 0;
            foreach (ComboBoxItem item in FileChannelsCountComboBox.Items.OfType<ComboBoxItem>())
            {
                // The file size should be divided by the number of channels without remainder
                bool optionEnabled = int.TryParse(item.Content?.ToString(), out int count) && (CurrentContext.SourceSize % count == 0);
                item.IsEnabled = optionEnabled;
                if (optionEnabled)
                {
                    enabledOptionsCount++;
                }
                else if (item == FileChannelsCountComboBox.SelectedItem)
                {
                    FileChannelsCountComboBox.SelectedIndex = 0;
                }
            }
            FileChannelsCountComboBox.IsEnabled = (enabledOptionsCount > 1);
            FileChannelsCountChanged();
        }


        // Update available bit depth options for selected channels count
        private void FileChannelsCountChanged()
        {
            if ((FileBitsPerChannelComboBox == null) || (_contexts == null)) return;

            int? channelsCount = GetSelectedNumber(FileChannelsCountComboBox);
            if (channelsCount != null)
            {
                int enabledOptionsCount = 0;

                foreach (ComboBoxItem item in FileBitsPerChannelComboBox.Items.OfType<ComboBoxItem>())
                {
                    // The file size should be divided by the number of channels of selected bit depth without remainder
                    bool optionEnabled = int.TryParse(item.Content?.ToString(), out int bits) &&
                        (CurrentContext.SourceSize % ((bits >> 3) * channelsCount.Value) == 0);
                    item.IsEnabled = optionEnabled;
                    if (optionEnabled) enabledOptionsCount++;
                }

                FileBitsPerChannelComboBox.IsEnabled = (enabledOptionsCount > 1);
            }
            else
            {
                FileBitsPerChannelComboBox.SelectedIndex = 0;
                FileBitsPerChannelComboBox.IsEnabled = false;
            }

            // Check if selected item disabled
            if (FileBitsPerChannelComboBox.SelectedItem is not ComboBoxItem { IsEnabled: true }) FileBitsPerChannelComboBox.SelectedIndex = 0;
            FileUpdateEndiannessOption();
        }


        // Update read endianness option for data file
        private void FileUpdateEndiannessOption()
        {
            if ((FileBigEndianRadioButton == null) || (_contexts == null)) return;
            FileBigEndianRadioButton.IsEnabled = (FileBitsPerChannelComboBox.SelectedIndex > 0);
            FileUpdateChannels();
        }


        // Update the number and parameters of data channels
        private void FileUpdateChannels()
        {
            if (_contexts == null) return;

            CompressionContext currentContext = CurrentContext;
            if (currentContext.Source is not FileDataSource dataSource) return;

            List<Channel> channels = currentContext.Channels;

            int? selectedChannelsCount = GetSelectedNumber(FileChannelsCountComboBox);
            int? selectedBPC = GetSelectedNumber(FileBitsPerChannelComboBox);

            if ((selectedChannelsCount != null) && (selectedBPC != null))
            {
                bool isBigEndian = (FileLittleEndianRadioButton.IsChecked != true);

                // Update channels
                if (channels.Count != selectedChannelsCount)
                {
                    channels.Clear();
                    for (int i = 0; i < selectedChannelsCount; i++)
                    {
                        channels.Add(new FileDataChannel(dataSource, i));
                    }
                }

                int bytesPerChannel = selectedBPC.Value >> 3;
                int bytesPerFrame = bytesPerChannel * selectedChannelsCount.Value;

                dataSource.UpdateSourceParameters(bytesPerFrame, bytesPerChannel, isBigEndian);
                // Update bit depth
                foreach (FileDataChannel channel in channels.OfType<FileDataChannel>()) channel.UpdateBitDepth();
                FileUpdatePreview();
            }
            else
            {
                currentContext.PreviewIsUpToDate = false;
            }
        }


        // Update hexadecimal preview of the data file
        private void FileUpdatePreview()
        {
            CompressionContext currentContext = CurrentContext;
            if (currentContext.Source is not FileDataSource dataSource) return;

            FilePreviewTextBox.Text = PreviewRenderer.BuildDataText(currentContext.Channels, dataSource.FramesCount, Constants.MaxPreviewFrames);
            currentContext.PreviewIsUpToDate = true;
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Compression part
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Compress/cancel button click
        private async void CompressButton_Click(object? sender, RoutedEventArgs e)
        {
            CompressionContext currentContext = CurrentContext;

            // Check if compression in progress
            if ((currentContext.Stage == CompressionStage.Preparation) || (currentContext.Stage == CompressionStage.Analysis)
                || (currentContext.Stage == CompressionStage.Compression))
            {
                // If so, then the "Cancel" button was clicked, so request cancellation.
                CompressButton.IsEnabled = false;
                _compressionCancellationTokenSource.Cancel();
                return;
            }

            // Else if compession wasn't started...
            if (currentContext.Source == null) return;

            // Show save file dialog box
            string? exportFile = await PickFileToSaveAsync(GetBestAlphanumericSubstring(currentContext.Filename));
            if (exportFile != null) await CompressAsync(exportFile);
        }


        // Compress the current data and export it to the header file
        internal async Task CompressAsync(string exportFile)
        {
            CompressionContext currentContext = CurrentContext;
            if (currentContext.Source == null) return;

            List<Channel> channels = currentContext.Channels;

            DisableControls();
            currentContext.CompressedSize = 0;
            CompressionProgress.Value = 0.0d;

            // Update preview if it doesn't match the channel parameters
            if (!currentContext.PreviewIsUpToDate && (currentContext.UpdatePreview != null)) currentContext.UpdatePreview();

            if (currentContext.Source is ImageDataSource imageSource)
            {
                // Current context is an Image context
                _compressor = new Compressor(channels, (UInt16)imageSource.Width, (UInt16)imageSource.Height,
                    imageSource.MultithreadedReadingAllowed, WriteCompressedByteDelegate, DispatchUpdateProgressDelegate);
                SquaringOption? selectedOption = ImageSplitIntoSquaresComboBox.SelectedItem as SquaringOption;
                if ((selectedOption != null) && (selectedOption.Size > 1))
                {
                    _compressor.SplitToSquares = true;
                    _compressor.WidthOfSquare = selectedOption.Size;
                }
                else
                {
                    _compressor.SplitToSquares = false;
                    _compressor.WidthOfSquare = 1;
                }
            }
            else if (currentContext.Source is FileDataSource fileSource)
            {
                // Current context is a data file context
                _compressor = new Compressor(channels, (UInt32)fileSource.FramesCount, fileSource.MultithreadedReadingAllowed,
                    WriteCompressedByteDelegate, DispatchUpdateProgressDelegate);
            }
            else
            {
                // Unimplemented compression context...
                EnableControls();
                return;
            }

            _compressor.BlockSizeBits = (byte)(int)BlockSizeBitsComboBox.SelectedItem!;
            _compressor.DeltaCalculationOption = (DeltaOption)DeltaOptionComboBox.SelectedIndex;

            if (!_compressionCancellationTokenSource.TryReset())
            {
                _compressionCancellationTokenSource.Dispose();
                _compressionCancellationTokenSource = new CancellationTokenSource();
            }
            CancellationToken token = _compressionCancellationTokenSource.Token;

            // Automatic selection of the settings (the smallest output wins)
            if (SettingsModeComboBox.SelectedIndex == Constants.AutoSettingsModeIndex)
            {
                List<byte> blockSizeBitsOptions = Constants.BlockSizeBits.Select(b => (byte)b).ToList();
                List<byte> squareWidths = _squaringOptions.Select(o => (o.Size > 1) ? o.Size : (byte)1).ToList();
                bool isImage = currentContext.Source is ImageDataSource;
                Compressor compressor = _compressor;
                try
                {
                    AutoSettings settings = await Task.Run(() => compressor.AutoSelectSettings(blockSizeBitsOptions,
                        Enum.GetValues<DeltaOption>(), isImage ? squareWidths : [ 1 ], token), token);

                    // Show the chosen settings
                    BlockSizeBitsComboBox.SelectedItem = (int)settings.BlockSizeBits;
                    DeltaOptionComboBox.SelectedIndex = (int)settings.DeltaCalculationOption;
                    if (isImage)
                    {
                        int squareIndex = _squaringOptions.FindIndex(o => (o.Size > 1 ? o.Size : 1) == settings.WidthOfSquare);
                        if (squareIndex >= 0) ImageSplitIntoSquaresComboBox.SelectedIndex = squareIndex;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException)
                    {
                        currentContext.Stage = CompressionStage.Cancelled;
                        UpdateProgressBarColor();
                    }
                    else
                    {
                        UpdateProgressDelegate(CompressionStage.Failed, currentContext.Progress);
                        await DisplayMessage(MessageKind.Error, ex.Message, Constants.ErrorWindowCaption);
                    }
                    _compressor = null;
                    EnableControls();
                    return;
                }
            }

            // Create and configure export class instance
            _export = new CodeExport(Path.Combine(AppContext.BaseDirectory, Constants.DefaultXmlFilename),
                Path.Combine(AppContext.BaseDirectory, Constants.DefaultXsltFilename));
            Dictionary<string, string> dict = new Dictionary<string, string>
            {
                { "nof", _compressor.TotalFrames.ToString() },
                { "noc", _compressor.ChannelsCount.ToString() },
                { "bpc", "{ " + string.Join(", ", _compressor.BitsPerChannel) + " }" },
                { "bsb", _compressor.BlockSizeBits.ToString() },
                { "bpmd", "{ " + string.Join(", ", _compressor.BitsPerMethodDeclaration) + " }" },
                { "bpf", _compressor.BitsPerFrame.ToString() },
                { "do", ((int)_compressor.DeltaCalculationOption).ToString() },
                { "dos", _compressor.DeltaCalculationOption.ToString() },
                { "iw", _compressor.ImageWidth.ToString() },
                { "ih", _compressor.ImageHeight.ToString() },
                { "sts", _compressor.SplitToSquares ? "1" : "0" },
                { "ss", _compressor.WidthOfSquare.ToString() }
            };

            string targetPlatform = TargetPlatformComboBox.SelectedItem as string ?? Constants.DefaultTargetPlatform;
            bool preparationResult = _export.PrepareExport(true, dict, targetPlatform.ToLowerInvariant(), currentContext.Filename,
                Path.GetFileNameWithoutExtension(exportFile), exportFile, out string errorMessage);

            if (!preparationResult)
            {
                _export = null;
                _compressor = null;
                EnableControls();
                await DisplayMessage(MessageKind.Error, errorMessage, Constants.ErrorWindowCaption);
                return;
            }

            // Run compression task
            Compressor runningCompressor = _compressor;
            try
            {
                await Task.Run(() => { runningCompressor.Compress(token); }, token);
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    currentContext.Stage = CompressionStage.Cancelled;
                    UpdateProgressBarColor();
                }
                else
                {
                    UpdateProgressDelegate(CompressionStage.Failed, currentContext.Progress);
                    await DisplayMessage(MessageKind.Error, ex.Message, Constants.ErrorWindowCaption);
                }

                EnableControls();
            }

            if (!_export.FinalizeExport(out errorMessage))
            {
                await DisplayMessage(MessageKind.Error, errorMessage, Constants.ErrorWindowCaption);
            }

            _export = null;
            _compressor = null;
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Compression delegates
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Write next compressed byte and check data writing status
        public void WriteCompressedByteDelegate(byte compressedByte)
        {
            Debug.Assert((_export != null) && (_compressor != null));

            _export.AppendArrayElement(compressedByte);
            CurrentContext.CompressedSize++;

            if (_export.LastErrorMessage != null)
            {
                _compressor.RequestFallingIntoError();
                string message = _export.LastErrorMessage;
                Dispatcher.UIThread.Post(async () => await DisplayMessage(MessageKind.Error, message, Constants.ErrorWindowCaption));
            }
        }


        // The compressor reports progress from worker threads, so we have to marshal the call to the UI thread
        private void DispatchUpdateProgressDelegate(CompressionStage stage, double progress)
        {
            Dispatcher.UIThread.Invoke(() => UpdateProgressDelegate(stage, progress));
        }


        // Update compression progress and check data reading status
        public void UpdateProgressDelegate(CompressionStage stage, double progress)
        {
            CompressionContext currentContext = CurrentContext;
            currentContext.Progress = (float)progress;
            CompressionProgress.Value = progress;

            DataSource? dataSource = currentContext.Source;

            if (stage != currentContext.Stage)
            {
                currentContext.Stage = stage;
                UpdateProgressBarColor();

                if ((stage == CompressionStage.Preparation) || (stage == CompressionStage.Analysis)
                     || (stage == CompressionStage.Compression))
                {
                    CompressButton.Content = Constants.CancelButtonCaption;
                    CompressButton.IsEnabled = true;
                }
                else if (stage == CompressionStage.Done)
                {
                    UpdateResultsUIElements();
                    EnableControls();
                }
                else if ((stage == CompressionStage.Cancelled) || (stage == CompressionStage.Failed))
                {
                    EnableControls();
                }
            }

            // Check if reading error occurred
            if ((dataSource != null) && dataSource.ErrorOccurred)
            {
                _compressor?.RequestFallingIntoError();
                string message = dataSource.LastErrorMessage ?? Constants.DefaultFileError;
                dataSource.ErrorOccurred = false;
                Dispatcher.UIThread.Post(async () => await DisplayMessage(MessageKind.Error, message, Constants.ErrorWindowCaption));
            }
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // UI helper methods
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Compression tab changed
        private void DataSourceTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // Selection events of the inner controls bubble up here, so ignore them
            if ((e.Source != DataSourceTabControl) || (_contexts == null) ||
                (_currentContextIndex >= _contexts.Length)) return;

            // Save compression parameters for previous tab
            CompressionContext lastContext = CurrentContext;
            lastContext.SelectedBlockSize = BlockSizeBitsComboBox.SelectedIndex;
            lastContext.SelectedDeltaOption = DeltaOptionComboBox.SelectedIndex;
            lastContext.SelectedSettingsMode = SettingsModeComboBox.SelectedIndex;
            lastContext.SelectedPlatform = TargetPlatformComboBox.SelectedIndex;

            int newContextIndex = DataSourceTabControl.SelectedIndex;
            if (newContextIndex < 0) return;
            _currentContextIndex = newContextIndex;
            CompressionContext currentContext = CurrentContext;

            // Restore compression context for selected tab
            BlockSizeBitsComboBox.SelectedIndex = currentContext.SelectedBlockSize;
            DeltaOptionComboBox.SelectedIndex = currentContext.SelectedDeltaOption;
            SettingsModeComboBox.SelectedIndex = currentContext.SelectedSettingsMode;
            TargetPlatformComboBox.SelectedIndex = currentContext.SelectedPlatform;

            // Update other UI elements
            if (!currentContext.PreviewIsUpToDate && (currentContext.UpdatePreview != null)) currentContext.UpdatePreview();
            UpdateResultsUIElements();
            UpdateProgressBarColor();
            CompressionProgress.Value = currentContext.Progress;
            CompressButton.IsEnabled = (currentContext.Source != null);
        }


        // Show saved compression results for the current context
        private void UpdateResultsUIElements()
        {
            CompressionContext currentContext = CurrentContext;

            if (currentContext.Stage == CompressionStage.Done)
            {
                long compressedSize = currentContext.CompressedSize;
                CompressedSizeLabel.Text = GetSizeString(compressedSize);
                double efficiency = (1.0d - (double)compressedSize / (double)currentContext.SourceSize) * 100.0d;
                CompressionEfficiencyLabel.Text = String.Format("{0:0.00}%", efficiency);
            }
            else
            {
                CompressedSizeLabel.Text = Constants.DefaultSizeString;
                CompressionEfficiencyLabel.Text = Constants.DefaultEfficiencyString;
            }
        }


        // Select progress bar color according to a compression status
        private void UpdateProgressBarColor()
        {
            if (!Constants.CompressionStageColors.TryGetValue(CurrentContext.Stage, out Color progressBarColor))
            {
                progressBarColor = Constants.DefaultProgressColor;
            }
            CompressionProgress.Foreground = new SolidColorBrush(progressBarColor);
        }


        // Disable main controls
        private void DisableControls()
        {
            DataSourceTabControl.IsEnabled = false;
            BlockSizeBitsComboBox.IsEnabled = false;
            DeltaOptionComboBox.IsEnabled = false;
            SettingsModeComboBox.IsEnabled = false;
            TargetPlatformComboBox.IsEnabled = false;
            CompressButton.IsEnabled = false;
        }


        // Enable main controls
        private void EnableControls()
        {
            DataSourceTabControl.IsEnabled = true;
            BlockSizeBitsComboBox.IsEnabled = true;
            DeltaOptionComboBox.IsEnabled = true;
            SettingsModeComboBox.IsEnabled = true;
            TargetPlatformComboBox.IsEnabled = true;
            CompressButton.Content = Constants.CompressButtonCaption;
            CompressButton.IsEnabled = (CurrentContext.Source != null);
        }


        // Clean the context
        private void CleanContext(CompressionContext context)
        {
            context.Clean();
            CompressionProgress.Value = 0.0d;
            UpdateResultsUIElements();
            UpdateProgressBarColor();
        }


        // Display message dialog
        private async Task DisplayMessage(MessageKind kind, string text, string caption)
        {
            await new MessageDialog(kind, text, caption).ShowDialog(this);
        }


        // Show the open file dialog and return the local path of the selected file
        private async Task<string?> PickFileToOpenAsync(IReadOnlyList<FilePickerFileType> fileTypes)
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = fileTypes,
                SuggestedStartLocation = _lastFolder
            });
            if (files.Count == 0) return null;

            return await GetLocalPathAsync(files[0]);
        }


        // Show the save file dialog and return the local path of the selected file
        private async Task<string?> PickFileToSaveAsync(string suggestedName)
        {
            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = suggestedName,
                DefaultExtension = Constants.ExportFileExtension,
                FileTypeChoices = [ Constants.HeaderFileType, FilePickerFileTypes.All ],
                ShowOverwritePrompt = true,
                SuggestedStartLocation = _lastFolder
            });
            if (file == null) return null;

            return await GetLocalPathAsync(file);
        }


        // Remember the folder and get the local path of a picked file
        private async Task<string?> GetLocalPathAsync(IStorageFile file)
        {
            _lastFolder = await file.GetParentAsync() ?? _lastFolder;
            string? path = file.TryGetLocalPath();
            if (path == null) await DisplayMessage(MessageKind.Error, Constants.LocalFilesOnlyError, Constants.ErrorWindowCaption);
            return path;
        }


        // Get formatted file size string
        private static string GetSizeString(long size)
        {
            if (size < 1024)
            {
                return String.Format("{0}" + Constants.ByteSymbol, size);
            }
            else if (size < 10240)
            {
                return String.Format("{0:0.00}" + Constants.KilobyteSymbol, (double)size / 1024.0d);
            }
            else if (size < 1048576)
            {
                return String.Format("{0:0.0}" + Constants.KilobyteSymbol, (double)size / 1024.0d);
            }
            else
            {
                return String.Format("{0:0.00}" + Constants.MegabyteSymbol, (double)size / 1048576.0d);
            }
        }


        // Choosing the best substring from the original file name
        // to use in the resulting .h-file variable names.
        private static string GetBestAlphanumericSubstring(string sourceString)
        {
            // Split into substrings
            List<string> strings = [];
            string currentSubstring = "";
            foreach (char c in sourceString)
            {
                if (Char.IsLetterOrDigit(c))
                {
                    currentSubstring += c;
                }
                else if (currentSubstring.Length > 0)
                {
                    strings.Add(currentSubstring);
                    currentSubstring = "";
                }
            }
            if (currentSubstring.Length > 0) strings.Add(currentSubstring);

            // Get the 1st one if it is long enough or the longest one
            if (strings.Count > 0)
            {
                if (strings[0].Length > 2) return strings[0];
                return strings.Aggregate("", (max, cur) => max.Length > cur.Length ? max : cur);
            }

            return sourceString;
        }

    }
}

// END-OF-FILE
