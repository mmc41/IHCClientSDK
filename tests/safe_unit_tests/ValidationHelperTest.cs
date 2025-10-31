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

        [Test]
        public void ValidateObject_WithUsernameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var invalidUser = new IhcUser
            {
                Username = "thisusernameiswaytoolo", // 23 characters, max is 20
                Password = "validpass",
                Group = IhcUserGroup.Users
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => ValidationHelper.ValidateDataAnnotations(invalidUser, nameof(invalidUser)));
            Assert.That(ex.ParamName, Is.EqualTo(nameof(invalidUser)));
            Assert.That(ex.Message, Does.Contain("Validation failed"));
            Assert.That(ex.Message, Does.Contain("Username length can't be more than 20"));
        }
    }
}
