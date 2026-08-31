using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ihc;

namespace Ihc.Utility;

class Program
{
    static int Main(string[] args)
    {
        try
        {
            // Validate command line arguments
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: ihc_settings_encrypt <encrypt|decrypt> <path-to-ihcsettings.json>");
                Console.WriteLine();
                Console.WriteLine("This utility encrypts or decrypts the password in ihcsettings.json file.");
                Console.WriteLine();
                Console.WriteLine("Operations:");
                Console.WriteLine("  encrypt  - Encrypts the password and sets encryption.isEncrypted to true");
                Console.WriteLine("  decrypt  - Decrypts the password and sets encryption.isEncrypted to false");
                Console.WriteLine();
                Console.WriteLine("Requirements:");
                Console.WriteLine("  - Environment variable IHC_ENCRYPT_PASSPHRASE must be set");
                Console.WriteLine("  - The passphrase must be at least 12 characters long");
                Console.WriteLine();
                return 1;
            }

            string operation = args[0].ToLowerInvariant();
            string filePath = args[1];

            if (operation != "encrypt" && operation != "decrypt")
            {
                Console.Error.WriteLine($"Error: Invalid operation '{args[0]}'. Must be 'encrypt' or 'decrypt'.");
                return 1;
            }

            // Verify file exists
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"Error: File not found: {filePath}");
                return 1;
            }

            // Read file content
            string jsonContent = File.ReadAllText(filePath);

            // Parse JSON and validate structure
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(jsonContent);
                if (root == null)
                {
                    Console.Error.WriteLine("Error: Failed to parse JSON file (result was null)");
                    return 1;
                }
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Error: Invalid JSON format: {ex.Message}");
                return 1;
            }

            // Check if ihcclient section exists
            var ihcclientNode = root["ihcclient"];
            if (ihcclientNode == null)
            {
                Console.Error.WriteLine("Error: 'ihcclient' section not found in JSON file");
                return 1;
            }

            // Handle encryption section based on operation
            var encryptionNode = root["encryption"];
            bool isEncrypted = false;

            if (operation == "encrypt")
            {
                // For encrypt operation: create encryption section if missing
                if (encryptionNode == null)
                {
                    Console.WriteLine("Creating missing 'encryption' section...");
                    encryptionNode = new JsonObject();
                    root["encryption"] = encryptionNode;
                }

                var isEncryptedNode = encryptionNode["isEncrypted"];
                if (isEncryptedNode == null)
                {
                    Console.WriteLine("Creating missing 'encryption.isEncrypted' field, setting to false...");
                    encryptionNode["isEncrypted"] = false;
                    isEncrypted = false;
                }
                else
                {
                    isEncrypted = isEncryptedNode.GetValue<bool>();
                }

                if (isEncrypted)
                {
                    Console.Error.WriteLine("Error: Password is already encrypted (encryption.isEncrypted = true).");
                    Console.Error.WriteLine("Use 'decrypt' operation first if you want to re-encrypt with a different passphrase.");
                    return 1;
                }
            }
            else // decrypt
            {
                // For decrypt operation: encryption section must exist
                if (encryptionNode == null)
                {
                    Console.Error.WriteLine("Error: 'encryption' section not found in JSON file.");
                    Console.Error.WriteLine("Cannot decrypt - file was not encrypted with this utility.");
                    return 1;
                }

                var isEncryptedNode = encryptionNode["isEncrypted"];
                if (isEncryptedNode == null)
                {
                    Console.Error.WriteLine("Error: 'encryption.isEncrypted' field not found in JSON file.");
                    Console.Error.WriteLine("Cannot decrypt - file was not encrypted with this utility.");
                    return 1;
                }

                isEncrypted = isEncryptedNode.GetValue<bool>();

                if (!isEncrypted)
                {
                    Console.Error.WriteLine("Error: Password is not encrypted (encryption.isEncrypted = false).");
                    Console.Error.WriteLine("Cannot decrypt a plaintext password.");
                    return 1;
                }
            }

            // Get the password field
            var passwordNode = ihcclientNode["password"];
            if (passwordNode == null)
            {
                Console.Error.WriteLine("Error: 'ihcclient.password' field not found in JSON file");
                return 1;
            }

            string? password = passwordNode.GetValue<string>();
            if (string.IsNullOrEmpty(password))
            {
                Console.Error.WriteLine("Error: 'ihcclient.password' is empty or null");
                return 1;
            }

            // Initialize SimpleSecret cipher
            SimpleSecret cipher;
            try
            {
                cipher = new SimpleSecret(); // Uses IHC_ENCRYPT_PASSPHRASE environment variable
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.Error.WriteLine($"Please set the {SimpleSecret.EncryptPassphaseEnvName} environment variable.");
                return 1;
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }

            // Perform the requested operation
            string resultPassword;
            if (operation == "encrypt")
            {
                try
                {
                    resultPassword = cipher.EncryptString(password);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error encrypting password: {ex.Message}");
                    return 1;
                }

                // Update the JSON
                ihcclientNode["password"] = resultPassword;
                encryptionNode["isEncrypted"] = true;

                WriteSettingsFile(root, filePath);

                Console.WriteLine($"Success! Password encrypted in: {filePath}");
                Console.WriteLine($"The file has been updated with:");
                Console.WriteLine($"  - ihcclient.password: (encrypted)");
                Console.WriteLine($"  - encryption.isEncrypted: true");
                Console.WriteLine();
                Console.WriteLine($"Keep your {SimpleSecret.EncryptPassphaseEnvName} environment variable secure!");
            }
            else // decrypt
            {
                try
                {
                    resultPassword = cipher.DecryptString(password);
                }
                catch (System.Security.Cryptography.CryptographicException ex)
                {
                    Console.Error.WriteLine($"Error decrypting password: {ex.Message}");
                    Console.Error.WriteLine("This usually means the passphrase is incorrect or the data is corrupted.");
                    return 1;
                }
                catch (FormatException ex)
                {
                    Console.Error.WriteLine($"Error decrypting password: {ex.Message}");
                    return 1;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error decrypting password: {ex.Message}");
                    return 1;
                }

                // Update the JSON
                ihcclientNode["password"] = resultPassword;
                encryptionNode["isEncrypted"] = false;

                WriteSettingsFile(root, filePath);

                Console.WriteLine($"Success! Password decrypted in: {filePath}");
                Console.WriteLine($"The file has been updated with:");
                Console.WriteLine($"  - ihcclient.password: (plaintext)");
                Console.WriteLine($"  - encryption.isEncrypted: false");
                Console.WriteLine();
                Console.WriteLine("⚠️  WARNING: The password is now stored in PLAINTEXT!");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    /// <summary>
    /// Writes <paramref name="root"/> back over <paramref name="filePath"/>, formatted the one way both the
    /// encrypt and the decrypt path need.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The write goes through a temporary file in the SAME directory and is then swapped into place, because
    /// File.WriteAllText truncates before it writes: a failure part-way through would leave this file holding
    /// neither the old password nor the new one, and an unparseable settings file is one a host cannot start
    /// from. Same directory so the swap stays on one volume.
    /// </para>
    /// <para>
    /// The swap must not hand the settings file weaker protection than it had. A newly created file gets
    /// whatever the DIRECTORY grants — 0644 under a common umask, only the inherited ACEs on Windows — so
    /// renaming one of those over a deliberately locked-down credentials file would publish the password
    /// while appearing to do nothing but reformat it. The temporary is therefore created carrying the
    /// settings file's own Unix mode, which the rename takes with it, and swapped in with File.Replace,
    /// which keeps the replaced file's ACLs on Windows.
    /// </para>
    /// <para>
    /// On the decrypt path the temporary holds the plaintext password. It is deleted when the write or the
    /// swap fails, but nothing can delete it if the process is killed in between, so it is created with the
    /// settings file's protection rather than the directory's, and .gitignore covers its name.
    /// </para>
    /// </remarks>
    internal static void WriteSettingsFile(JsonNode root, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        string tempPath = filePath + ".tmp";
        try
        {
            WriteProtectedAs(tempPath, root.ToJsonString(options), filePath);
            File.Replace(tempPath, filePath, destinationBackupFileName: null);
        }
        catch
        {
            // Best effort: the original file is still intact, and there is nowhere better to report a
            // failure to clean up than the exception already on its way out.
            try { File.Delete(tempPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    /// <summary>
    /// Creates <paramref name="path"/> holding <paramref name="content"/>, protected as
    /// <paramref name="protectedAs"/> is protected.
    /// </summary>
    /// <remarks>
    /// The mode is given at creation rather than applied afterwards, because a chmod that follows the open
    /// leaves a window in which the password is readable by whoever the directory's default mode allows. A
    /// stale file left by an earlier kill is deleted rather than truncated, since a truncating open would
    /// keep the mode that file already had — the one thing this must not inherit.
    /// </remarks>
    private static void WriteProtectedAs(string path, string content, string protectedAs)
    {
        var fileOptions = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
        {
            fileOptions.UnixCreateMode = File.GetUnixFileMode(protectedAs);
        }

        File.Delete(path);
        using var stream = new FileStream(path, fileOptions);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }
}
