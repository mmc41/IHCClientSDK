using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Ihc
{
    /// <summary>
    /// Provides simple, portable password-based encryption for arbitrary UTF-8 strings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Purpose:</b> Encrypt/decrypt small configuration secrets (e.g., device passwords) so they are not stored
    /// as plaintext in JSON or other files. This is a pragmatic improvement over plaintext, not a replacement for
    /// enterprise secret management.
    /// </para>
    /// <para>
    /// <b>Operation:</b> A passphrase supplied via constructor is used to derive a 256-bit key with PBKDF2-HMAC-SHA256
    /// and a random 16-byte salt using 400,000 iterations (see <see cref="Pbkdf2Iterations"/>). Encryption uses AES-256-GCM
    /// with a random 12-byte nonce and produces a 16-byte authentication tag (integrity protection).
    /// </para>
    /// <para>
    /// <b>Output format (Version 1):</b> The encrypted output is a Base64URL-encoded binary blob with the layout:
    /// <code>
    /// [ 1B version | 16B salt | 12B nonce | 4B big-endian ctLen | ct | 16B tag ]
    /// </code>
    /// The resulting string (an "encryptedString") is safe to embed in JSON/YAML/env vars/URLs.
    /// </para>
    /// <para>
    /// <b>Passphrase handling:</b> Keep the passphrase out of source control. Either pass it directly to the
    /// <see cref="SimpleSecret(string, bool)"/> constructor or rely on the parameterless constructor which reads it from the
    /// <c>IHC_ENCRYPT_PASSPHRASE</c> environment variable. Passphrase must be at least 12 characters long.
    /// </para>
    /// <para>
    /// <b>Passphrase best practices:</b>
    /// • Store passphrases in OS credential stores (Windows Credential Manager, macOS Keychain, Linux Secret Service API)
    /// • Use strong, unique passphrases (16+ characters, mix of letters, numbers, symbols)
    /// • Never commit passphrases to version control (add to .gitignore)
    /// • Use different passphrases for dev/test/production environments
    /// • For automated systems, consider using environment variables or secret management services
    /// • Rotate passphrases periodically by re-encrypting with a new passphrase
    /// </para>
    /// <para>
    /// <b>Limits:</b> Intended for small strings. Very large payloads will be fully buffered in memory.
    /// Security degrades if the passphrase is weak or stored alongside the ciphertext.
    /// </para>
    /// </remarks>
    public sealed class SimpleSecret
    {
        // ---- Fixed internal parameters (update carefully; bump Version if format/semantics change) ----
        private const byte Version = 1;                 // format version
        private const int SaltSize = 16;                // 128-bit salt
        private const int NonceSize = 12;               // 96-bit nonce for GCM
        private const int TagSize = 16;                 // 128-bit tag
        private const int KeySize = 32;                 // 256-bit AES key
        private const int Pbkdf2Iterations = 400_000;   // cost factor (increased for enhanced security)
        public const string EncryptPassphaseEnvName = "IHC_ENCRYPT_PASSPHRASE";

        private readonly string _passphrase;

        private readonly bool enable;

        /// <summary>
        /// Initializes the cipher with an explicit passphrase.
        /// </summary>
        /// <param name="passphrase">A secret passphrase. Do not store this in source control. Must be at least 12 characters long.</param>
        /// <param name="enable">Specifies if encryption/decryption is enabled</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="passphrase"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="passphrase"/> is shorter than 12 characters.</exception>
        /// <example>
        /// <code><![CDATA[
        /// var cipher = new SimpleSecret("correct horse battery staple");
        /// var blob = cipher.EncryptString("ihc-password");
        /// var plain = cipher.DecryptString(blob);
        /// ]]></code>
        /// </example>
        public SimpleSecret(string passphrase, bool enable = true)
        {
            _passphrase = passphrase;
            this.enable = enable;

            if (enable)
            {
                if (_passphrase == null)
                    throw new ArgumentNullException(nameof(passphrase));

                if (_passphrase.Length < 12)
                    throw new ArgumentException("Passphrase must be at least 12 characters long for adequate security.", nameof(passphrase));
            }
        }

        /// <summary>
        /// Initializes the cipher using a passphrase read from the <c>IHC_ENCRYPT_PASSPHRASE</c> environment variable.
        /// </summary>
        /// <remarks>
        /// Use this when you prefer configuration via environment (e.g., local dev shells, CI/CD runners).
        /// The passphrase must be at least 12 characters long.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <c>IHC_ENCRYPT_PASSPHRASE</c> environment variable is missing, empty, or shorter than 12 characters.
        /// </exception>
        /// <example>
        /// <code><![CDATA[
        /// // prior to running your app:
        /// //   export IHC_ENCRYPT_PASSPHRASE="correct horse battery staple"
        /// var cipher = new SimpleSecret();
        /// ]]></code>
        /// </example>
        public SimpleSecret(bool enable = true)
        {
            this.enable = enable;
            _passphrase = Environment.GetEnvironmentVariable(EncryptPassphaseEnvName);

            if (enable) {
                if (string.IsNullOrEmpty(_passphrase))
                    throw new InvalidOperationException($"Environment variable {EncryptPassphaseEnvName} is empty/missing.");

                if (_passphrase.Length < 12)
                    throw new InvalidOperationException($"Environment variable {EncryptPassphaseEnvName} must contain at least 12 characters for adequate security.");
            }
        }

        /// <summary>
        /// Encrypts a UTF-8 string and returns a Base64URL-encoded <c>encryptedString</c> suitable for storage.
        /// </summary>
        /// <param name="plaintext">The UTF-8 text to encrypt (e.g., password, token, or small JSON snippet).</param>
        /// <returns>
        /// A Base64URL-encoded versioned blob containing salt, nonce, ciphertext, and tag.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="plaintext"/> is null.</exception>
        public string EncryptString(string plaintext)
        {
            if (!enable)
                return plaintext;

            if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));

            byte[] salt  = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] key   = DeriveKey(_passphrase, salt, Pbkdf2Iterations);

            byte[] pt = Encoding.UTF8.GetBytes(plaintext);
            byte[] ct = new byte[pt.Length];
            byte[] tag = new byte[TagSize];

            try
            {
                using var aesgcm = new AesGcm(key, TagSize);
                aesgcm.Encrypt(nonce, pt, ct, tag); // no AAD for simplicity
            }
            finally
            {
                // Best-effort wipe of sensitive data
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(pt);
            }

            // Build versioned blob: [ver | salt | nonce | ctLen | ct | tag]
            int ctLen = ct.Length;
            int total = 1 + SaltSize + NonceSize + 4 + ctLen + TagSize;
            byte[] blob = new byte[total];

            int idx = 0;
            blob[idx++] = Version;
            Buffer.BlockCopy(salt, 0, blob, idx, SaltSize);  idx += SaltSize;
            Buffer.BlockCopy(nonce, 0, blob, idx, NonceSize); idx += NonceSize;
            BinaryPrimitives.WriteInt32BigEndian(blob.AsSpan(idx, 4), ctLen); idx += 4;
            Buffer.BlockCopy(ct, 0, blob, idx, ctLen);        idx += ctLen;
            Buffer.BlockCopy(tag, 0, blob, idx, TagSize);

            return ToBase64Url(blob);
        }

        /// <summary>
        /// Decrypts a previously returned <paramref name="encryptedString"/> using the constructor-supplied passphrase.
        /// </summary>
        /// <param name="encryptedString">
        /// The Base64URL-encoded, versioned blob produced by <see cref="EncryptString(string)"/>.
        /// It is not raw ciphertext; it contains the necessary parameters (salt, nonce, tag, etc.).
        /// </param>
        /// <returns>The original UTF-8 plaintext string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="encryptedString"/> is null.</exception>
        /// <exception cref="FormatException">Thrown when the input is not a valid encrypted string or the blob format is invalid.</exception>
        /// <exception cref="NotSupportedException">Thrown if the blob version is not supported.</exception>
        /// <exception cref="CryptographicException">Thrown when decryption fails (wrong passphrase or tampered data).</exception>
        public string DecryptString(string encryptedString)
        {
            if (!enable)
                return encryptedString;
                
            if (encryptedString is null) throw new ArgumentNullException(nameof(encryptedString));

            byte[] blob;
            try
            {
                blob = FromBase64Url(encryptedString);
            }
            catch (FormatException ex)
            {
                throw new FormatException(
                    $"The input string is not a valid encrypted string. Expected a Base64URL-encoded blob produced by {nameof(EncryptString)}. " +
                    "If you are trying to decrypt a plaintext string, it must first be encrypted using EncryptString().",
                    ex);
            }

            int min = 1 + SaltSize + NonceSize + 4 + TagSize;
            if (blob.Length < min)
                throw new FormatException("Blob too short.");

            int idx = 0;
            byte ver = blob[idx++];
            if (ver != Version)
                throw new NotSupportedException($"Unsupported blob version {ver}.");

            byte[] salt  = new byte[SaltSize];
            Buffer.BlockCopy(blob, idx, salt, 0, SaltSize);  idx += SaltSize;

            byte[] nonce = new byte[NonceSize];
            Buffer.BlockCopy(blob, idx, nonce, 0, NonceSize); idx += NonceSize;

            int ctLen = BinaryPrimitives.ReadInt32BigEndian(blob.AsSpan(idx, 4)); idx += 4;
            if (ctLen < 0 || blob.Length < idx + ctLen + TagSize)
                throw new FormatException("Invalid ciphertext length.");

            byte[] ct  = new byte[ctLen];
            Buffer.BlockCopy(blob, idx, ct, 0, ctLen); idx += ctLen;

            byte[] tag = new byte[TagSize];
            Buffer.BlockCopy(blob, idx, tag, 0, TagSize);

            byte[] key = DeriveKey(_passphrase, salt, Pbkdf2Iterations);
            byte[] pt  = new byte[ctLen];

            try
            {
                using var aesgcm = new AesGcm(key, TagSize);
                aesgcm.Decrypt(nonce, ct, tag, pt);
                return Encoding.UTF8.GetString(pt);
            }
            catch (CryptographicException ex)
            {
                throw new CryptographicException("Decryption failed (wrong passphrase or corrupted data).", ex);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(pt);
            }
        }

        // ---- Private helpers ----

        private static byte[] DeriveKey(string passphrase, byte[] salt, int iterations)
        {
            using var kdf = new Rfc2898DeriveBytes(passphrase, salt, iterations, HashAlgorithmName.SHA256);
            return kdf.GetBytes(KeySize);
        }

        private static string ToBase64Url(byte[] data)
        {
            string b64 = Convert.ToBase64String(data);
            return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static byte[] FromBase64Url(string s)
        {
            string b64 = s.Replace('-', '+').Replace('_', '/');
            switch (b64.Length % 4)
            {
                case 2: b64 += "=="; break;
                case 3: b64 += "="; break;
            }
            return Convert.FromBase64String(b64);
        }
    }
}
