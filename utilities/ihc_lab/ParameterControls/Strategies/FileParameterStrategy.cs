using System;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling file parameters (BinaryFile and TextFile).
/// Creates BinaryFilePicker or TextFilePicker controls based on the type.
/// </summary>
public class FileParameterStrategy : ParameterControlStrategyBase
{
    /// <summary>
    /// Determines if this strategy can handle BinaryFile or TextFile types.
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        return field.IsFile;
    }

    /// <summary>
    /// Creates a BinaryFilePicker or TextFilePicker control based on the field type.
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

        Control filePicker;

        if (typeof(TextFile).IsAssignableFrom(field.Type))
        {
            // Get the static Encoding property from the concrete TextFile type using reflection
            var encodingProperty = field.Type.GetProperty("Encoding", BindingFlags.Public | BindingFlags.Static);
            var encoding = encodingProperty?.GetValue(null) as Encoding;

            if (encoding == null)
                throw new InvalidOperationException(
                    $"Could not lookup Encoding for TextFile type '{field.Type.Name}'");

            // Create TextFilePicker for text files
            filePicker = new TextFilePicker
            {
                Name = controlName,
                TextEncoding = encoding,
                MinWidth = 150
            };
        }
        else if (typeof(BinaryFile).IsAssignableFrom(field.Type))
        {
            // Create BinaryFilePicker for binary files
            filePicker = new BinaryFilePicker
            {
                Name = controlName,
                MinWidth = 150
            };
        }
        else
        {
            throw new NotSupportedException(
                $"File type '{field.Type.FullName}' must implement BinaryFile or TextFile interface");
        }

        // Show the description as a tooltip only when one exists, matching every other control's behaviour.
        ApplyDescriptionTooltip(filePicker, field);

        return filePicker;
    }

    /// <summary>
    /// Subscribes to the file picker's <c>FileChanged</c> event so that a user picking a file is synced to the
    /// service. The pickers expose no bindable value; <c>FileChanged</c> is their only edit signal (raised by
    /// the file-open flow, not by programmatic Data/Filename assignment), which is why this can't be a no-op.
    /// </summary>
    public override void SubscribeToValueChanged(Control control, EventHandler handler)
    {
        switch (control)
        {
            case BinaryFilePicker binaryPicker:
                binaryPicker.FileChanged += (s, e) => handler(binaryPicker, EventArgs.Empty);
                break;
            case TextFilePicker textPicker:
                textPicker.FileChanged += (s, e) => handler(textPicker, EventArgs.Empty);
                break;
        }
    }

    /// <summary>
    /// Extracts the file data from a BinaryFilePicker or TextFilePicker control.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
        // The picker implements BinaryFile/TextFile, so build the concrete parameter type from it via the
        // copy constructor every file model is required to expose (see the BinaryFile/TextFile remarks).
        if (control is BinaryFile binaryFile)
            return ConstructFromFile(field.Type, typeof(BinaryFile), binaryFile);

        if (control is TextFile textFile)
            return ConstructFromFile(field.Type, typeof(TextFile), textFile);

        throw new InvalidOperationException(
            $"Expected BinaryFilePicker or TextFilePicker control but got {control.GetType().Name}");
    }

    /// <summary>
    /// Builds an instance of <paramref name="targetType"/> from a file picker via the copy constructor that
    /// every BinaryFile/TextFile model is required to expose, with a clear error when it is missing rather
    /// than falling through to a misleading "wrong control type" message.
    /// </summary>
    private static object ConstructFromFile(Type targetType, Type fileInterface, object pickerAsFile)
    {
        var constructor = targetType.GetConstructor(new[] { fileInterface })
            ?? throw new InvalidOperationException(
                $"File type '{targetType.Name}' must declare a copy constructor taking {fileInterface.Name} " +
                $"for the Lab to build it from a file picker.");

        try
        {
            return constructor.Invoke(new[] { pickerAsFile });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create instance of {targetType.Name} from {fileInterface.Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets file data into a BinaryFilePicker or TextFilePicker control.
    /// </summary>
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        if (value == null)
            return;

        if (control is BinaryFilePicker binaryFilePicker && value is BinaryFile binaryFile)
        {
            binaryFilePicker.Data = binaryFile.Data;
            binaryFilePicker.Filename = binaryFile.Filename;
        }
        else if (control is TextFilePicker textFilePicker && value is TextFile textFile)
        {
            textFilePicker.Data = textFile.Data;
            textFilePicker.Filename = textFile.Filename;
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot set value of type {value.GetType().Name} into control {control.GetType().Name}");
        }
    }
}
