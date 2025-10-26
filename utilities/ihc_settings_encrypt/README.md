# IHC Settings Encrypt Utility

A command-line utility to encrypt and decrypt passwords in `ihcsettings.json` files using the `SimpleSecret` encryption class.

## Purpose

This utility helps manage sensitive credentials in IHC configuration files by:
- **Encrypting** the `ihcclient.password` field using AES-256-GCM encryption
- **Decrypting** previously encrypted passwords back to plaintext
- Managing the `encryption.isEncrypted` flag automatically

## Prerequisites

1. **Environment Variable**: Set `IHC_ENCRYPT_PASSPHRASE` with your encryption passphrase
   - Minimum length: 12 characters
   - Recommended: 16+ characters with mixed character types
   - Keep this passphrase secure and separate from your settings file
   - **Important**: Use the same passphrase for both encryption and decryption

2. **Valid JSON File**: Your `ihcsettings.json` must contain:
   - `ihcclient.password` field (string)
   - For **decrypt** operation: `encryption.isEncrypted` field must exist and be `true`
   - For **encrypt** operation: If `encryption` section is missing, it will be created automatically

## Usage

### Basic Syntax

```bash
ihc_settings_encrypt <encrypt|decrypt> <path-to-ihcsettings.json>
```

### Encrypt a Password

Encrypts a plaintext password in the settings file.

```bash
# Set the encryption passphrase (must be 12+ characters)
export IHC_ENCRYPT_PASSPHRASE="your-secure-passphrase-here"

# Encrypt the password
dotnet run --project utilities/ihc_settings_encrypt/ihc_settings_encrypt.csproj -- encrypt /path/to/ihcsettings.json
```

**Behavior:**
- Fails if `encryption.isEncrypted` is already `true`
- Creates `encryption` section and `isEncrypted` field if missing
- Encrypts the password and sets `encryption.isEncrypted` to `true`

### Decrypt a Password

Decrypts an encrypted password back to plaintext.

```bash
# Set the same passphrase used for encryption
export IHC_ENCRYPT_PASSPHRASE="your-secure-passphrase-here"

# Decrypt the password
dotnet run --project utilities/ihc_settings_encrypt/ihc_settings_encrypt.csproj -- decrypt /path/to/ihcsettings.json
```

**Behavior:**
- Fails if `encryption.isEncrypted` is `false` or missing
- Decrypts the password and sets `encryption.isEncrypted` to `false`
- Shows warning about plaintext storage

### Alternative: Using Built Executable

```bash
# Build the utility once
dotnet build utilities/ihc_settings_encrypt/ihc_settings_encrypt.csproj

# Use the built executable
export IHC_ENCRYPT_PASSPHRASE="your-secure-passphrase"
dotnet utilities/ihc_settings_encrypt/bin/Debug/net9.0/ihc_settings_encrypt.dll encrypt ihcsettings.json
dotnet utilities/ihc_settings_encrypt/bin/Debug/net9.0/ihc_settings_encrypt.dll decrypt ihcsettings.json
```

## Examples

### Example 1: Encrypting a Password

**Before** (`ihcsettings.json`):
```json
{
  "ihcclient": {
    "endpoint": "http://192.168.1.100",
    "userName": "admin",
    "password": "myPlainPassword123",
    "application": "administrator"
  }
}
```

**Command:**
```bash
$ export IHC_ENCRYPT_PASSPHRASE="my-secure-encryption-passphrase"
$ dotnet run --project utilities/ihc_settings_encrypt/ihc_settings_encrypt.csproj -- encrypt ihcsettings.json

Creating missing 'encryption' section...
Creating missing 'encryption.isEncrypted' field, setting to false...
Success! Password encrypted in: ihcsettings.json
The file has been updated with:
  - ihcclient.password: (encrypted)
  - encryption.isEncrypted: true

Keep your IHC_ENCRYPT_PASSPHRASE environment variable secure!
```

**After** (`ihcsettings.json`):
```json
{
  "encryption": {
    "isEncrypted": true
  },
  "ihcclient": {
    "endpoint": "http://192.168.1.100",
    "userName": "admin",
    "password": "AUk7A8St5R3czttCtvdE2un-WCO0g49_cqeY3I0AAAASpjMw3oKU...",
    "application": "administrator"
  }
}
```

### Example 2: Decrypting a Password

**Before** (encrypted file):
```json
{
  "encryption": {
    "isEncrypted": true
  },
  "ihcclient": {
    "password": "AUk7A8St5R3czttCtvdE2un-WCO0g49_cqeY3I0AAAASpjMw3oKU..."
  }
}
```

