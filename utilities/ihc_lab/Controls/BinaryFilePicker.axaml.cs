using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Ihc;

namespace IhcLab;

public partial class BinaryFilePicker : UserControl, BinaryFile
{
    private TextBlock? fileStatusLabel;

    private byte[]? binaryData;
    private string? fileName;

    /// <summary>
    /// Gets or sets the binary file data
    /// </summary>
    public byte[] Data
    {
        get => binaryData ?? Array.Empty<byte>();
        set => binaryData = value;
    }

    /// <summary>
    /// Gets or sets the binary file name
    /// </summary>
    public string Filename
    {
        get => fileName ?? string.Empty;
        set => fileName = value;
    }

    public BinaryFilePicker()
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
                Title = "Select Binary File to Upload",
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

            // Read file content as binary
            await using var stream = await file.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            binaryData = memoryStream.ToArray();
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

        if (binaryData == null)
        {
            fileStatusLabel.Text = "No file selected";
            return;
        }

        fileStatusLabel.Text = $"{fileName} ({binaryData.Length} bytes)";
    }
}
