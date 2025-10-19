using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Ihc;

namespace IhcLab;

public partial class TextFilePicker : UserControl, TextFile
{
    private TextBlock? fileStatusLabel;
    private string? textData;
    private string? fileName;
    private Encoding textEncoding = Encoding.UTF8;

    /// <summary>
    /// Gets the text file data (implements TextFile interface)
    /// </summary>
    public string Data
    {
        get => textData ?? string.Empty;
        set => textData = value;
    }

    /// <summary>
    /// Gets the text file name (implements TextFile interface)
    /// </summary>
    public string Filename
    {
        get => fileName ?? string.Empty;
        set => fileName = value;
    }

    /// <summary>
    /// Gets the encoding used for text files (implements TextFile interface)
    /// Not actually used in this class.
    /// </summary>
    public static Encoding Encoding { get { throw new NotImplementedException("Not relevant for TextFilePicker. Set at runtime"); } }

    /// <summary>
    /// Gets or sets the text encoding used when reading text files.
    /// Defaults to UTF-8.
    /// </summary>
    public Encoding TextEncoding
    {
        get => textEncoding;
        set => textEncoding = value ?? throw new ArgumentNullException(nameof(value));
    }

    public TextFilePicker()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        fileStatusLabel = this.FindControl<TextBlock>("FileStatusLabel");
        UpdateStatusLabel();
    }

    private async void OnUploadButtonClick(object? sender, RoutedEventArgs e)
    {
        await UploadFileAsync();
    }

    private async Task UploadFileAsync()
    {
        try
        {
            // Get the TopLevel (window) to access the StorageProvider
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                UpdateStatusLabel("Error: Cannot access window");
                return;
            }

            // Configure file picker options
            var filePickerOptions = new FilePickerOpenOptions
            {
                Title = "Select Text File to Upload",
                AllowMultiple = false
            };

            // Open file picker
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(filePickerOptions);

            if (files == null || files.Count == 0)
            {
                // User cancelled
                return;
            }

            var file = files.First();

            // Read file content as text using configured encoding
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream, textEncoding);
            textData = await reader.ReadToEndAsync();
            fileName = file.Name;

            UpdateStatusLabel();
        }
        catch (Exception ex)
        {
            UpdateStatusLabel($"Error: {ex.Message}");
        }
    }

    private void UpdateStatusLabel(string? customMessage = null)
    {
        if (fileStatusLabel == null)
            return;

        if (customMessage != null)
        {
            fileStatusLabel.Text = customMessage;
            return;
        }

        if (textData == null)
        {
            fileStatusLabel.Text = "No file selected";
            return;
        }

        fileStatusLabel.Text = $"{fileName} ({textData.Length} characters)";
    }
}
