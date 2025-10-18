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
            var redactedUser = user.RedactPasword();

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
    }
}
