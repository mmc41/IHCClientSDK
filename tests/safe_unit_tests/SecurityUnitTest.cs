using System;
using NUnit.Framework;
using Ihc;

namespace Ihc.Tests
{
    /// <summary>
    /// Security related unit tests
    /// </summary>
    [TestFixture]
    public class SecurityUnitTest
    {
        [Test]
        public void IhcUser_ToStringWithFalse_RedactsPassword()
        {
            // Arrange
            var user = new IhcUser
            {
                Username = "testuser",
                Password = "secretpassword",
                Email = "test@example.com",
                Firstname = "John",
                Lastname = "Doe",
                Phone = "123-456-7890",
                Group = IhcUserGroup.Administrators,
                Project = "testproject",
                CreatedDate = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
                LoginDate = DateTimeOffset.Parse("2024-01-02T00:00:00Z")
            };

            // Act
            string result = user.ToString(false);
            
            // Assert
            Assert.That(result, Does.Contain($"Username={user.Username}"));
            Assert.That(result, Does.Contain($"Password={UserConstants.REDACTED_PASSWORD}"));
            Assert.That(result, Does.Not.Contain("secretpassword"));
            Assert.That(result, Does.Contain($"Email={user.Email}"));
        }

        [Test]
        public void IhcUser_ToStringWithTrue_DoesNotRedactPassword()
        {
            // Arrange
            var user = new IhcUser
            {
                Username = "testuser",
                Password = "secretpassword",
                Email = "test@example.com",
                Firstname = "John",
                Lastname = "Doe",
                Phone = "123-456-7890",
                Group = IhcUserGroup.Administrators,
                Project = "testproject",
                CreatedDate = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
                LoginDate = DateTimeOffset.Parse("2024-01-02T00:00:00Z")
            };

            // Act
            string result = user.ToString(true);

            // Assert
            Assert.That(result, Does.Contain($"Username={user.Username}"));
            Assert.That(result, Does.Contain("Password=secretpassword"));
            Assert.That(result, Does.Not.Contain(UserConstants.REDACTED_PASSWORD));
            Assert.That(result, Does.Contain($"Email={user.Email}"));
        }

        [Test]
        public void IhcUser_RedactPassword_CreatesNewInstanceWithRedactedPassword()
        {
            // Arrange
            var user = new IhcUser
            {
                Username = "testuser",
                Password = "secretpassword",
                Email = "test@example.com",
                Firstname = "John",
                Lastname = "Doe",
                Phone = "123-456-7890",
                Group = IhcUserGroup.Users,
                Project = "testproject",
                CreatedDate = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
                LoginDate = DateTimeOffset.Parse("2024-01-02T00:00:00Z")
            };

            // Act
            var redactedUser = user.RedactPassword();

            // Assert
            Assert.That(redactedUser.Password, Is.EqualTo(UserConstants.REDACTED_PASSWORD));
            Assert.That(redactedUser.Username, Is.EqualTo(user.Username));
            Assert.That(redactedUser.Email, Is.EqualTo(user.Email));
            Assert.That(user.Password, Is.EqualTo("secretpassword")); // Original should be unchanged
        }

