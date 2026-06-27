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
    /// Raised after a file has been picked and its name/data captured, giving the Lab's parameter sync the
    /// only signal it can use to push the picked file to the service. Programmatic <see cref="Data"/>/
    /// <see cref="Filename"/> assignment (e.g. the service-&gt;GUI restore path) is intentionally silent and
    /// does NOT raise this event.
    /// </summary>
    public event EventHandler? FileChanged;

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

    /// <summary>
    /// Applies a picked file (name + binary content) and raises <see cref="FileChanged"/>. This is the single
    /// entry point the file-open flow uses; setting the data this way - rather than via the <see cref="Data"/>/
    /// <see cref="Filename"/> setters - is what notifies listeners that the user picked a new file.
    /// </summary>
    public void ApplyPickedFile(string filename, byte[] data)
    {
        fileName = filename;
        binaryData = data;
        UpdateStatusLabel();
        FileChanged?.Invoke(this, EventArgs.Empty);
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

            ApplyPickedFile(file.Name, memoryStream.ToArray());
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