**Command:**
```bash
$ export IHC_ENCRYPT_PASSPHRASE="my-secure-encryption-passphrase"
$ dotnet run --project utilities/ihc_settings_encrypt/ihc_settings_encrypt.csproj -- decrypt ihcsettings.json

Success! Password decrypted in: ihcsettings.json
The file has been updated with:
  - ihcclient.password: (plaintext)
  - encryption.isEncrypted: false

⚠️  WARNING: The password is now stored in PLAINTEXT!
```

**After** (decrypted file):
```json
{
  "encryption": {
    "isEncrypted": false
  },
  "ihcclient": {
    "password": "myPlainPassword123"
  }
}
```

## Validation and Error Handling

### Encrypt Operation Validates:
- ✅ File exists and is valid JSON
- ✅ `ihcclient.password` field exists and is not empty
- ✅ `encryption.isEncrypted` is not already `true`
- ✅ Environment variable is set with 12+ character passphrase

### Decrypt Operation Validates:
- ✅ File exists and is valid JSON
- ✅ `encryption` section exists
- ✅ `encryption.isEncrypted` field exists and is `true`
- ✅ Password is properly encrypted (Base64URL format)
- ✅ Environment variable is set with correct passphrase

## Common Error Scenarios

### Trying to Encrypt Already Encrypted Password
```bash
$ dotnet run --project utilities/ihc_settings_encrypt/ihc_settings_encrypt.csproj -- encrypt ihcsettings.json

Error: Password is already encrypted (encryption.isEncrypted = true).
Use 'decrypt' operation first if you want to re-encrypt with a different passphrase.
```

### Trying to Decrypt Plaintext Password
```bash
$ dotnet run --project utilities/ihc_settings_encrypt/ihc_settings_encrypt.csproj -- decrypt ihcsettings.json

Error: Password is not encrypted (encryption.isEncrypted = false).
Cannot decrypt a plaintext password.
```

### Wrong Passphrase for Decryption
```bash
$ export IHC_ENCRYPT_PASSPHRASE="wrong-passphrase"
$ dotnet run --project utilities/ihc_settings_encrypt/ihc_settings_encrypt.csproj -- decrypt ihcsettings.json

Error decrypting password: Decryption failed (wrong passphrase or corrupted data).
This usually means the passphrase is incorrect or the data is corrupted.
```

## Security Notes

⚠️ **Important Security Considerations:**

1. **Passphrase Storage**: Never commit the `IHC_ENCRYPT_PASSPHRASE` to version control
2. **Same Passphrase Required**: You must use the same passphrase for encryption and decryption
3. **Passphrase Management**: Store passphrases in:
   - OS credential stores (Windows Credential Manager, macOS Keychain, Linux Secret Service)
   - Environment variables (for automation)
   - Secret management services (for production)
4. **Different Passphrases**: Use different passphrases for dev/test/production environments
5. **Passphrase Strength**: Longer passphrases are more secure (16+ characters recommended)
6. **Decrypt Warning**: Decrypting puts your password back in plaintext - use only when necessary
7. **File Permissions**: Ensure `ihcsettings.json` has appropriate file permissions

## Workflow Recommendations

### For Development
```bash
# Keep password encrypted in ihcsettings.json
# Set passphrase in your shell profile (.bashrc, .zshrc, etc.)
export IHC_ENCRYPT_PASSPHRASE="dev-passphrase-16chars"
```

### For Production
```bash
# Use environment-specific passphrases
# Consider using secret management services
# Rotate passphrases periodically
```

### Changing Encryption Passphrase
```bash
# 1. Decrypt with old passphrase
export IHC_ENCRYPT_PASSPHRASE="old-passphrase"
dotnet run ... -- decrypt ihcsettings.json

# 2. Re-encrypt with new passphrase
export IHC_ENCRYPT_PASSPHRASE="new-passphrase"
dotnet run ... -- encrypt ihcsettings.json
```

## Automatic Decryption by SDK

The IHC client SDK automatically decrypts passwords when:
- `encryption.isEncrypted` is `true`
- `IHC_ENCRYPT_PASSPHRASE` environment variable is set
- The passphrase matches the one used for encryption

See the `SimpleSecret` class and `IhcSettings.GetFromConfiguration()` for implementation details.

## Exit Codes

- `0` - Success (operation completed)
- `1` - Error (invalid arguments, file issues, wrong passphrase, validation failure, etc.)
