using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Security.Policy;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;
using System.Configuration;
using System.Security.AccessControl;
using System.Collections;
using static DeltaComp.Compressor;


namespace DeltaComp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// There won't be too much description in this file, because it's just a UI.
    /// </summary>
    /// 


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

            // Default max buffer size for PixelDataSource
            public const int MaxBufferDataSize = 104857600;

            // Messages
            public const string WaringWindowCaption = "Warning";
            public const string ErrorWindowCaption = "Error";
            public const string UnsupportedPixelFormatWarning = "Unsupported pixel format: {0}. Image will be converted to the supported one.";
            public const string UnsupportedImageSizeError = "Unsupported image size: {0}x{1}.";
            public const string CannotConvertError = "Unsupported pixel format: {0}. Please try another image.";
            public const string CannotGrayscaleError = "The pixel format ({0}) does not support conversion to gray.";
            public const string CannotPreviewError = "Unable to preview source image pixel format: {0}";
            public const string DefaultFileError = "Error reading file.";
            public const string UnsupportedFileSizeError = "Unsupported file size: {0}.";

            // Other string constants
            public const string CompressButtonCaption = "Compress";
            public const string CancelButtonCaption = "Cancel";
            public const string NoSplitOption = "No split";
            public const string ImagesFilter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.ico;*.wdp;*.tif;*.tiff|All Files (*.*)|*.*";
            public const string FilesFilter = "All Files (*.*)|*.*";
            public const string ExportFilesFilter = "Header files|*.h|All Files (*.*)|*.*";
            public const string ExportFileExtension = ".h";
            public const string ByteSymbol = "B";
            public const string KilobyteSymbol = "KB";
            public const string MegabyteSymbol = "MB";
            public const string DefaultSizeString = "0.00" + KilobyteSymbol;
            public const string DefaultEfficiencyString = "0.00%";
            public const string DefaultFileNameString = "-";
            public const string DefaultXmlFilename = "Header_Source.xml";
            public const string DefaultXsltFilename = "Header_Transformation.xslt";
            public const string DefaultTargetPlatform = "All";

            // Supported pixel formats list
            public static readonly Dictionary<PixelFormat, PixelFormatParameters> SupportedFormats = 
                new Dictionary<PixelFormat, PixelFormatParameters>()
            {
                // Grayscale (or b/w)
                { PixelFormats.BlackWhite, new PixelFormatParameters() { IsColor = false, IsAlpha = false } },
                { PixelFormats.Gray2, new PixelFormatParameters() { IsColor = false, IsAlpha = false } },
                { PixelFormats.Gray4, new PixelFormatParameters() { IsColor = false, IsAlpha = false } },
                { PixelFormats.Gray8, new PixelFormatParameters() { IsColor = false, IsAlpha = false } },
                { PixelFormats.Gray16, new PixelFormatParameters() { IsColor = false, IsAlpha = false } },

                // Non-standard color depth
                { PixelFormats.Bgr555, new PixelFormatParameters() { IsColor = true, IsAlpha = false, Order = ChannelOrder.ChannelOrderBGRA } },
                { PixelFormats.Bgr565, new PixelFormatParameters() { IsColor = true, IsAlpha = false, Order = ChannelOrder.ChannelOrderBGRA } },
                { PixelFormats.Bgr101010, new PixelFormatParameters() { IsColor = true, IsAlpha = false, Order = ChannelOrder.ChannelOrderBGRA } },

                // Full color
                { PixelFormats.Bgr24, new PixelFormatParameters() { IsColor = true, IsAlpha = false, Order = ChannelOrder.ChannelOrderBGRA } },
                { PixelFormats.Bgr32, new PixelFormatParameters() { IsColor = true, IsAlpha = false, Order = ChannelOrder.ChannelOrderBGRA } },
                { PixelFormats.Rgb24, new PixelFormatParameters() { IsColor = true, IsAlpha = false, Order = ChannelOrder.ChannelOrderRGBA } },
                { PixelFormats.Rgb48, new PixelFormatParameters() { IsColor = true, IsAlpha = false, Order = ChannelOrder.ChannelOrderRGBA } },

                // Alpha
                { PixelFormats.Bgra32, new PixelFormatParameters() { IsColor = true, IsAlpha = true, Order = ChannelOrder.ChannelOrderBGRA } },
                { PixelFormats.Rgba64, new PixelFormatParameters() { IsColor = true, IsAlpha = true, Order = ChannelOrder.ChannelOrderRGBA } },

                // Pre-multiplied alpha
                { PixelFormats.Pbgra32, new PixelFormatParameters() { IsColor = true, IsAlpha = true, Order = ChannelOrder.ChannelOrderBGRA } },
                { PixelFormats.Prgba64, new PixelFormatParameters() { IsColor = true, IsAlpha = true, Order = ChannelOrder.ChannelOrderRGBA } }

                // Unsupported directly formats
                // Gray32Float
                // Indexed1, Indexed2, Indexed4, Indexed8
                // Cmyk32
                // Prgba128Float
                // Rgb128Float
                // Rgba128Float
            };

            // Some default pixel format convertions list
            public static readonly Dictionary<PixelFormat, PixelFormat> DefaultConversions = new Dictionary<PixelFormat, PixelFormat>()
            {
                // Unsupported to supported formats
                { PixelFormats.Gray32Float, PixelFormats.Gray16 },
                { PixelFormats.Indexed1, PixelFormats.Bgr24 },
                { PixelFormats.Indexed2, PixelFormats.Bgr24 },
                { PixelFormats.Indexed4, PixelFormats.Bgr24 },
                { PixelFormats.Indexed8, PixelFormats.Bgr24 },
                { PixelFormats.Cmyk32, PixelFormats.Bgr32 },
                { PixelFormats.Prgba128Float, PixelFormats.Prgba64 },
                { PixelFormats.Rgb128Float, PixelFormats.Rgb48 },
                { PixelFormats.Rgba128Float, PixelFormats.Rgba64 },

                // Color to Grayscale formats
                { PixelFormats.Bgr555, PixelFormats.Gray8 },
                { PixelFormats.Bgr565, PixelFormats.Gray8 },
                { PixelFormats.Bgr101010, PixelFormats.Gray16 },
                { PixelFormats.Bgr24, PixelFormats.Gray8 },
                { PixelFormats.Bgr32, PixelFormats.Gray8 },
                { PixelFormats.Rgb24, PixelFormats.Gray8 },
                { PixelFormats.Rgb48, PixelFormats.Gray16 },
                { PixelFormats.Bgra32, PixelFormats.Gray8 },
                { PixelFormats.Rgba64, PixelFormats.Gray16 },
                { PixelFormats.Pbgra32, PixelFormats.Gray8 },
                { PixelFormats.Prgba64, PixelFormats.Gray16 }
            };

            // Preview pixel formats list
            public static readonly Dictionary<PixelFormat, PixelFormat[]> PreviewFormats = new Dictionary<PixelFormat, PixelFormat[]>()
            {
                { PixelFormats.BlackWhite, [ PixelFormats.Gray8, PixelFormats.Bgra32] },
                { PixelFormats.Gray2, [ PixelFormats.Gray8, PixelFormats.Bgra32 ] },
                { PixelFormats.Gray4, [ PixelFormats.Gray8, PixelFormats.Bgra32 ] },
                { PixelFormats.Gray8, [ PixelFormats.Gray8, PixelFormats.Bgra32 ] },
                { PixelFormats.Gray16, [ PixelFormats.Gray16, PixelFormats.Rgba64 ] },

                { PixelFormats.Bgr555, [ PixelFormats.Bgr32 ] },
                { PixelFormats.Bgr565, [ PixelFormats.Bgr32 ] },
                { PixelFormats.Bgr101010, [ PixelFormats.Rgb48 ] },

                { PixelFormats.Bgr24, [ PixelFormats.Bgr32 ] },
                { PixelFormats.Bgr32, [ PixelFormats.Bgr32 ] },
                { PixelFormats.Rgb24, [ PixelFormats.Bgr32 ] },
                { PixelFormats.Rgb48, [ PixelFormats.Rgb48 ] },

                { PixelFormats.Bgra32, [ PixelFormats.Bgra32, PixelFormats.Bgr32 ] },
                { PixelFormats.Rgba64, [ PixelFormats.Rgba64, PixelFormats.Rgb48 ] },
                { PixelFormats.Pbgra32, [ PixelFormats.Pbgra32 ] },
                { PixelFormats.Prgba64, [ PixelFormats.Prgba64 ] }
            };

            // Default progress bar color
            public static readonly System.Windows.Media.Color DefaultProgressColor = 
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF06B025");

            // Preview pixel formats list
            public static readonly Dictionary<CompressionStage, 
                System.Windows.Media.Color> CompressionStageColors = new Dictionary<CompressionStage, System.Windows.Media.Color>()
            {
                { CompressionStage.None, System.Windows.Media.Colors.Transparent },
                { CompressionStage.Preparation, System.Windows.Media.Colors.DodgerBlue },
                { CompressionStage.Analysis, System.Windows.Media.Colors.DodgerBlue },
                { CompressionStage.Compression, DefaultProgressColor },
                { CompressionStage.Done, DefaultProgressColor },
                { CompressionStage.Cancelled, System.Windows.Media.Colors.Gray },
                { CompressionStage.Failed, System.Windows.Media.Colors.Crimson }
            };

            // Default splitting into squares option (No split)
            public static readonly SquaringOption DefaultSquaringOption = 
                new SquaringOption { Size = 0, Name = Constants.NoSplitOption };

            // Default pixel formats
            public static readonly PixelFormat DefaultColorPixelFormat = PixelFormats.Bgr24;
            public static readonly PixelFormat DefaultGrayscalePixelFormat = PixelFormats.Gray8;

        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private structures
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Pixel format parameters which we cannot directly get from the PixelFormat structure.
        private struct PixelFormatParameters
        {
            public bool IsColor;
            public bool IsAlpha;
            public ChannelOrder Order;
        }

        // A structure describing the option of splitting the image into squares.
        private struct SquaringOption
        {
            public byte Size;
            public string Name { get; set; }
        }

        // Delegate to be called from the compression routine
        private delegate void UpdatePreviewDelegate();

        // Description of the compression context related to one of the tabs (Image/Audio/File)
        private struct CompressionContext
        {
            public string Name;
            public DataSource? Source;
            public PreviewDestination? Preview;
            public string Filepath;
            public string Filename;
            public long SourceSize;
            public volatile int CompressedSize;
            public bool PreviewIsUpToDate;
            public int SelectedBlockSize;
            public int SelectedDeltaOption;
            public int SelectedSearchOption;
            public int SelectedPlatform;
            public volatile CompressionStage Stage;
            public volatile float Progress;
            public List<Channel> Channels;
            public UpdatePreviewDelegate? UpdatePreview;
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // A list of available square sizes into which the image can be split
        private List<SquaringOption> _squaringOptions = new List<SquaringOption>()
                                            { Constants.DefaultSquaringOption };

        // Array of contexts and current context
        private CompressionContext[] _contexts = null!;
        private int _currentContextIndex = 0;

        // When the image is loading, we must prohibit the processing of interface actions.
        private bool _dataLoading = false;

        // Source image parameters
        private BitmapSource? _originalBitmapSource = null;
        private PixelFormatParameters _originalPixelFormatParameters;
        private bool _bitmapGrayscaled;
        private PixelFormat _grayscaledPixelFormat;

        // Compression Task cancellation token
        private CancellationTokenSource _compressionCancellationTokenSource = new CancellationTokenSource();

        // Image/data max buffer size
        private int _maxBufferSize = Constants.MaxBufferDataSize;

        // Compressor class instance
        private Compressor? _compressor = null;

        // Export class instance
        private CodeExport? _export = null;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Basic preparations for work
        public MainWindow()
        {
            InitializeComponent();

            ImageSplitIntoSquaresComboBox.ItemsSource = _squaringOptions;

            // Read MaxBufferSize setting
            string? maxBufferSizeConfig = ConfigurationManager.AppSettings["MaxBufferSize"];
            int.TryParse(maxBufferSizeConfig, out _maxBufferSize);

            // Read Platforms setting
            string? platformsConfig = ConfigurationManager.AppSettings["Platforms"];
            if (platformsConfig != null)
            {
                foreach (string platform in platformsConfig.Split(','))
                {
                    ComboBoxItem platformItem = new ComboBoxItem();
                    platformItem.Content = platform;
                    TargetPlatformComboBox.Items.Add(platformItem);
                }
            }
            else
            {
                ComboBoxItem platformItem = new ComboBoxItem();
                platformItem.Content = Constants.DefaultTargetPlatform;
                TargetPlatformComboBox.Items.Add(platformItem);
            }

            TargetPlatformComboBox.SelectedIndex = 0;

            // Configure contexts
            _contexts = new CompressionContext[DataSourceTabControl.Items.Count];
            // Init CompressionContext for every tab
            for (int i = 0; i < DataSourceTabControl.Items.Count; i++)
            {
                TabItem item = (TabItem)DataSourceTabControl.Items.GetItemAt(i);
                CompressionContext context = new CompressionContext()
                {
                    Name = item.Name,
                    Source = null,
                    Preview = null,
                    Filepath = String.Empty,
                    Filename = String.Empty,
                    SourceSize = 0,
                    CompressedSize = 0,
                    PreviewIsUpToDate = false,
                    SelectedBlockSize = BlockSizeBitsComboBox.SelectedIndex,
                    SelectedDeltaOption = DeltaOptionComboBox.SelectedIndex,
                    SelectedSearchOption = BlockSearchOptionComboBox.SelectedIndex,
                    SelectedPlatform = TargetPlatformComboBox.SelectedIndex,
                    Stage = CompressionStage.None,
                    Progress = 0.0f,
                    Channels = new List<Channel>(),
                    UpdatePreview = null
                };
                _contexts[i] = context;
            }

            _currentContextIndex = DataSourceTabControl.SelectedIndex;
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Image tab
        // Action handlers
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Open new image
        private void ImageOpenButton_Click(object sender, RoutedEventArgs e)
        {
            // Configure open file dialog box
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = Constants.ImagesFilter;
            dialog.RestoreDirectory = true;

            // Show open file dialog box
            bool? openResult = dialog.ShowDialog();

            if (openResult != true) return;

            // Get a referece to current CompressionContext structure
            ref CompressionContext currentContext = ref _contexts[_currentContextIndex];
            CleanContext(ref currentContext);

            ImageSourceViewer.Source = null;
            _originalBitmapSource = null;
            _dataLoading = true;

            ImageUpdatePreviewButton.IsEnabled = false;
            ImageBitDepthMinusButton.IsEnabled = false;
            ImageBitDepthPlusButton.IsEnabled = false;
            ImageChannelDownButton.IsEnabled = false;
            ImageChannelUpButton.IsEnabled = false;
            ImageSplitIntoSquaresComboBox.IsEnabled = false;
            CompressButton.IsEnabled = false;

            // Init BitmapImage
            BitmapImage? OriginalBitmap = new BitmapImage();

            string selectedFile = dialog.FileName;

            try
            {
                OriginalBitmap.BeginInit();
                OriginalBitmap.UriSource = new Uri(selectedFile);

                // I don't want any additional transformations here.
                OriginalBitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                OriginalBitmap.EndInit();
            }
            catch (Exception ex)
            {
                DisplayMessage(MessageBoxImage.Error, ex.Message, Constants.ErrorWindowCaption);
                OriginalBitmap = null;
            }

            if (OriginalBitmap != null)
            {
                // Check min size
                if ((OriginalBitmap.PixelWidth < Constants.MinImageDimension) || (OriginalBitmap.PixelWidth > Constants.MaxImageDimension) ||
                    (OriginalBitmap.PixelHeight < Constants.MinImageDimension) || (OriginalBitmap.PixelHeight > Constants.MaxImageDimension))
                {
                    DisplayMessage(MessageBoxImage.Error, String.Format(Constants.UnsupportedImageSizeError,
                        OriginalBitmap.PixelWidth.ToString(), OriginalBitmap.PixelHeight.ToString()), Constants.ErrorWindowCaption);
                }
                else
                {
                    PixelFormat pixelFormat = OriginalBitmap.Format;

                    // Check if pixel format supported
                    if (!Constants.SupportedFormats.TryGetValue(pixelFormat, out _originalPixelFormatParameters))
                    {
                        // If not then try to convert to one of the supported formats.
                        PixelFormat convertedPixelFormat;
                        if (!Constants.DefaultConversions.TryGetValue(pixelFormat, out convertedPixelFormat))
                        {
                            convertedPixelFormat = Constants.DefaultColorPixelFormat;
                        }

                        if (!Constants.SupportedFormats.TryGetValue(convertedPixelFormat, out _originalPixelFormatParameters))
                        {
                            // Supported pixel format/conversion not found.
                            DisplayMessage(MessageBoxImage.Error, String.Format(Constants.CannotConvertError,
                                pixelFormat.ToString()), Constants.ErrorWindowCaption);
                        }
                        else
                        {
                            DisplayMessage(MessageBoxImage.Warning, String.Format(Constants.UnsupportedPixelFormatWarning,
                                pixelFormat.ToString()), Constants.WaringWindowCaption);

                            // Convert pixel format using FormatConvertedBitmap.
                            FormatConvertedBitmap? ConvertedBitmap = new FormatConvertedBitmap();
                            
                            try
                            {
                                ConvertedBitmap.BeginInit();

                                // Use the OriginalBitmap (BitmapSource) object defined above as the source 
                                // for this new BitmapSource (chain the BitmapSource objects together).
                                ConvertedBitmap.Source = OriginalBitmap;
                                ConvertedBitmap.DestinationFormat = convertedPixelFormat;
                                ConvertedBitmap.EndInit();
                            }
                            catch (Exception ex)
                            {
                                DisplayMessage(MessageBoxImage.Error, ex.Message, Constants.ErrorWindowCaption);
                                OriginalBitmap = null;
                                ConvertedBitmap = null;
                            }

                            _originalBitmapSource = ConvertedBitmap;
                        }
                    }
                    else
                    {
                        _originalBitmapSource = OriginalBitmap;
                    }

                    currentContext.Filepath = selectedFile;
                    currentContext.Filename = System.IO.Path.GetFileName(selectedFile);
                }
            }

            // Display source image
            ImageSourceViewer.Source = _originalBitmapSource;

            // Init PixelDataSource for further data processing
            bool bitmapSourceAvailable = (_originalBitmapSource != null);
            if (bitmapSourceAvailable)
            {
                BitmapDataSource bitmapDataSource = new BitmapDataSource(_originalBitmapSource!, _maxBufferSize);
                bitmapDataSource.MainThreadId = Thread.CurrentThread.ManagedThreadId;

                // Check the available sizes of squares for splitting
                _squaringOptions.Clear();
                _squaringOptions.Add(Constants.DefaultSquaringOption);
                for (byte i = 2; i <= 16; i++)
                {
                    if (((bitmapDataSource.Width % i) == 0) && ((bitmapDataSource.Height % i) == 0))
                    {
                        _squaringOptions.Add(new SquaringOption { Size = i, Name = string.Format("{0}x{0}", i.ToString()) });
                    }
                }
                ImageSplitIntoSquaresComboBox.Items.Refresh();
                if (ImageSplitIntoSquaresComboBox.SelectedIndex < 0) ImageSplitIntoSquaresComboBox.SelectedIndex = 0;
                ImageSplitIntoSquaresComboBox.IsEnabled = true;

                currentContext.Source = bitmapDataSource;

                // Image loaded so we can compress it
                CompressButton.IsEnabled = true;
            }

            // Update checkboxes
            ImageConvertToGrayscaleCheckBox.IsEnabled = bitmapSourceAvailable && _originalPixelFormatParameters.IsColor;
            ImageConvertToGrayscaleCheckBox.IsChecked = bitmapSourceAvailable && !_originalPixelFormatParameters.IsColor;
            ImageUseAlphaCheckBox.IsEnabled = bitmapSourceAvailable && _originalPixelFormatParameters.IsAlpha;
            ImageUseAlphaCheckBox.IsChecked = bitmapSourceAvailable && _originalPixelFormatParameters.IsAlpha;

            // Update pixel component channels list
            ImageUpdateChannels();

            _dataLoading = false;

        }


        // Update image preview
        private void ImageUpdatePreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_contexts[_currentContextIndex].Preview == null) ImageInitializePreview();
            ImageUpdatePreview();
        }


        // One of Channels checkboxes (Convert to Grayscale or Use Alpha) changes its "isChecked" state.
        private void ImageChannelParametersChanged(object sender, RoutedEventArgs e)
        {
            if (!_dataLoading)
            {
                ImageUpdateChannels();
            }
        }


        // Depending on the selected line (channel), we can find the possible directions of its movement.
        private void ImageChannelsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ImageUpdateMoveChannelsButtons();
        }


        // One of the channel row movement buttons was pressed, so we need to find the direction of movement.
        private void ImageChannelMoveButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedRow = ImageChannelsDataGrid.SelectedIndex;
            Channel channel = _contexts[_currentContextIndex].Channels[selectedRow];
            if (sender == ImageChannelDownButton)
            {
                selectedRow++;
            }
            else
            {
                selectedRow--;
            }

            // And finally move selected row...
            ImageMoveChannelToPosition(channel, selectedRow);
        }


        // One of the changing bit depth buttons was pressed
        private void ImageAllChannelsChangeBitDepthButton_Click(object sender, RoutedEventArgs e)
        {
            // Change the bit depth of all channels
            int step = (sender == ImageBitDepthMinusButton) ? -1 : 1;
            foreach (Channel c in _contexts[_currentContextIndex].Channels)
            {
                int newBitDepth = c.TargetBitDepth + step;
                if ((newBitDepth >= 1) && (newBitDepth <= c.BitsPerChannel)) c.TargetBitDepth = newBitDepth;
            }
            ImageChannelsDataGrid.Items.Refresh();

            // And update the "isEnabled" attribute of the bit depth change buttons.
            ImageUpdateBitDepthButtons();
            _contexts[_currentContextIndex].PreviewIsUpToDate = false;
        }


        // The bit depth has been changed manually, so we need to update the bit depth buttons.
        private void ImageChannelBitDepthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ImageUpdateBitDepthButtons();
            _contexts[_currentContextIndex].PreviewIsUpToDate = false;
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Image tab
        // Other procedures that serve the UI
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Update pixel component channels list
        private void ImageUpdateChannels()
        {
            // The number and types of channels will be changed, so the preview image must be reinitialized.
            ImageDestinationViewer.Source = null;
            ImageChannelsDataGrid.ItemsSource = null;
            ImageOriginalSizeLabel.Content = Constants.DefaultSizeString;
            ImageSourceSizeLabel.Content = Constants.DefaultSizeString;
            _bitmapGrayscaled = false;

            // Get a referece to current CompressionContext structure
            ref CompressionContext currentContext = ref _contexts[_currentContextIndex];
            currentContext.Preview = null;
            BitmapDataSource? bitmapDataSource = (BitmapDataSource?)currentContext.Source;
            List<Channel> channels = currentContext.Channels;
            channels.Clear();

            int bitsPerPixel = 0;

            // Check if source data available
            if ((_originalBitmapSource == null) || (bitmapDataSource == null)) return;

            // Check if source image is color and Convert to Grayscale is needed
            if (_originalPixelFormatParameters.IsColor && (ImageConvertToGrayscaleCheckBox.IsChecked == true))
            {
                if (!Constants.DefaultConversions.TryGetValue(_originalBitmapSource.Format, out _grayscaledPixelFormat))
                {
                    _grayscaledPixelFormat = Constants.DefaultGrayscalePixelFormat;
                }
                _bitmapGrayscaled = true;
            }

            if (!_originalPixelFormatParameters.IsColor)
            {
                // Source image is Grayscale
                ColorComponentChannel grayscaleChannel = new ColorComponentChannel(bitmapDataSource, 
                    ChannelType.Grayscale, _originalPixelFormatParameters.Order);
                channels.Add(grayscaleChannel);
                bitsPerPixel += grayscaleChannel.BitsPerChannel;
            }
            else
            {
                // Source image is color...
                List<ColorComponentChannel> colorComponents = new List<ColorComponentChannel>();

                for (int n = (int)ChannelType.Red; n <= (int)ChannelType.Blue; n++)
                {
                    ColorComponentChannel component = new ColorComponentChannel(bitmapDataSource, 
                        (ChannelType)n, _originalPixelFormatParameters.Order);
                    colorComponents.Add(component);
                    bitsPerPixel += component.BitsPerChannel;
                }

                if (_bitmapGrayscaled)
                {
                    // ...and should be converted to Grayscale...
                    GrayscaleMixerChannel grayscaleChannel = new GrayscaleMixerChannel(colorComponents, _grayscaledPixelFormat);
                    channels.Add(grayscaleChannel);
                }
                else
                {
                    // ...or should be processed as it is.
                    channels.AddRange(colorComponents);
                }
            }

            if (_originalPixelFormatParameters.IsAlpha && (ImageUseAlphaCheckBox.IsChecked == true))
            {
                // Source image contains an alpha channel
                ColorComponentChannel alphaChannel = new ColorComponentChannel(bitmapDataSource, 
                    ChannelType.Alpha, _originalPixelFormatParameters.Order);
                channels.Add(alphaChannel);
                bitsPerPixel += alphaChannel.BitsPerChannel;
            }

            // Display channels list
            ImageChannelsDataGrid.ItemsSource = channels;

            // Update source size label
            ImageOriginalSizeLabel.Content = GetSizeString(bitmapDataSource.Height * 
                ((bitmapDataSource.Width * bitsPerPixel + 7) >> 3));

            // Initialize preview image
            ImageInitializePreview();

            // Update bit depth buttons
            ImageUpdateBitDepthButtons();
        }


        // Initialize preview environment
        private void ImageInitializePreview()
        {
            // Check if source data available
            if (_originalBitmapSource == null) return;

            // Next we must find correct pixel format for the preview according to the following rules:
            //
            // 1. If the original image is colored (regardless of the alpha channel presence)
            // or grayscale (or converted from color, but without an alpha channel), then the pixel format
            // corresponding to the components should be selected and the channels are copied one to one.
            //
            // 2. If the original color image had an alpha channel, but it was removed, then the color
            // pixel format without the alpha channel is selected for preview.
            //
            // 3. If the original image with alpha channel has been converted to gray and
            // the alpha channel is used, then the color pixel format with an alpha channel should be 
            // selected and the values for each of the color channels should be set to the same 
            // corresponding grayscale value in order to present shades of gray.
            //

            // Get a referece to current CompressionContext structure
            ref CompressionContext currentContext = ref _contexts[_currentContextIndex];

            if (currentContext.UpdatePreview == null) currentContext.UpdatePreview = ImageUpdatePreview;
            currentContext.PreviewIsUpToDate = false;
            ChannelMapping mapping = ChannelMapping.OneToOne;
            bool secondPreviewOption = false;
            PixelFormat sourcePixelFormat = _bitmapGrayscaled ? _grayscaledPixelFormat : _originalBitmapSource.Format;

            if (_originalPixelFormatParameters.IsAlpha)
            {
                if (_bitmapGrayscaled != (ImageUseAlphaCheckBox.IsChecked == false)) secondPreviewOption = true;
                if (_bitmapGrayscaled && (ImageUseAlphaCheckBox.IsChecked == true)) mapping = ChannelMapping.GrayscaleAlphaToColorAlpha;
            }

            PixelFormat[]? previewPixelFormats;
            PixelFormat previewPixelFormat;
            bool previewFormatDefined = false;

            // Try to find appropriate preview pixel format according to the specified criteria
            if (Constants.PreviewFormats.TryGetValue(sourcePixelFormat, out previewPixelFormats))
            {
                if (secondPreviewOption)
                {
                    if (previewPixelFormats.Length > 1)
                    {
                        previewPixelFormat = previewPixelFormats[1];
                        previewFormatDefined = true;
                    }
                }
                else
                {
                    previewPixelFormat = previewPixelFormats[0];
                    previewFormatDefined = true;
                }
            }

            // Check if a suitable pixel format has been found
            PixelFormatParameters PreviewPixelFormatParameters;
            if (!previewFormatDefined || !Constants.SupportedFormats.TryGetValue(previewPixelFormat, out PreviewPixelFormatParameters))
            {
                // Preview format not found
                DisplayMessage(MessageBoxImage.Error, String.Format(Constants.CannotPreviewError,
                    sourcePixelFormat.ToString()), Constants.ErrorWindowCaption);
            }
            else
            {
                // Init PreviewBitmapDestination, process and display preview image.
                BitmapPreviewDestination previewDestination = new BitmapPreviewDestination(_originalBitmapSource.PixelWidth, 
                    _originalBitmapSource.PixelHeight, _originalBitmapSource.DpiX, _originalBitmapSource.DpiY, 
                    currentContext.Channels, previewPixelFormat, PreviewPixelFormatParameters.Order, mapping);
                currentContext.Preview = previewDestination;
                ImageDestinationViewer.Source = previewDestination.Bitmap;
                ImageUpdatePreviewButton.IsEnabled = true;
                ImageUpdatePreview();
            }

        }


        // Update preview accordig to selected channel parameters
        private void ImageUpdatePreview()
        {
            // Get a referece to current CompressionContext structure
            ref CompressionContext currentContext = ref _contexts[_currentContextIndex];

            if ((_originalBitmapSource == null) || (currentContext.Preview == null)) return;

            currentContext.Preview.UpdatePreview();

            // Calculate the data size based on the number of channels and color depth
            int sourceBitsPerPixel = 0;
            foreach (Channel c in currentContext.Channels) sourceBitsPerPixel += c.TargetBitDepth;
            long size = (sourceBitsPerPixel * _originalBitmapSource.PixelWidth * _originalBitmapSource.PixelHeight + 7) >> 3;
            currentContext.SourceSize = size;
            ImageSourceSizeLabel.Content = GetSizeString(size);

            currentContext.PreviewIsUpToDate = true;
        }


        // Move channel up or down and update ChannelsDataGrid keeping the selected row.
        private void ImageMoveChannelToPosition(Channel channel, int position)
        {
            _contexts[_currentContextIndex].Channels.Remove(channel);
            _contexts[_currentContextIndex].Channels.Insert(position, channel);
            ImageChannelsDataGrid.Items.Refresh();
            ImageChannelsDataGrid.SelectedItem = channel;
            ImageChannelsDataGrid.ScrollIntoView(channel);
            DataGridRow row = (DataGridRow)ImageChannelsDataGrid.ItemContainerGenerator.ContainerFromIndex(position);
            row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }


        // If all channels are set to the minimum or maximum bit depth, then further decreasing or increasing it
        // is impossible, which means we can disable the corresponding button.
        private void ImageUpdateBitDepthButtons()
        {
            // Check if source data availsble and we have at least one channel with a bit depth of more than 1.
            ref CompressionContext currentContext = ref _contexts[_currentContextIndex];
            if ((currentContext.Source != null) && (currentContext.Source.BitsPerFrame > currentContext.Source.ChannelsCount) &&
                (currentContext.Channels.Count > 0))
            {
                bool allChannelsAtMinBitDepth = true;
                bool allChannelsAtMaxBitDepth = true;
                foreach(Channel c in currentContext.Channels)
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
            int selectedRow = ImageChannelsDataGrid.SelectedIndex;
            if ((selectedRow != -1) && (_contexts[_currentContextIndex].Channels.Count > 1))
            {
                // We can move the row up if it is not the top one.
                ImageChannelUpButton.IsEnabled = selectedRow != 0;
                // And we can move the row down if it is not the bottom one.
                ImageChannelDownButton.IsEnabled = selectedRow != (_contexts[_currentContextIndex].Channels.Count - 1);
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
        private void FileOpenButton_Click(object sender, RoutedEventArgs e)
        {
            // Configure open file dialog box
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = Constants.FilesFilter;
            dialog.RestoreDirectory = true;

            // Show open file dialog box
            bool? openResult = dialog.ShowDialog();

            if (openResult != true) return;
           
            string selectedFile = dialog.FileName;

            // Get a reference to current CompressionContext structure
            ref CompressionContext currentContext = ref _contexts[_currentContextIndex];
            CleanContext(ref currentContext);

            FileChannelsCountComboBox.IsEnabled = false;
            FileBitsPerChannelComboBox.IsEnabled = false;
            FileBigEndianRadioButton.IsEnabled = false;
            FileUpdatePreviewButton.IsEnabled = false;
            FileOriginalSizeLabel.Content = Constants.DefaultSizeString;
            FileNameLabel.Content = Constants.DefaultFileNameString;
            FilePreviewTextBox.Text = "";
            CompressButton.IsEnabled = false;

            FileDataSource dataSource = new FileDataSource(selectedFile, _maxBufferSize);

            // Check if file opened
            if (dataSource.ErrorOccurred)
            {
                string message = (dataSource.LastErrorMessage != null) ? dataSource.LastErrorMessage : Constants.DefaultFileError;
                DisplayMessage(MessageBoxImage.Error, message, Constants.ErrorWindowCaption);
                return;
            }
            // Check file size
            if ((dataSource.FileSize < Constants.MinDataFileSize) || (dataSource.FileSize > Constants.MaxDataFileSize)) {
                DisplayMessage(MessageBoxImage.Error, String.Format(Constants.UnsupportedFileSizeError, 
                    GetSizeString(dataSource.FileSize)), Constants.ErrorWindowCaption);
                return;
            }

            currentContext.Source = dataSource;
            currentContext.Filepath = selectedFile;
            currentContext.Filename = System.IO.Path.GetFileName(selectedFile);
            currentContext.SourceSize = dataSource.FileSize;

            FileNameLabel.Content = currentContext.Filename;
            FileOriginalSizeLabel.Content = GetSizeString(dataSource.FileSize);
            currentContext.Channels.Clear();
            currentContext.Preview = new FilePreviewDestination(currentContext.Channels, dataSource, FilePreviewTextBox);
            currentContext.UpdatePreview = FileUpdatePreview;

            FileUpdateChannelsOptions();
            FileUpdatePreviewButton.IsEnabled = true;
            CompressButton.IsEnabled = true;
        }


        // Channels count changed
        private void FileChannelsCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FileChannelsCountChanged();
        }


        // Channels bit depth changed
        private void FileBitsPerChannelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FileUpdateEndiannessOption();
        }


        // File reading endianness changed
        private void FileEndiannessRadioButton_StateChanged(object sender, RoutedEventArgs e)
        {
            FileUpdateChannels();
        }

        // Update preview
        private void FileUpdatePreviewButton_Click(object sender, RoutedEventArgs e)
        {
            FileUpdatePreview();
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Custom data (File) tab
        // Other procedures that serve the UI
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Update a list of available file splitting (into alternating channels) options
        private void FileUpdateChannelsOptions()
        {
            int enabledOptionsCount = 0;
            foreach (ComboBoxItem item in FileChannelsCountComboBox.Items)
            {
                string? selectedValue = item.Content.ToString();
                bool optionEnabled = false;
                if (selectedValue != null)
                {
                    // The file size should be divided by the number of channels without remainder
                    optionEnabled = (_contexts[_currentContextIndex].SourceSize % int.Parse(selectedValue) == 0);
                }
                item.IsEnabled = optionEnabled;
                if (optionEnabled)
                {
                    enabledOptionsCount++;
                }
                else
                {
                    // Check if selected item disabled
                    if (item == (ComboBoxItem)FileChannelsCountComboBox.SelectedItem) 
                        FileChannelsCountComboBox.SelectedIndex = 0;
                }
            }
            FileChannelsCountComboBox.IsEnabled = (enabledOptionsCount > 1);
            FileChannelsCountChanged();
        }


        // Update available bit depth options for selected channels count
        private void FileChannelsCountChanged()
        {
            if (FileBitsPerChannelComboBox == null) return;
            ComboBoxItem selectedItem = (ComboBoxItem)FileChannelsCountComboBox.SelectedItem;
            string? selectedChannelsCountValue = selectedItem?.Content.ToString();
            if (selectedChannelsCountValue != null)
            {
                int channelsCount = int.Parse(selectedChannelsCountValue);
                int enabledOptionsCount = 0;

                foreach (ComboBoxItem item in FileBitsPerChannelComboBox.Items)
                {
                    string? selectedBPCValue = item.Content.ToString();
                    if (selectedBPCValue != null)
                    {
                        // The file size should be divided by the number of channels of selected bit depth without remainder
                        bool optionEnabled = (_contexts[_currentContextIndex].SourceSize % 
                            ((int.Parse(selectedBPCValue) >> 3) * channelsCount) == 0);
                        item.IsEnabled = optionEnabled;
                        if (optionEnabled) enabledOptionsCount++;
                    }
                    else
                    {
                        item.IsEnabled = false;
                    }
                }

                FileBitsPerChannelComboBox.IsEnabled = (enabledOptionsCount > 1);
            }
            else
            {
                FileBitsPerChannelComboBox.SelectedIndex = 0;
                FileBitsPerChannelComboBox.IsEnabled = false;
            }

            // Check if selected item disabled
            selectedItem = (ComboBoxItem)FileBitsPerChannelComboBox.SelectedItem;
            if ((selectedItem == null) || (selectedItem.IsEnabled != true)) FileBitsPerChannelComboBox.SelectedIndex = 0;
            FileUpdateEndiannessOption();
        }


        // Update read endianness option for data file
        private void FileUpdateEndiannessOption()
        {
            if (FileBigEndianRadioButton == null) return;
            FileBigEndianRadioButton.IsEnabled = (FileBitsPerChannelComboBox.SelectedIndex > 0);
            FileUpdateChannels();
        }


        // Update the number and parameters of data channels
        private void FileUpdateChannels()
        {
            if (_contexts == null) return;

            // Get a reference to current CompressionContext structure
            ref CompressionContext currentContext = ref _contexts[_currentContextIndex];
            FileDataSource? dataSource = (FileDataSource?)currentContext.Source;

            if (dataSource == null) return;

            List<Channel> channels = currentContext.Channels;
            int channelsCount = channels.Count;

            ComboBoxItem selectedChannelsItem = (ComboBoxItem)FileChannelsCountComboBox.SelectedItem;
            ComboBoxItem selectedBPCItem = (ComboBoxItem)FileBitsPerChannelComboBox.SelectedItem;
            string? selectedChannelsCountValue = selectedChannelsItem?.Content.ToString();
            string? selectedBPCValue = selectedBPCItem?.Content.ToString();

            if ((selectedChannelsCountValue != null) && (selectedBPCValue != null))
            {
                int selectedChannelsCount = int.Parse(selectedChannelsCountValue);
                int selectedBPC = int.Parse(selectedBPCValue);
                bool isBigEndian = (FileLittleEndianRadioButton.IsChecked != true);

                // Update channels
                if (channels.Count != selectedChannelsCount)
                {
                    channels.Clear();
                    for (int i = 0; i < selectedChannelsCount; i++)
                    {
                        FileDataChannel channel = new FileDataChannel(dataSource, i);
                        channels.Add(channel);
                    }
                }

                int bytesPerChannel = selectedBPC >> 3;
                int bytesPerFrame = bytesPerChannel * selectedChannelsCount;

                dataSource.UpdateSourceParameters(bytesPerFrame, bytesPerChannel, isBigEndian);
                // Update bit depth
                foreach (FileDataChannel channel in channels) channel.UpdateBitDepth();
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
            PreviewDestination? preview = _contexts[_currentContextIndex].Preview;
            if (preview != null)
            {
                preview.UpdatePreview();
                _contexts[_currentContextIndex].PreviewIsUpToDate = true;
            }
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Compression part
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Compress/cancel button click
        private async void CompressButton_Click(object sender, RoutedEventArgs e)
        {
            // Get a copy of current CompressionContext structure
            CompressionContext currentContext = _contexts[_currentContextIndex];

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

            List<Channel> channels = currentContext.Channels;

            // Configure save file dialog box
            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.FileName = GetBestAlphanumericSubstring(currentContext.Filename); // Default file name
            dialog.Filter = Constants.ExportFilesFilter;
            dialog.DefaultExt = Constants.ExportFileExtension;
            dialog.RestoreDirectory = true;

            // Show save file dialog box
            bool? openResult = dialog.ShowDialog();

            if (openResult != true) return;
            
            DisableControls();
            // Here we have to access original CompressionContext structure (not the copy)
            _contexts[_currentContextIndex].CompressedSize = 0;
            CompressionProgress.Value = 0.0d;

            // Update preview if it doesn't match the channel parameters
            if (!currentContext.PreviewIsUpToDate && (currentContext.UpdatePreview != null)) currentContext.UpdatePreview();

            byte bitsPerFrame = 0;
            byte[] bitsPerChannel = new byte[currentContext.Channels.Count];
            for (int i = 0; i < currentContext.Channels.Count; i++)
            {
                Channel currentChannel = currentContext.Channels[i];
                bitsPerFrame += (byte)currentChannel.TargetBitDepth;
                bitsPerChannel[i] = (byte)currentChannel.TargetBitDepth;
            }

            if (currentContext.Source is BitmapDataSource)
            {
                // Current context is an Image context

                BitmapDataSource dataSource = (BitmapDataSource)currentContext.Source;
                _compressor = new Compressor(channels, (UInt16)dataSource.Width, (UInt16)dataSource.Height, 
                    dataSource.MultithreadedReadingAllowed, WriteCompressedByteDelegate, UpdateProgressDelegate);
                SquaringOption selectedOption = (SquaringOption)ImageSplitIntoSquaresComboBox.SelectedItem;
                if (selectedOption.Size > 1)
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
            else if (currentContext.Source is FileDataSource)
            {
                // Current context is a data file context

                FileDataSource dataSource = (FileDataSource)currentContext.Source;
                _compressor = new Compressor(channels, (UInt32)dataSource.FramesCount, dataSource.MultithreadedReadingAllowed, 
                    WriteCompressedByteDelegate, UpdateProgressDelegate);
            }
            else
            {
                // Unimplemented compression context...
                return;
            }

            _compressor.BlockSizeBits = (byte)(Int32)BlockSizeBitsComboBox.SelectedValue;
            _compressor.DeltaCalculationOption = (DeltaOption)DeltaOptionComboBox.SelectedIndex;
            _compressor.UseBruteForceBestBlockSearch = BlockSearchOptionComboBox.SelectedIndex == 1 ? true : false;

            // Create and configure export class instance
            _export = new CodeExport(Constants.DefaultXmlFilename, Constants.DefaultXsltFilename);
            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict.Add("nof", _compressor.TotalFrames.ToString());
            dict.Add("noc", _compressor.ChannelsCount.ToString());
            dict.Add("bpc", "{ " + string.Join(", ", _compressor.BitsPerChannel) + " }");
            dict.Add("bsb", _compressor.BlockSizeBits.ToString());
            dict.Add("bpmd", "{ " + string.Join(", ", _compressor.BitsPerMethodDeclaration) + " }");
            dict.Add("bpf", _compressor.BitsPerFrame.ToString());
            dict.Add("do", ((int)_compressor.DeltaCalculationOption).ToString());
            dict.Add("dos", _compressor.DeltaCalculationOption.ToString());
            dict.Add("iw", _compressor.ImageWidth.ToString());
            dict.Add("ih", _compressor.ImageHeight.ToString());
            dict.Add("sts", _compressor.SplitToSquares ? "1" : "0");
            dict.Add("ss", _compressor.WidthOfSquare.ToString());

            string errorMessage;

            ComboBoxItem selectedTargetPlatformItem = (ComboBoxItem)TargetPlatformComboBox.SelectedItem;
            string? targetPlatform = selectedTargetPlatformItem?.Content.ToString();
            if (targetPlatform == null) targetPlatform = Constants.DefaultTargetPlatform;
            bool preparationResult = _export.PrepareExport(true, dict, targetPlatform.ToLower(), currentContext.Filename, 
                System.IO.Path.GetFileNameWithoutExtension(dialog.FileName), dialog.FileName, out errorMessage);

            if (!preparationResult)
            {
                DisplayMessage(MessageBoxImage.Error, errorMessage, Constants.ErrorWindowCaption);
                _export = null;
                return;
            }

            if (!_compressionCancellationTokenSource.TryReset())
            {
                _compressionCancellationTokenSource.Dispose();
                _compressionCancellationTokenSource = new CancellationTokenSource();
            }

            // Run compression task
            CancellationToken token = _compressionCancellationTokenSource.Token;
            try
            {
                await Task.Run(() => { _compressor.Compress(token); }, token);
            }
            catch (Exception ex)
            {
                string error = ex.Message;

                if (ex is OperationCanceledException)
                {
                    currentContext.Stage = CompressionStage.Cancelled;
                    //Debug.WriteLine("Compression cancelled: " + error);
                }
                else
                {
                    DisplayMessage(MessageBoxImage.Error, ex.Message, Constants.ErrorWindowCaption);
                    UpdateProgressDelegate(CompressionStage.Failed, _contexts[_currentContextIndex].Progress);
                }
            
                EnableControls();
            }
            

            if (!_export.FinalizeExport(out errorMessage))
            {
                DisplayMessage(MessageBoxImage.Error, errorMessage, Constants.ErrorWindowCaption);
            }

            _export = null;
            _compressor = null;

            //Debug.WriteLine("Compressed bytes count: " + _contexts[_currentContextIndex].CompressedSize.ToString());
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Compression delegates
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Write next compressed byte and check data writing status
        public void WriteCompressedByteDelegate(byte compressedByte)
        {
            Debug.Assert((_export != null) && (_compressor != null));

            _export.AppendArrayElement(compressedByte);
            _contexts[_currentContextIndex].CompressedSize++;

            if (_export.LastErrorMessage != null)
            {
                _compressor.RequestFallingIntoError();
                DisplayMessage(MessageBoxImage.Error, _export.LastErrorMessage, Constants.ErrorWindowCaption);
            }
        }


        // Update compression progress and check data reading status
        public void UpdateProgressDelegate(CompressionStage stage, double progress)
        {
            Debug.Assert(_compressor != null);

            // Get a referece to current CompressionContext structure
            ref CompressionContext currentContext = ref _contexts[_currentContextIndex];
            currentContext.Progress = (float)progress;
            CompressionProgress.Value = progress;

            DataSource? dataSource = currentContext.Source;
            Debug.Assert(dataSource != null);

            if (stage != currentContext.Stage)
            {
                currentContext.Stage = stage;
                UpdateProgressBarColor();

                if ((stage == CompressionStage.Preparation) || (stage == CompressionStage.Analysis)
                     || (stage == CompressionStage.Compression)) {
                    CompressButton.Content = Constants.CancelButtonCaption;
                    CompressButton.IsEnabled = true;
                }
                else if (stage == CompressionStage.Done)
                {
                    UpdateResultsUIElements();
                    EnableControls();
                }
                else if ((stage == CompressionStage.Cancelled) || (stage == CompressionStage.Failed)) {
                    EnableControls();
                }
            }

            // Check if reading error occurred
            if (dataSource.ErrorOccurred)
            {
                _compressor.RequestFallingIntoError();
                string message = (dataSource.LastErrorMessage != null) ? dataSource.LastErrorMessage : Constants.DefaultFileError;
                DisplayMessage(MessageBoxImage.Error, message, Constants.ErrorWindowCaption);
                dataSource.ErrorOccurred = false;
            }
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // UI helper methods
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Compression tab changed
        private void DataSourceTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((e.Source is not TabControl) || (_contexts == null) ||
                (_currentContextIndex >= _contexts.Length)) return;

            if (_currentContextIndex >= 0)
            {
                // Save compression parameters for previous tab
                ref CompressionContext lastContext = ref _contexts[_currentContextIndex];
                lastContext.SelectedBlockSize = BlockSizeBitsComboBox.SelectedIndex;
                lastContext.SelectedDeltaOption = DeltaOptionComboBox.SelectedIndex;
                lastContext.SelectedSearchOption = BlockSearchOptionComboBox.SelectedIndex;
                lastContext.SelectedPlatform = TargetPlatformComboBox.SelectedIndex;
            }

            int newContextIndex = DataSourceTabControl.SelectedIndex;
            if (newContextIndex < 0) return;
            _currentContextIndex = newContextIndex;
            ref CompressionContext currentContext = ref _contexts[_currentContextIndex];

            // Restore compression context for selected tab
            BlockSizeBitsComboBox.SelectedIndex = currentContext.SelectedBlockSize;
            DeltaOptionComboBox.SelectedIndex = currentContext.SelectedDeltaOption;
            BlockSearchOptionComboBox.SelectedIndex = currentContext.SelectedSearchOption;
            TargetPlatformComboBox.SelectedIndex = currentContext.SelectedPlatform;

            // Update other UI elements
            if (!currentContext.PreviewIsUpToDate && (currentContext.UpdatePreview != null)) currentContext.UpdatePreview();
            UpdateResultsUIElements();
            UpdateProgressBarColor();
            CompressionProgress.Value = (double)currentContext.Progress;
            CompressButton.IsEnabled = (currentContext.Source != null);
        }


        // Show saved compression results for the current context
        private void UpdateResultsUIElements()
        {
            // Get a referece to current CompressionContext structure
            ref CompressionContext currentContext = ref _contexts[_currentContextIndex];

            if (currentContext.Stage == CompressionStage.Done)
            {
                long compressedSize = _contexts[_currentContextIndex].CompressedSize;
                CompressedSizeLabel.Content = GetSizeString(compressedSize);
                double efficiency = (1.0f - (double)compressedSize / (double)_contexts[_currentContextIndex].SourceSize) * 100.0f;
                CompressionEfficiencyLabel.Content = String.Format("{0:0.00}%", efficiency);
            }
            else
            {
                CompressedSizeLabel.Content = Constants.DefaultSizeString;
                CompressionEfficiencyLabel.Content = Constants.DefaultEfficiencyString;
            }
        }


        // Select progress bar color according to a compression status
        private void UpdateProgressBarColor()
        {
            System.Windows.Media.Color progressBarColor;
            if (!Constants.CompressionStageColors.TryGetValue(_contexts[_currentContextIndex].Stage, out progressBarColor))
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
            BlockSearchOptionComboBox.IsEnabled = false;
            TargetPlatformComboBox.IsEnabled = false;
            CompressButton.IsEnabled = false;

        }


        // Enable main controls
        private void EnableControls()
        {
            bool sourceDataAvailable = (_contexts[_currentContextIndex].Source != null);
            DataSourceTabControl.IsEnabled = true;
            BlockSizeBitsComboBox.IsEnabled = true;
            DeltaOptionComboBox.IsEnabled = true;
            BlockSearchOptionComboBox.IsEnabled = true;
            TargetPlatformComboBox.IsEnabled = true;
            CompressButton.Content = Constants.CompressButtonCaption;
            CompressButton.IsEnabled = sourceDataAvailable;
        }


        // Clean current context
        private void CleanContext(ref CompressionContext context)
        {
            context.Source = null;
            context.Preview = null;
            context.Filepath = String.Empty;
            context.Filename = String.Empty;
            context.SourceSize = 0;
            context.CompressedSize = 0;
            context.Stage = CompressionStage.None;
            context.Progress = 0.0f;
            context.PreviewIsUpToDate = false;

            CompressionProgress.Value = 0.0d;
            UpdateResultsUIElements();
        }


        // Display message dialog
        private void DisplayMessage(MessageBoxImage icon, string text, string caption)
        {
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxResult messageResult = MessageBox.Show(text,
                caption, button, icon, MessageBoxResult.OK);
        }


        // Get formatted file size string
        private string GetSizeString(long size)
        {
            if (size < 1024)
            {
                return (String.Format("{0}" + Constants.ByteSymbol, size));
            }
            else if (size < 10240)
            {
                return (String.Format("{0:0.00}" + Constants.KilobyteSymbol, (double)size / 1024.0f));
            }
            else if (size < 1048576)
            {
                return (String.Format("{0:0.0}" + Constants.KilobyteSymbol, (double)size / 1024.0f));
            }
            else
            {
                return (String.Format("{0:0.00}" + Constants.MegabyteSymbol, (double)size / 1048576.0f));
            }
        }


        // Choosing the best substring from the original file name
        // to use in the resulting .h-file variable names. 
        private string GetBestAlphanumericSubstring(string sourceString)
        {
            // Split into substrings
            List<string> strings = new List<string>();
            string currentSubstring = "";
            for (int i = 0; i < sourceString.Length; i++)
            {
                char c = sourceString[i];
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

            // Get the 1st one if it is long enough or the longest one
            if (strings.Count > 0)
            {
                if (strings[0].Length > 2) return strings[0];
                return (strings.Aggregate("", (max, cur) => max.Length > cur.Length ? max : cur));
            }

            return sourceString;
        }

    }
}

// END-OF-FILE
