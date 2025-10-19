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

namespace IhcLab;

public partial class InputFilePicker : UserControl
{
    private TextBlock? fileStatusLabel;

    /// <summary>
    /// Defines whether the file should be loaded as binary or text
    /// </summary>
    public enum FileType { BinaryFile, StringFile };

    /// <summary>
    /// Union type to store file content as either byte[] or string
    /// </summary>
    private class FileContent
    {
        public byte[]? BinaryData { get; set; }
        public string? TextData { get; set; }
        public FileType ContentType { get; set; }
        public string? FileName { get; set; }

        public bool HasContent => BinaryData != null || TextData != null;
    }

    private FileContent fileContent = new FileContent();
    private FileType currentFileType = FileType.StringFile;
    private Encoding textEncoding = Encoding.UTF8;

    /// <summary>
    /// Gets or sets the file type (binary or text)
    /// </summary>
    public FileType CurrentFileType
    {
        get => currentFileType;
        set
        {
            if (currentFileType != value)
            {
                currentFileType = value;
                // Clear existing content when type changes
                fileContent = new FileContent();
                UpdateStatusLabel();
            }
        }
    }

    /// <summary>
    /// Gets or sets the text encoding used when reading text files.
    /// Defaults to UTF-8. Only applies when CurrentFileType is StringFile.
    /// </summary>
    public Encoding TextEncoding
    {
        get => textEncoding;
        set => textEncoding = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the file content value (byte[] for binary, string for text)
    /// </summary>
    public object? Value
    {
        get
        {
            if (!fileContent.HasContent)
                return null;

            return fileContent.ContentType == FileType.BinaryFile
                ? fileContent.BinaryData
                : fileContent.TextData;
        }
        set
        {
            if (value == null)
            {
                fileContent = new FileContent();
                UpdateStatusLabel();
                return;
            }

            if (value is byte[] binaryData)
            {
                fileContent = new FileContent
                {
                    BinaryData = binaryData,
                    ContentType = FileType.BinaryFile,
                    FileName = "uploaded-binary-data"
                };
                currentFileType = FileType.BinaryFile;
            }
            else if (value is string textData)
            {
                fileContent = new FileContent
                {
                    TextData = textData,
                    ContentType = FileType.StringFile,
                    FileName = "uploaded-text-data"
                };
                currentFileType = FileType.StringFile;
            }
            else
            {
                throw new ArgumentException($"Value must be byte[] or string, but was {value.GetType().Name}", nameof(value));
            }

            UpdateStatusLabel();
        }
    }

    public InputFilePicker()
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
                Title = "Select File to Upload",
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

            // Read file content based on file type
            await using var stream = await file.OpenReadAsync();

            if (currentFileType == FileType.BinaryFile)
            {
                // Read as binary
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);

                fileContent = new FileContent
                {
                    BinaryData = memoryStream.ToArray(),
                    ContentType = FileType.BinaryFile,
                    FileName = file.Name
                };
            }
            else
            {
                // Read as text using configured encoding
                using var reader = new StreamReader(stream, textEncoding);
                var textContent = await reader.ReadToEndAsync();

                fileContent = new FileContent
                {
                    TextData = textContent,
                    ContentType = FileType.StringFile,
                    FileName = file.Name
                };
            }

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

        if (!fileContent.HasContent)
        {
            fileStatusLabel.Text = "No file selected";
            return;
        }

        var sizeInfo = fileContent.ContentType == FileType.BinaryFile
            ? $"{fileContent.BinaryData?.Length ?? 0} bytes"
            : $"{fileContent.TextData?.Length ?? 0} characters";

        fileStatusLabel.Text = $"{fileContent.FileName} ({sizeInfo})";
    }
}