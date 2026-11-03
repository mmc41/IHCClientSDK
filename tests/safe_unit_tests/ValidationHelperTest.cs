using NUnit.Framework;
using System;
using System.ComponentModel.DataAnnotations;
using Ihc;

namespace Ihc.Tests
{
    /// <summary>
    /// Unit tests for ValidationHelper that verify data annotation validation behavior.
    /// </summary>
    [TestFixture]
    public class ValidationHelperTest
    {
        [Test]
        public void ValidateObject_WithNullObject_ThrowsArgumentException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => ValidationHelper.ValidateDataAnnotations(null, "testParam"));
            Assert.That(ex.ParamName, Is.EqualTo("testParam"));
            Assert.That(ex.Message, Does.Contain("Parameter must be provided"));
        }

        [Test]
        public void ValidateObject_WithValidIhcUser_DoesNotThrow()
        {
            // Arrange
            var validUser = new IhcUser
            {
                Username = "validuser",
                Password = "validpass",
                Email = "test@test.com",
                Firstname = "John",
                Lastname = "Doe",
                Phone = "123456789",
                Group = IhcUserGroup.Users
            };

            // Act & Assert
            Assert.DoesNotThrow(() => ValidationHelper.ValidateDataAnnotations(validUser, nameof(validUser)));
        }

        /// <summary>IHC Administrator's user dialog fields are display widths, not limits, and its reference records no
        /// maximum for a user name or password, so longer values pass the SDK's validation.</summary>
        [Test]
        public void ValidateObject_WithStringsLongerThanTheVendorsFieldWidths_DoesNotThrow()
        {
            var user = new IhcUser
            {
                Username = "thisusernameiswaytoolo", // 22 characters, past the dialog's 20 columns
                Password = "a password of more than twenty characters",
                Email = "a.rather.long.address@installer.example",
                Firstname = "Anne-Marie Louise",
                Lastname = "test lastname edited",
                Phone = "+45 12 34 56 78 90 12",
                Group = IhcUserGroup.Users
            };

            Assert.DoesNotThrow(() => ValidationHelper.ValidateDataAnnotations(user, nameof(user)));
        }

        /// <summary>
        /// An empty or blank password is refused by SDK policy, not by any known controller minimum: IHC Administrator
        /// lets an empty password through, and whether a controller stores one is unknown.
        /// </summary>
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void ValidateObject_WithAnEmptyPassword_ThrowsArgumentException(string? password)
        {
            var user = new IhcUser { Username = "user", Password = password, Group = IhcUserGroup.Users };

            var ex = Assert.Throws<ArgumentException>(() => ValidationHelper.ValidateDataAnnotations(user, nameof(user)));
            Assert.That(ex.Message, Does.Contain("Password is required"));
        }
    }
}
