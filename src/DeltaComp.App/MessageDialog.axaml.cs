using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace DeltaComp
{
    // Message type of the dialog
    public enum MessageKind
    {
        Information,
        Warning,
        Error
    }


    // A simple modal message box (Avalonia has no built-in one)
    public partial class MessageDialog : Window
    {
        public MessageDialog()
        {
            InitializeComponent();
        }


        public MessageDialog(MessageKind kind, string text, string caption) : this()
        {
            Title = caption;
            MessageText.Text = text;
            (string glyph, Color color) = kind switch
            {
                MessageKind.Error => ("×", Colors.Crimson),
                MessageKind.Warning => ("!", Colors.DarkOrange),
                _ => ("i", Colors.DodgerBlue)
            };
            IconText.Text = glyph;
            IconBorder.Background = new SolidColorBrush(color);
        }


        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