        /// <summary>
        /// Parameterized test for password redaction with various XML password element formats.
        /// </summary>
        [TestCase("<password>mysecretpassword</password>",
                  "<password>" + UserConstants.REDACTED_PASSWORD + "</password>",
                  TestName = "RedactPassword_WithSimplePasswordElement")]
        [TestCase("<ns1:password>mysecretpassword</ns1:password>",
                  "<ns1:password>" + UserConstants.REDACTED_PASSWORD + "</ns1:password>",
                  TestName = "RedactPassword_WithNamespacedPasswordElement")]
        [TestCase("<ns1:password xsi:type=\"xsd:string\">mypassword</ns1:password>",
                  "<ns1:password xsi:type=\"xsd:string\">" + UserConstants.REDACTED_PASSWORD + "</ns1:password>",
                  TestName = "RedactPassword_WithAttributesInPasswordElement")]
        [TestCase("<root><utcs:password>mypassword</utcs:password></root>",
                  "<root><utcs:password>" + UserConstants.REDACTED_PASSWORD + "</utcs:password></root>",
                  TestName = "RedactPassword_WithUtcsNamespacedPasswordElement")]
        [TestCase("<Password>mysecret</Password>",
                  "<Password>" + UserConstants.REDACTED_PASSWORD + "</Password>",
                  TestName = "RedactPassword_WithCaseInsensitivePasswordTag")]
        [TestCase("<PASSWORD>mysecret</PASSWORD>",
                  "<PASSWORD>" + UserConstants.REDACTED_PASSWORD + "</PASSWORD>",
                  TestName = "RedactPassword_WithUpperCasePasswordTag")]
        [TestCase("<ns1:password xsi:type=\"xsd:string\" xsi:nil=\"false\" foo=\"bar\">complexpassword</ns1:password>",
                  "<ns1:password xsi:type=\"xsd:string\" xsi:nil=\"false\" foo=\"bar\">" + UserConstants.REDACTED_PASSWORD + "</ns1:password>",
                  TestName = "RedactPassword_WithMultipleAttributesInPasswordElement")]
        public void RedactPassword_WithVariousPasswordElements_RedactsPassword(string input, string expected)
        {
            // Act
            string result = SecurityHelper.RedactPassword(input);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        #region SimpleSecret Tests

        [Test]
        public void SimpleSecret_EncryptString_ProducesDifferentCiphertext_ForSamePlaintext()
        {
            // Arrange
            string passphrase = "correct horse battery staple";
            string plaintext = "my-secret-password";
            var cipher = new SimpleSecret(passphrase);

            // Act
            string encrypted1 = cipher.EncryptString(plaintext);
            string encrypted2 = cipher.EncryptString(plaintext);

            // Assert - Due to random salt and nonce, ciphertexts should differ
            Assert.That(encrypted1, Is.Not.EqualTo(encrypted2));
            Assert.That(cipher.DecryptString(encrypted1), Is.EqualTo(plaintext));
            Assert.That(cipher.DecryptString(encrypted2), Is.EqualTo(plaintext));
        }

        [Test]
        public void SimpleSecret_EncryptString_ProducesDifferentCiphertext_ForDifferentPlaintext()
        {
            // Arrange
            string passphrase = "correct horse battery staple";
            var cipher = new SimpleSecret(passphrase);

            // Act
            string encrypted1 = cipher.EncryptString("password1");
            string encrypted2 = cipher.EncryptString("password2");

            // Assert
            Assert.That(encrypted1, Is.Not.EqualTo(encrypted2));
        }

        [TestCase("12characters")]  // Exactly 12 characters - should pass
        [TestCase("correct horse battery staple")]  // Much longer - should pass
        [TestCase("MyP@ssw0rd!2")]  // Exactly 12 with special chars - should pass
        public void SimpleSecret_Constructor_AcceptsValidPassphrase(string passphrase)
        {
            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => new SimpleSecret(passphrase));
        }

        [TestCase("")]
        [TestCase("a")]
        [TestCase("11charonly!")]  // 11 characters
        public void SimpleSecret_Constructor_RejectsShortPassphrase(string passphrase)
        {
            // Act & Assert - the message must name the boundary, so the rejection stays diagnosable
            // rather than surfacing as a bare "invalid argument".
            var ex = Assert.Throws<ArgumentException>(() => new SimpleSecret(passphrase));
            Assert.That(ex!.Message, Does.Contain("at least 12 characters"));
        }

        [Test]
        public void SimpleSecret_DecryptString_ThrowsCryptographicException_WhenWrongPassphrase()
        {
            // Arrange
            string correctPassphrase = "correct horse battery staple";
            string wrongPassphrase = "wrong horse battery staple!!";
            string plaintext = "my-secret-password";

            var cipher1 = new SimpleSecret(correctPassphrase);
            var cipher2 = new SimpleSecret(wrongPassphrase);

            // Act
            string encrypted = cipher1.EncryptString(plaintext);

            // Assert
            var ex = Assert.Throws<System.Security.Cryptography.CryptographicException>(
                () => cipher2.DecryptString(encrypted));
            Assert.That(ex!.Message, Does.Contain("wrong passphrase"));
        }

        [Test]
        public void SimpleSecret_DecryptString_ThrowsFormatException_WhenBlobTooShort()
        {
            // Arrange
            var cipher = new SimpleSecret("correct horse battery staple");
            string invalidBlob = "dG9vc2hvcnQ"; // "tooshort" in Base64

            // Act & Assert
            var ex = Assert.Throws<FormatException>(() => cipher.DecryptString(invalidBlob));
            Assert.That(ex!.Message, Does.Contain("too short"));
        }

        [Test]
        public void SimpleSecret_DecryptString_ThrowsCryptographicException_WhenBlobTampered()
        {
            // Arrange
            string passphrase = "correct horse battery staple";
            string plaintext = "my-secret-password";
            var cipher = new SimpleSecret(passphrase);
            string encrypted = cipher.EncryptString(plaintext);

            // Tamper with the encrypted string (modify a character in the middle)
            char[] tamperedChars = encrypted.ToCharArray();
            tamperedChars[encrypted.Length / 2] = tamperedChars[encrypted.Length / 2] == 'A' ? 'B' : 'A';
            string tamperedEncrypted = new string(tamperedChars);

            // Act & Assert
            Assert.Throws<System.Security.Cryptography.CryptographicException>(
                () => cipher.DecryptString(tamperedEncrypted));
        }

        [Test]
        public void SimpleSecret_EncryptedString_IsBase64UrlSafe()
        {
            // Arrange
            string passphrase = "correct horse battery staple";
            string plaintext = "my-secret-password-that-might-produce-special-chars";
            var cipher = new SimpleSecret(passphrase);

            // Act
            string encrypted = cipher.EncryptString(plaintext);

            // Assert - Base64URL should not contain +, /, or =
            Assert.That(encrypted, Does.Not.Contain("+"));
            Assert.That(encrypted, Does.Not.Contain("/"));
            Assert.That(encrypted, Does.Not.Contain("="));
        }

