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
    /// Raised after a file has been picked and its name/data captured, giving the Lab's parameter sync the
    /// only signal it can use to push the picked file to the service. Programmatic <see cref="Data"/>/
    /// <see cref="Filename"/> assignment (e.g. the service-&gt;GUI restore path) is intentionally silent and
    /// does NOT raise this event.
    /// </summary>
    public event EventHandler? FileChanged;

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
    /// Applies a picked file (name + text content) and raises <see cref="FileChanged"/>. This is the single
    /// entry point the file-open flow uses; setting the data this way - rather than via the <see cref="Data"/>/
    /// <see cref="Filename"/> setters - is what notifies listeners that the user picked a new file.
    /// </summary>
    public void ApplyPickedFile(string filename, string data)
    {
        fileName = filename;
        textData = data;
        UpdateStatusLabel();
        FileChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the encoding used for text files (implements TextFile interface).
    /// Not actually used in this class.
    /// </summary>
    public static Encoding Encoding
    {
        get => throw new NotSupportedException("Use TextEncoding instance property instead. This static property should not be accessed.");
    }

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
            ApplyPickedFile(file.Name, await reader.ReadToEndAsync());
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
