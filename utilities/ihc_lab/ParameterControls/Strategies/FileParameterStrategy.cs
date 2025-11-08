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
public class FileParameterStrategy : IParameterControlStrategy
{
    /// <summary>
    /// Determines if this strategy can handle BinaryFile or TextFile types.
    /// </summary>
    public bool CanHandle(FieldMetaData field)
    {
        return field.IsFile;
    }

    /// <summary>
    /// Creates a BinaryFilePicker or TextFilePicker control based on the field type.
    /// </summary>
    public ControlCreationResult CreateControl(FieldMetaData field, string controlName)
    {
        if (!CanHandle(field))
            throw new NotSupportedException(
                $"FileParameterStrategy cannot handle type '{field.Type.FullName}'");

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

            ToolTip.SetTip(filePicker, !string.IsNullOrWhiteSpace(field.Description)
                ? field.Description
                : "Upload text file");
        }
        else if (typeof(BinaryFile).IsAssignableFrom(field.Type))
        {
            // Create BinaryFilePicker for binary files
            filePicker = new BinaryFilePicker
            {
                Name = controlName,
                MinWidth = 150
            };

            ToolTip.SetTip(filePicker, !string.IsNullOrWhiteSpace(field.Description)
                ? field.Description
                : "Upload binary file");
        }
        else
        {
            throw new NotSupportedException(
                $"File type '{field.Type.FullName}' must implement BinaryFile or TextFile interface");
        }

        return new ControlCreationResult
        {
            Control = filePicker,
            IsComposite = false
        };
    }

    /// <summary>
    /// Extracts the file data from a BinaryFilePicker or TextFilePicker control.
    /// </summary>
    public object? ExtractValue(Control control, FieldMetaData field)
    {
        if (control is BinaryFile binaryFile)
        {
            // For BinaryFile types, we need to create an instance using the copy constructor
            // The control itself implements BinaryFile, so we can pass it directly
            try
            {
                var constructor = field.Type.GetConstructor(new[] { typeof(BinaryFile) });
                if (constructor != null)
                {
                    return constructor.Invoke(new object[] { binaryFile });
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create instance of {field.Type.Name} from BinaryFile. " +
                    $"Ensure type has a copy constructor: {ex.Message}", ex);
            }
        }

        if (control is TextFile textFile)
        {
            // For TextFile types, we need to create an instance using the copy constructor
            // The control itself implements TextFile, so we can pass it directly
            try
            {
                var constructor = field.Type.GetConstructor(new[] { typeof(TextFile) });
                if (constructor != null)
                {
                    return constructor.Invoke(new object[] { textFile });
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create instance of {field.Type.Name} from TextFile. " +
                    $"Ensure type has a copy constructor: {ex.Message}", ex);
            }
        }

        throw new InvalidOperationException(
            $"Expected BinaryFilePicker or TextFilePicker control but got {control.GetType().Name}");
    }

    /// <summary>
    /// Sets file data into a BinaryFilePicker or TextFilePicker control.
    /// </summary>
    public void SetValue(Control control, object? value, FieldMetaData field)
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