        [Test]
        public void SimpleSecret_EncryptDecrypt_HandlesLongStrings()
        {
            // Arrange
            string passphrase = "correct horse battery staple";
            string plaintext = new string('A', 10000); // 10KB string
            var cipher = new SimpleSecret(passphrase);

            // Act
            string encrypted = cipher.EncryptString(plaintext);
            string decrypted = cipher.DecryptString(encrypted);

            // Assert
            Assert.That(decrypted, Is.EqualTo(plaintext));
            Assert.That(decrypted.Length, Is.EqualTo(10000));
        }

        // The three unusable states of the passphrase environment variable. Each row keeps its own
        // message fragment, so a state that starts reporting another state's diagnostic still fails.
        [TestCase(null, SimpleSecret.EncryptPassphaseEnvName, TestName = "SimpleSecret_EnvironmentConstructor_ThrowsInvalidOperationException_WhenEnvVarMissing")]
        [TestCase("", "empty", TestName = "SimpleSecret_EnvironmentConstructor_ThrowsInvalidOperationException_WhenEnvVarEmpty")]
        [TestCase("short", "at least 12 characters", TestName = "SimpleSecret_EnvironmentConstructor_ThrowsInvalidOperationException_WhenEnvVarTooShort")]
        public void SimpleSecret_EnvironmentConstructor_ThrowsInvalidOperationException_WhenEnvVarUnusable(
            string? envValue, string expectedMessageFragment)
        {
            // Arrange
            string originalValue = Environment.GetEnvironmentVariable(SimpleSecret.EncryptPassphaseEnvName) ?? string.Empty;
            try
            {
                Environment.SetEnvironmentVariable(SimpleSecret.EncryptPassphaseEnvName, envValue);

                // Act & Assert
                var ex = Assert.Throws<InvalidOperationException>(() => new SimpleSecret());
                Assert.That(ex!.Message, Does.Contain(expectedMessageFragment));
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(SimpleSecret.EncryptPassphaseEnvName,
                    string.IsNullOrEmpty(originalValue) ? null : originalValue);
            }
        }

        [Test]
        public void SimpleSecret_EnvironmentConstructor_Success_WhenEnvVarValid()
        {
            // Arrange
            string originalValue = Environment.GetEnvironmentVariable(SimpleSecret.EncryptPassphaseEnvName) ?? string.Empty;
            string testPassphrase = "valid passphrase for testing";
            try
            {
                Environment.SetEnvironmentVariable(SimpleSecret.EncryptPassphaseEnvName, testPassphrase);

                // Act
                var cipher = new SimpleSecret();
                string encrypted = cipher.EncryptString("test");
                string decrypted = cipher.DecryptString(encrypted);

                // Assert
                Assert.That(decrypted, Is.EqualTo("test"));
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(SimpleSecret.EncryptPassphaseEnvName,
                    string.IsNullOrEmpty(originalValue) ? null : originalValue);
            }
        }

        [Test]
        public void SimpleSecret_MultipleInstances_WithSamePassphrase_CanDecryptEachOther()
        {
            // Arrange
            string passphrase = "correct horse battery staple";
            string plaintext = "shared-secret";
            var cipher1 = new SimpleSecret(passphrase);
            var cipher2 = new SimpleSecret(passphrase);

            // Act
            string encrypted = cipher1.EncryptString(plaintext);
            string decrypted = cipher2.DecryptString(encrypted);

            // Assert
            Assert.That(decrypted, Is.EqualTo(plaintext));
        }

        [Test]
        public void SimpleSecret_DecryptString_ThrowsClearException_WhenInputIsPlaintext()
        {
            // Arrange
            string passphrase = "correct horse battery staple";
            var cipher = new SimpleSecret(passphrase);
            string plaintextString = "this-is-not-encrypted";

            // Act & Assert
            var ex = Assert.Throws<FormatException>(() => cipher.DecryptString(plaintextString));
            Assert.That(ex!.Message, Does.Contain("not a valid encrypted string"));
            Assert.That(ex.Message, Does.Contain("Base64URL-encoded blob"));
            Assert.That(ex.Message, Does.Contain("EncryptString"));
        }

        [Test]
        public void SimpleSecret_DecryptString_ThrowsClearException_WhenInputIsInvalidBase64()
        {
            // Arrange
            string passphrase = "correct horse battery staple";
            var cipher = new SimpleSecret(passphrase);
            string invalidInput = "Hello World! This has spaces and punctuation!";

            // Act & Assert
            var ex = Assert.Throws<FormatException>(() => cipher.DecryptString(invalidInput));
            Assert.That(ex!.Message, Does.Contain("not a valid encrypted string"));
        }

        #endregion
    }
}
