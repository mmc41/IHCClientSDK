using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ihc;
using Ihc.App;
using FakeItEasy;

namespace Ihc.Tests
{
    /// <summary>
    /// Unit tests for LabAppService that verify service/operation selection, argument handling,
    /// and dynamic operation invocation using FakeItEasy mocked services (no actual controller connection).
    /// </summary>
    [TestFixture]
    public class LabAppServiceTests
    {
        #pragma warning disable NUnit1032 // Fakes from FakeItEasy don't need disposal
        private IAuthenticationService fakeAuthService;
        private IResourceInteractionService fakeResourceService;
        private IConfigurationService fakeConfigService;
        #pragma warning restore NUnit1032
        private IhcSettings settings;

        /// <summary>
        /// Sets up test fixtures before each test.
        /// Creates fake IHC service instances with pre-configured responses for common operations.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            // Create fake services
            fakeAuthService = A.Fake<IAuthenticationService>();
            fakeResourceService = A.Fake<IResourceInteractionService>();
            fakeConfigService = A.Fake<IConfigurationService>();

            // Configure fake auth service methods
            A.CallTo(() => fakeAuthService.IsAuthenticated()).Returns(Task.FromResult(true));
            var fakeUser = new IhcUser { Username = "testuser", Group = IhcUserGroup.Users };
            A.CallTo(() => fakeAuthService.Authenticate()).Returns(Task.FromResult(fakeUser));

            // Configure fake resource service methods
            var fakeResourceValue = new ResourceValue
            {
                ResourceID = 123456,
                Value = new ResourceValue.UnionValue
                {
                    ValueKind = ResourceValue.ValueKind.BOOL,
                    BoolValue = true
                },
                IsValueRuntime = true
            };
            A.CallTo(() => fakeResourceService.GetRuntimeValue(A<int>._)).Returns(Task.FromResult(fakeResourceValue));

            // Configure fake config service methods
            A.CallTo(() => fakeConfigService.GetSystemInfo()).Returns(Task.FromResult(new SystemInfo
            {
                Version = "3.0",
                Brand = "LK"
            }));

            // Create test settings
            settings = new IhcSettings
            {
                Endpoint = "http://test",
                UserName = "testuser",
                Password = "testpass",
                Application = Application.administrator,
                LogSensitiveData = false,
                AsyncContinueOnCapturedContext = false
            };
        }

        #region Constructor Tests

        /// <summary>
        /// Verifies that the constructor accepts null service filter and defaults to accepting all services.
        /// </summary>
        [Test]
        public void Constructor_WithNullServiceFilter_UsesTrueFilter()
        {
            // Act
            var service = new LabAppService(null, null);

            // Assert
            Assert.That(service, Is.Not.Null);
            Assert.That(service.Services, Is.Not.Null);
            Assert.That(service.Services, Is.Empty);
        }

        /// <summary>
        /// Verifies that the constructor accepts custom service and operation filters.
        /// </summary>
        [Test]
        public void Constructor_WithFilters_CreatesInstance()
        {
            // Arrange
            Func<IIHCApiService, bool> serviceFilter = s => s is IAuthenticationService;
            Func<ServiceOperationMetadata, bool> operationFilter = o => o.Name.Contains("Auth");

            // Act
            var service = new LabAppService(serviceFilter, operationFilter);

            // Assert
            Assert.That(service, Is.Not.Null);
            Assert.That(service.Services, Is.Empty);
        }

        #endregion

        #region Configure Tests

        /// <summary>
        /// Verifies that Configure() accepts an empty services array and initializes Services property.
        /// </summary>
        [Test]
        public void Configure_WithEmptyServices_SetsEmptyArray()
        {
            // Arrange
            var labService = new LabAppService(null, null);

            // Act
            labService.Configure(settings, Array.Empty<IIHCApiService>());

            // Assert
            Assert.That(labService.Services, Is.Not.Null);
            Assert.That(labService.Services, Is.Empty);
        }

        /// <summary>
        /// Verifies that Configure() creates ServiceItem instances for all provided services.
        /// </summary>
        [Test]
        public void Configure_WithServices_CreatesServiceItems()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            var services = new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService };

            // Act
            labService.Configure(settings, services);

            // Assert
            Assert.That(labService.Services, Is.Not.Null);
            Assert.That(labService.Services.Length, Is.EqualTo(3));
            Assert.That(labService.Services[0].Service, Is.EqualTo(fakeAuthService));
            Assert.That(labService.Services[1].Service, Is.EqualTo(fakeResourceService));
            Assert.That(labService.Services[2].Service, Is.EqualTo(fakeConfigService));
        }

        /// <summary>
        /// Verifies that Configure() applies the service filter to exclude services.
        /// </summary>
        [Test]
        public void Configure_WithServiceFilter_FiltersServices()
        {
            // Arrange
            Func<IIHCApiService, bool> serviceFilter = s => s is IAuthenticationService;
            var labService = new LabAppService(serviceFilter, null);
            var services = new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService };

            // Act
            labService.Configure(settings, services);

            // Assert
            Assert.That(labService.Services.Length, Is.EqualTo(1));
            Assert.That(labService.Services[0].Service, Is.EqualTo(fakeAuthService));
        }

        /// <summary>
        /// Verifies that Configure() resets SelectedServiceIndex to 0 when called multiple times.
        /// </summary>
        [Test]
        public void Configure_ResetsSelection_ToFirstService()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            var services = new IIHCApiService[] { fakeAuthService, fakeResourceService };
            labService.Configure(settings, services);
            labService.SelectedServiceIndex = 1;

            // Act - reconfigure
            labService.Configure(settings, services);

            // Assert
            Assert.That(labService.SelectedServiceIndex, Is.EqualTo(0));
        }

        #endregion

        #region Selection Property Tests

        /// <summary>
        /// Verifies that SelectedServiceIndex getter returns the current service index.
        /// </summary>
        [Test]
        public void SelectedServiceIndex_Get_ReturnsCorrectValue()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            var services = new IIHCApiService[] { fakeAuthService, fakeResourceService };
            labService.Configure(settings, services);

            // Act
            var index = labService.SelectedServiceIndex;

            // Assert
            Assert.That(index, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies that SelectedServiceIndex setter updates the selected service index.
        /// </summary>
        [Test]
        public void SelectedServiceIndex_Set_UpdatesValue()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            var services = new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService };
            labService.Configure(settings, services);

            // Act
            labService.SelectedServiceIndex = 2;

            // Assert
            Assert.That(labService.SelectedServiceIndex, Is.EqualTo(2));
        }

        /// <summary>
        /// Verifies that setting SelectedServiceIndex to a negative value throws ArgumentOutOfRangeException.
        /// </summary>
        [Test]
        public void SelectedServiceIndex_SetNegative_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => labService.SelectedServiceIndex = -1);
        }

        /// <summary>
        /// Verifies that setting SelectedServiceIndex beyond the array bounds throws ArgumentOutOfRangeException.
        /// </summary>
        [Test]
        public void SelectedServiceIndex_SetTooLarge_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => labService.SelectedServiceIndex = 5);
        }

        /// <summary>
        /// Verifies that setting SelectedServiceIndex before calling Configure() throws InvalidOperationException.
        /// </summary>
        [Test]
        public void SelectedServiceIndex_SetBeforeConfigure_ThrowsInvalidOperationException()
        {
            // Arrange
            var labService = new LabAppService(null, null);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => labService.SelectedServiceIndex = 0);
        }

        /// <summary>
        /// Verifies that SelectedOperationIndex getter returns the current operation index for the selected service.
        /// </summary>
        [Test]
        public void SelectedOperationIndex_Get_ReturnsCorrectValue()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Act
            var index = labService.SelectedOperationIndex;

            // Assert
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void SelectedOperationIndex_Set_UpdatesValue()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });
            var operationCount = labService.SelectedService.OperationItems.Length;

            // Act
            if (operationCount > 1)
            {
                labService.SelectedOperationIndex = 1;

                // Assert
                Assert.That(labService.SelectedOperationIndex, Is.EqualTo(1));
            }
            else
            {
                Assert.Pass("Service has only one operation, cannot test setting index to 1");
            }
        }

        [Test]
        public void SelectedOperationIndex_SetNegative_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => labService.SelectedOperationIndex = -1);
        }

        [Test]
        public void SelectedOperationIndex_SetTooLarge_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });
            var operationCount = labService.SelectedService.OperationItems.Length;

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => labService.SelectedOperationIndex = operationCount + 10);
        }

        [Test]
        public void SelectedOperationIndex_PreservedWhenSwitchingServices()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService });

            // Set operation index on first service
            labService.SelectedServiceIndex = 0;
            if (labService.SelectedService.OperationItems.Length > 1)
            {
                labService.SelectedOperationIndex = 1;
            }

            // Act - switch to second service and back
            labService.SelectedServiceIndex = 1;
            labService.SelectedServiceIndex = 0;

            // Assert - operation index should be restored
            if (labService.SelectedService.OperationItems.Length > 1)
            {
                Assert.That(labService.SelectedOperationIndex, Is.EqualTo(1));
            }
        }

        [Test]
        public void SelectedOperation_ReturnsCorrectOperationItem()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Act
            var operation = labService.SelectedOperation;

            // Assert
            Assert.That(operation, Is.Not.Null);
            Assert.That(operation.Service.Service, Is.EqualTo(fakeAuthService));
        }

        [Test]
        public void SelectedOperation_Set_UpdatesSelectedServiceAndOperation()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService });
            labService.SelectedServiceIndex = 0; // Start with first service

            // Get an operation from the third service
            var targetServiceItem = labService.Services[2];
            if (targetServiceItem.OperationItems.Length == 0)
            {
                Assert.Pass("Service has no operations, cannot test");
                return;
            }
            var targetOperation = targetServiceItem.OperationItems[0];

            // Act - set operation from a different service
            labService.SelectedOperation = targetOperation;

            // Assert - should automatically select the correct service
            Assert.That(labService.SelectedServiceIndex, Is.EqualTo(2));
            Assert.That(labService.SelectedService, Is.EqualTo(targetServiceItem));
            Assert.That(labService.SelectedOperation, Is.EqualTo(targetOperation));
        }

        [Test]
        public void SelectedOperation_SetDifferentOperationSameService_UpdatesOperation()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            var serviceItem = labService.Services[0];
            if (serviceItem.OperationItems.Length < 2)
            {
                Assert.Pass("Service has less than 2 operations, cannot test");
                return;
            }

            // Start with first operation
            labService.SelectedOperationIndex = 0;
            var secondOperation = serviceItem.OperationItems[1];

            // Act - set to second operation
            labService.SelectedOperation = secondOperation;

            // Assert
            Assert.That(labService.SelectedOperationIndex, Is.EqualTo(1));
            Assert.That(labService.SelectedOperation, Is.EqualTo(secondOperation));
        }

        [Test]
        public void SelectedOperation_SetNull_ThrowsArgumentNullException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => labService.SelectedOperation = null);
        }

        [Test]
        public void SelectedOperation_SetOperationFromNonConfiguredService_ThrowsArgumentException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Create an operation from a service that's not configured
            var otherFakeService = A.Fake<IConfigurationService>();
            var otherServiceItem = new LabAppService.ServiceItem(otherFakeService, o => true);
            if (otherServiceItem.OperationItems.Length == 0)
            {
                Assert.Pass("Service has no operations, cannot test");
                return;
            }
            var otherOperation = otherServiceItem.OperationItems[0];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => labService.SelectedOperation = otherOperation);
        }

        [Test]
        public void SelectedOperation_SetBeforeConfigure_ThrowsInvalidOperationException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            var fakeServiceItem = new LabAppService.ServiceItem(fakeAuthService, o => true);
            if (fakeServiceItem.OperationItems.Length == 0)
            {
                Assert.Pass("Service has no operations, cannot test");
                return;
            }
            var fakeOperation = fakeServiceItem.OperationItems[0];

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => labService.SelectedOperation = fakeOperation);
        }

        [Test]
        public void SelectedService_ReturnsCorrectServiceItem()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService });
            labService.SelectedServiceIndex = 1;

            // Act
            var service = labService.SelectedService;

            // Assert
            Assert.That(service, Is.Not.Null);
            Assert.That(service.Service, Is.EqualTo(fakeResourceService));
        }

        [Test]
        public void SelectedService_Set_UpdatesSelectedServiceIndex()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService });
            labService.SelectedServiceIndex = 0;

            var targetService = labService.Services[2];

            // Act
            labService.SelectedService = targetService;

            // Assert
            Assert.That(labService.SelectedServiceIndex, Is.EqualTo(2));
            Assert.That(labService.SelectedService, Is.EqualTo(targetService));
            Assert.That(labService.SelectedService.Service, Is.EqualTo(fakeConfigService));
        }

        [Test]
        public void SelectedService_SetNull_ThrowsArgumentNullException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => labService.SelectedService = null);
        }

        [Test]
        public void SelectedService_SetServiceNotInArray_ThrowsArgumentException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Create a fake service that isn't in the configured services
            var otherFakeService = A.Fake<IConfigurationService>();
            var otherServiceItem = new LabAppService.ServiceItem(otherFakeService, o => true);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => labService.SelectedService = otherServiceItem);
        }

        [Test]
        public void SelectedService_SetBeforeConfigure_ThrowsInvalidOperationException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            var fakeServiceItem = new LabAppService.ServiceItem(fakeAuthService, o => true);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => labService.SelectedService = fakeServiceItem);
        }

        #endregion

        #region Services Setter Tests

        [Test]
        public void Services_SetNull_ThrowsArgumentNullException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => labService.Services = null);
        }

        [Test]
        public void Services_Set_ResetsSelectedServiceIndexToZero()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService });
            labService.SelectedServiceIndex = 2; // Set to last service

            var newServices = new[] {
                new LabAppService.ServiceItem(fakeAuthService, o => true),
                new LabAppService.ServiceItem(fakeResourceService, o => true)
            };

            // Act - set new services array
            labService.Services = newServices;

            // Assert - should reset to 0
            Assert.That(labService.SelectedServiceIndex, Is.EqualTo(0));
        }

        [Test]
        public void Services_SetSmallerArray_ResetsSelection()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService });
            labService.SelectedServiceIndex = 2; // Index 2 is valid in 3-item array

            // Act - set to smaller array where index 2 would be invalid
            var newServices = new[] {
                new LabAppService.ServiceItem(fakeAuthService, o => true)
            };
            labService.Services = newServices;

            // Assert - should reset to 0, preventing out-of-range
            Assert.That(labService.SelectedServiceIndex, Is.EqualTo(0));
            Assert.That(labService.Services.Length, Is.EqualTo(1));
        }

        [Test]
        public void Services_SetEmptyArray_ResetsSelection()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Act
            labService.Services = Array.Empty<LabAppService.ServiceItem>();

            // Assert
            Assert.That(labService.SelectedServiceIndex, Is.EqualTo(0));
            Assert.That(labService.Services.Length, Is.EqualTo(0));
        }

        #endregion

        #region ServiceItem.SelectedOperationIndex Tests

        [Test]
        public void ServiceItem_SelectedOperationIndex_SetNegative_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var serviceItem = new LabAppService.ServiceItem(fakeAuthService, o => true);

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => serviceItem.SelectedOperationIndex = -1);
        }

        [Test]
        public void ServiceItem_SelectedOperationIndex_SetBeyondBounds_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var serviceItem = new LabAppService.ServiceItem(fakeAuthService, o => true);
            var operationCount = serviceItem.OperationItems.Length;

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => serviceItem.SelectedOperationIndex = operationCount + 10);
        }

        [Test]
        public void ServiceItem_SelectedOperationIndex_SetValidIndex_Succeeds()
        {
            // Arrange
            var serviceItem = new LabAppService.ServiceItem(fakeAuthService, o => true);

            if (serviceItem.OperationItems.Length < 2)
            {
                Assert.Pass("Service has less than 2 operations, cannot test setting to index 1");
                return;
            }

            // Act
            serviceItem.SelectedOperationIndex = 1;

            // Assert
            Assert.That(serviceItem.SelectedOperationIndex, Is.EqualTo(1));
        }

        [Test]
        public void ServiceItem_SelectedOperationIndex_SetZeroWhenNoOperations_Succeeds()
        {
            // Arrange - create a service with no operations
            var emptyFakeService = A.Fake<IAuthenticationService>();
            var serviceItem = new LabAppService.ServiceItem(emptyFakeService, o => false); // Filter out all operations

            // Act
            serviceItem.SelectedOperationIndex = 0;

            // Assert
            Assert.That(serviceItem.SelectedOperationIndex, Is.EqualTo(0));
        }

        [Test]
        public void ServiceItem_SelectedOperationIndex_SetNonZeroWhenNoOperations_ThrowsArgumentOutOfRangeException()
        {
            // Arrange - create a service with no operations
            var emptyFakeService = A.Fake<IAuthenticationService>();
            var serviceItem = new LabAppService.ServiceItem(emptyFakeService, o => false); // Filter out all operations

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => serviceItem.SelectedOperationIndex = 1);
        }

        #endregion

        #region LookupFirstOperationIndexByDisplayName Tests

        [Test]
        public void LookupFirstOperationIndexByDisplayName_FullMatch_ReturnsCorrectIndex()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });
            var serviceItem = labService.Services[0];

            // Act
            var index = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "Authenticate");

            // Assert
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            if (index >= 0)
            {
                Assert.That(serviceItem.OperationItems[index].DisplayName, Is.EqualTo("Authenticate"));
            }
        }

        [Test]
        public void LookupFirstOperationIndexByDisplayName_PrefixMatch_ReturnsFirstMatch()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });
            var serviceItem = labService.Services[0];

            // Act - look for operations starting with "Is"
            var index = serviceItem.LookupFirstOperationIndexByDisplayName(prefixDisplayName: "Is");

            // Assert
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            if (index >= 0)
            {
                Assert.That(serviceItem.OperationItems[index].DisplayName, Does.StartWith("Is"));
            }
        }

        [Test]
        public void LookupFirstOperationIndexByDisplayName_SubstringMatch_ReturnsFirstMatch()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });
            var serviceItem = labService.Services[0];

            // Act - look for operations containing "Auth"
            var index = serviceItem.LookupFirstOperationIndexByDisplayName(substringDisplayName: "Auth");

            // Assert
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            if (index >= 0)
            {
                Assert.That(serviceItem.OperationItems[index].DisplayName, Does.Contain("Auth"));
            }
        }

        [Test]
        public void LookupFirstOperationIndexByDisplayName_NoMatch_ReturnsNegativeOne()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });
            var serviceItem = labService.Services[0];

            // Act
            var index = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "NonExistentOperation");

            // Assert
            Assert.That(index, Is.EqualTo(-1));
        }

        [Test]
        public void LookupFirstOperationIndexByDisplayName_NoConditions_ReturnsZero()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });
            var serviceItem = labService.Services[0];

            // Act
            var index = serviceItem.LookupFirstOperationIndexByDisplayName();

            // Assert
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void LookupFirstOperationIndexByDisplayName_FullMatchPriority_ReturnsExactMatch()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });
            var serviceItem = labService.Services[0];

            // Find an operation name that exists
            var existingOpName = serviceItem.OperationItems[0].DisplayName;

            // Act - provide all three conditions, full match should take priority
            var index = serviceItem.LookupFirstOperationIndexByDisplayName(
                fullDisplayName: existingOpName,
                prefixDisplayName: "xyz",  // This won't match
                substringDisplayName: "abc"); // This won't match

            // Assert - should find the full match
            Assert.That(index, Is.EqualTo(0));
        }

        #endregion

        #region SelectOperation Tests

        [Test]
        public void SelectOperation_ValidOperation_UpdatesSelectedOperationIndex()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });
            var serviceItem = labService.Services[0];

            // Ensure there are at least 2 operations
            if (serviceItem.OperationItems.Length < 2)
            {
                Assert.Pass("Service has less than 2 operations, cannot test selection");
                return;
            }

            var targetOperation = serviceItem.OperationItems[1];

            // Act
            serviceItem.SelectOperation(targetOperation);

            // Assert
            Assert.That(serviceItem.SelectedOperationIndex, Is.EqualTo(1));
        }

        [Test]
        public void SelectOperation_OperationFromDifferentService_ThrowsArgumentException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService });
            var serviceItem1 = labService.Services[0];
            var serviceItem2 = labService.Services[1];

            var operationFromService2 = serviceItem2.OperationItems[0];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => serviceItem1.SelectOperation(operationFromService2));
        }

        [Test]
        public void SelectOperation_FirstOperation_SetsIndexToZero()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });
            var serviceItem = labService.Services[0];

            // Set to a different index first
            if (serviceItem.OperationItems.Length > 1)
            {
                serviceItem.SelectedOperationIndex = 1;
            }

            var firstOperation = serviceItem.OperationItems[0];

            // Act
            serviceItem.SelectOperation(firstOperation);

            // Assert
            Assert.That(serviceItem.SelectedOperationIndex, Is.EqualTo(0));
        }

        #endregion

        #region SetArgument Tests

        [Test]
        public void SetArgument_ValidIntArgument_SetsValue()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            // Select GetRuntimeValue(int resourceID) operation
            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0), "GetRuntimeValue operation should exist");
            var operation = serviceItem.OperationItems[opIndex];

            Assert.That(operation.OperationMetadata.Parameters.Length, Is.EqualTo(1), "GetRuntimeValue should have 1 parameter");
            Assert.That(operation.OperationMetadata.Parameters[0].Type, Is.EqualTo(typeof(int)), "Parameter should be int");

            // Act
            operation.SetMethodArgument(0, 42);

            // Assert
            Assert.That(operation.MethodArguments[0], Is.EqualTo(42));
        }

        [Test]
        public void SetArgument_NullForValueType_ThrowsArgumentException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            // Select GetRuntimeValue(int resourceID) operation
            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0));
            var operation = serviceItem.OperationItems[opIndex];

            // Act & Assert - try to set null to int parameter
            Assert.Throws<ArgumentException>(() => operation.SetMethodArgument(0, null));
        }

        [Test]
        public void SetArgument_WrongType_ThrowsArgumentException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            // Select GetRuntimeValue(int resourceID) operation
            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0));
            var operation = serviceItem.OperationItems[opIndex];

            // Act & Assert - try to set string value to int parameter
            Assert.Throws<ArgumentException>(() => operation.SetMethodArgument(0, "not-an-int"));
        }

        [Test]
        public void SetArgument_NegativeIndex_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0));
            var operation = serviceItem.OperationItems[opIndex];

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => operation.SetMethodArgument(-1, 123));
        }

        [Test]
        public void SetArgument_IndexTooLarge_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0));
            var operation = serviceItem.OperationItems[opIndex];
            var paramCount = operation.OperationMetadata.Parameters.Length;

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => operation.SetMethodArgument(paramCount + 5, 123));
        }

        [Test]
        public void ResetArguments_RestoresDefaultValues()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0));
            var operation = serviceItem.OperationItems[opIndex];
            var originalValue = operation.MethodArguments[0];

            // Modify the argument
            operation.SetMethodArgument(0, 999);
            Assert.That(operation.MethodArguments[0], Is.EqualTo(999));

            // Act - reset
            operation.ResetMethodArguments();

            // Assert - should be back to default
            Assert.That(operation.MethodArguments[0], Is.EqualTo(originalValue));
        }

        #endregion

        #region GetDefaultValue Tests

        [Test]
        public void GetDefaultValue_String_ReturnsEmptyString()
        {
            // Act
            var result = LabAppService.OperationItem.GetDefaultValue(typeof(string));

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void GetDefaultValue_Int_ReturnsZero()
        {
            // Act
            var result = LabAppService.OperationItem.GetDefaultValue(typeof(int));

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void GetDefaultValue_Bool_ReturnsFalse()
        {
            // Act
            var result = LabAppService.OperationItem.GetDefaultValue(typeof(bool));

            // Assert
            Assert.That(result, Is.EqualTo(false));
        }

        [Test]
        public void GetDefaultValue_DateTime_ReturnsNow()
        {
            // Act
            var result = LabAppService.OperationItem.GetDefaultValue(typeof(DateTime));

            // Assert
            Assert.That(result, Is.TypeOf<DateTime>());
            var dt = (DateTime)result;
            Assert.That(dt, Is.EqualTo(DateTime.Now).Within(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void GetDefaultValue_Guid_ReturnsEmpty()
        {
            // Act
            var result = LabAppService.OperationItem.GetDefaultValue(typeof(Guid));

            // Assert
            Assert.That(result, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void GetDefaultValue_TimeSpan_ReturnsZero()
        {
            // Act
            var result = LabAppService.OperationItem.GetDefaultValue(typeof(TimeSpan));

            // Assert
            Assert.That(result, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void GetDefaultValue_ByteArray_ReturnsEmptyArray()
        {
            // Act
            var result = LabAppService.OperationItem.GetDefaultValue(typeof(byte[]));

            // Assert
            Assert.That(result, Is.TypeOf<byte[]>());
            Assert.That(((byte[])result).Length, Is.EqualTo(0));
        }

        [Test]
        public void GetDefaultValue_NullType_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => LabAppService.OperationItem.GetDefaultValue(null));
        }

        [Test]
        public void GetDefaultValue_ReferenceType_ReturnsNull()
        {
            // Act
            var result = LabAppService.OperationItem.GetDefaultValue(typeof(object));

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion

        #region DynCallSelectedOperation Tests

        [Test]
        public async Task DynCallSelectedOperation_BooleanResult_ReturnsFormattedResult()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Select IsAuthenticated() operation
            var serviceItem = labService.Services.First(s => s.Service == fakeAuthService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "IsAuthenticated");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0), "IsAuthenticated operation should exist");
            var operation = serviceItem.OperationItems[opIndex];

            // Select the operation (automatically selects the service too)
            labService.SelectedOperation = operation;

            // Act
            var result = await labService.DynCallSelectedOperation();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.DisplayResult, Is.EqualTo("true"));
            Assert.That(result.ReturnType, Is.EqualTo(typeof(Task<bool>)));
        }

        [Test]
        public async Task DynCallSelectedOperation_ObjectResult_ReturnsFormattedResult()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeConfigService });

            // Select GetSystemInfo() operation
            var serviceItem = labService.Services.First(s => s.Service == fakeConfigService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetSystemInfo");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0), "GetSystemInfo operation should exist");
            var operation = serviceItem.OperationItems[opIndex];

            // Select the operation (automatically selects the service too)
            labService.SelectedOperation = operation;

            // Act
            var result = await labService.DynCallSelectedOperation();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.DisplayResult, Does.Contain("SystemInfo"));
            Assert.That(result.ReturnType.IsGenericType, Is.True);
            Assert.That(result.ReturnType.GetGenericTypeDefinition(), Is.EqualTo(typeof(Task<>)));
        }

        [Test]
        public async Task DynCallSelectedOperation_WithIntArgument_SuccessfullyInvokes()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            // Select GetRuntimeValue(int) operation
            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0), "GetRuntimeValue operation should exist");
            var operation = serviceItem.OperationItems[opIndex];

            // Select the operation (automatically selects the service too)
            labService.SelectedOperation = operation;

            // Verify operation has int parameter
            Assert.That(operation.OperationMetadata.Parameters.Length, Is.EqualTo(1));
            Assert.That(operation.OperationMetadata.Parameters[0].Type, Is.EqualTo(typeof(int)));

            // Set argument to specific value
            operation.SetMethodArgument(0, 123456);
            Assert.That(operation.MethodArguments[0], Is.EqualTo(123456), "Argument should be set to 123456");

            // Act
            var result = await labService.DynCallSelectedOperation();

            // Assert - operation succeeded and returned ResourceValue
            Assert.That(result, Is.Not.Null);
            Assert.That(result.DisplayResult, Does.Contain("ResourceValue"));
            Assert.That(result.ReturnType.IsGenericType, Is.True);
            Assert.That(result.ReturnType.GetGenericArguments()[0], Is.EqualTo(typeof(ResourceValue)));
        }

        [Test]
        public async Task DynCallSelectedOperation_MultipleServices_SelectsCorrectService()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService });

            Assert.That(labService.Services.Length, Is.EqualTo(3), "Should have 3 services");

            // Select second service (ResourceInteractionService)
            labService.SelectedServiceIndex = 1;
            Assert.That(labService.SelectedServiceIndex, Is.EqualTo(1));
            Assert.That(labService.SelectedService.Service, Is.EqualTo(fakeResourceService));

            // Select GetRuntimeValue operation
            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0));
            var operation = serviceItem.OperationItems[opIndex];

            // Select the operation (automatically selects the service too)
            labService.SelectedOperation = operation;

            operation.SetMethodArgument(0, 999);

            // Act
            var result = await labService.DynCallSelectedOperation();

            // Assert - operation succeeded
            Assert.That(result, Is.Not.Null);
            Assert.That(result.DisplayResult, Does.Contain("ResourceValue"));
        }

        #endregion

        #region ParameterCount Tests

        [Test]
        public void ParameterCount_WithNoParameters_ReturnsZero()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Find Authenticate() which has no parameters
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "Authenticate");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0), "Authenticate operation should exist");
            var operation = serviceItem.OperationItems[opIndex];

            // Act
            var paramCount = operation.MethodParameterCount;

            // Assert
            Assert.That(paramCount, Is.EqualTo(0));
            Assert.That(operation.MethodArguments.Length, Is.EqualTo(0));
        }

        [Test]
        public void ParameterCount_WithSingleParameter_ReturnsOne()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            // Find GetRuntimeValue(int) which has 1 parameter
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0), "GetRuntimeValue operation should exist");
            var operation = serviceItem.OperationItems[opIndex];

            // Act
            var paramCount = operation.MethodParameterCount;

            // Assert
            Assert.That(paramCount, Is.EqualTo(1));
            Assert.That(operation.MethodArguments.Length, Is.EqualTo(1));
        }

        [Test]
        public void ParameterCount_MatchesArgumentsLength()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService });

            // Act & Assert - verify ParameterCount matches Arguments.Length for all operations
            foreach (var serviceItem in labService.Services)
            {
                foreach (var operation in serviceItem.OperationItems)
                {
                    Assert.That(operation.MethodParameterCount, Is.EqualTo(operation.MethodArguments.Length),
                        $"ParameterCount should match Arguments.Length for operation {operation.DisplayName}");
                }
            }
        }

        #endregion

        #region SetArgumentsFromArray Tests

        [Test]
        public void SetArgumentsFromArray_WithNullArray_ThrowsArgumentNullException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => operation.SetMethodArgumentsFromArray(null));
        }

        [Test]
        public void SetArgumentsFromArray_WithWrongLength_ThrowsArgumentException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            // GetRuntimeValue has 1 parameter, try with 2
            var wrongLengthArray = new object[] { 123, 456 };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => operation.SetMethodArgumentsFromArray(wrongLengthArray));
            Assert.That(ex.Message, Does.Contain("does not match parameter count"));
        }

        [Test]
        public void SetArgumentsFromArray_WithTooFewArguments_ThrowsArgumentException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            // GetRuntimeValue has 1 parameter, try with 0
            var emptyArray = Array.Empty<object>();

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => operation.SetMethodArgumentsFromArray(emptyArray));
            Assert.That(ex.Message, Does.Contain("does not match parameter count"));
        }

        [Test]
        public void SetArgumentsFromArray_WithValidArray_SetsAllArguments()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            var newValues = new object[] { 999888 };

            // Act
            operation.SetMethodArgumentsFromArray(newValues);

            // Assert
            Assert.That(operation.MethodArguments[0], Is.EqualTo(999888));
        }

        [Test]
        public void SetArgumentsFromArray_WithInvalidType_ThrowsArgumentException()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            // GetRuntimeValue expects int, try with string
            var invalidTypeArray = new object[] { "not an int" };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => operation.SetMethodArgumentsFromArray(invalidTypeArray));
        }

        [Test]
        public void SetArgumentsFromArray_WithEmptyArrayForNoParameterOperation_Succeeds()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "Authenticate");
            var operation = serviceItem.OperationItems[opIndex];

            // Authenticate has no parameters
            var emptyArray = Array.Empty<object>();

            // Act
            operation.SetMethodArgumentsFromArray(emptyArray);

            // Assert
            Assert.That(operation.MethodArguments.Length, Is.EqualTo(0));
        }

        [Test]
        public void SetArgumentsFromArray_ValidatesEachArgumentViaSetArgument()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            // Set initial value
            operation.SetMethodArgument(0, 100);
            Assert.That(operation.MethodArguments[0], Is.EqualTo(100));

            // Now use SetArgumentsFromArray to change it
            var newValues = new object[] { 200 };
            operation.SetMethodArgumentsFromArray(newValues);

            // Assert
            Assert.That(operation.MethodArguments[0], Is.EqualTo(200));
        }

        #endregion

        #region GetArgumentsAsArray Tests

        [Test]
        public void GetArgumentsAsArray_ReturnsDefensiveCopy()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            // Act
            var argsCopy = operation.GetMethodArgumentsAsArray();

            // Assert - verify it's not the same reference
            Assert.That(argsCopy, Is.Not.SameAs(operation.MethodArguments));
        }

        [Test]
        public void GetArgumentsAsArray_ContentsMatch()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            operation.SetMethodArgument(0, 12345);

            // Act
            var argsCopy = operation.GetMethodArgumentsAsArray();

            // Assert
            Assert.That(argsCopy.Length, Is.EqualTo(operation.MethodArguments.Length));
            Assert.That(argsCopy[0], Is.EqualTo(12345));
            Assert.That(argsCopy[0], Is.EqualTo(operation.MethodArguments[0]));
        }

        [Test]
        public void GetArgumentsAsArray_ModifyingCopy_DoesNotAffectOriginal()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            operation.SetMethodArgument(0, 555);

            // Act
            var argsCopy = operation.GetMethodArgumentsAsArray();
            argsCopy[0] = 999; // Modify the copy

            // Assert - original should be unchanged
            Assert.That(operation.MethodArguments[0], Is.EqualTo(555), "Original arguments should not be affected by modifying the copy");
            Assert.That(argsCopy[0], Is.EqualTo(999), "Copy should have the modified value");
        }

        [Test]
        public void GetArgumentsAsArray_ForNoParameterOperation_ReturnsEmptyArray()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "Authenticate");
            var operation = serviceItem.OperationItems[opIndex];

            // Act
            var argsCopy = operation.GetMethodArgumentsAsArray();

            // Assert
            Assert.That(argsCopy, Is.Not.Null);
            Assert.That(argsCopy.Length, Is.EqualTo(0));
        }

        [Test]
        public void GetArgumentsAsArray_MultipleCallsReturnIndependentCopies()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });
            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            operation.SetMethodArgument(0, 777);

            // Act
            var copy1 = operation.GetMethodArgumentsAsArray();
            var copy2 = operation.GetMethodArgumentsAsArray();

            // Assert
            Assert.That(copy1, Is.Not.SameAs(copy2), "Each call should return a new array instance");
            Assert.That(copy1[0], Is.EqualTo(copy2[0]), "Contents should match");
            Assert.That(copy1[0], Is.EqualTo(777));
        }

        #endregion

        #region Operation Filter Tests

        [Test]
        public void OperationFilter_ExcludesAsyncEnumerable()
        {
            // Arrange
            var operationFilter = new Func<ServiceOperationMetadata, bool>(operation =>
            {
                if (operation.Kind == ServiceOperationKind.AsyncEnumerable)
                    return false;
                return true;
            });

            var labService = new LabAppService(null, operationFilter);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService });

            // Act & Assert - verify no AsyncEnumerable operations are included
            foreach (var serviceItem in labService.Services)
            {
                foreach (var operation in serviceItem.OperationItems)
                {
                    Assert.That(operation.OperationMetadata.Kind, Is.Not.EqualTo(ServiceOperationKind.AsyncEnumerable),
                        $"Operation {operation.DisplayName} should not be AsyncEnumerable");
                }
            }
        }

        [Test]
        public void OperationFilter_ExcludesArrayParameters()
        {
            // Arrange - filter to exclude operations with array parameters
            var operationFilter = new Func<ServiceOperationMetadata, bool>(operation =>
            {
                foreach (var param in operation.Parameters)
                {
                    if (param.IsArray)
                        return false;
                }
                return true;
            });

            var labService = new LabAppService(null, operationFilter);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeConfigService });

            // Act & Assert - verify no operations with array parameters are included
            foreach (var serviceItem in labService.Services)
            {
                foreach (var operation in serviceItem.OperationItems)
                {
                    foreach (var param in operation.OperationMetadata.Parameters)
                    {
                        Assert.That(param.IsArray, Is.False,
                            $"Operation {operation.DisplayName} parameter {param.Name} should not be an array");
                    }
                }
            }
        }

        [Test]
        public void OperationFilter_ExcludesResourceValueParameters()
        {
            // Arrange - filter to exclude operations with ResourceValue parameters
            var operationFilter = new Func<ServiceOperationMetadata, bool>(operation =>
            {
                foreach (var param in operation.Parameters)
                {
                    if (param.Type == typeof(ResourceValue) || param.Type == typeof(ResourceValue[]))
                        return false;
                }
                return true;
            });

            var labService = new LabAppService(null, operationFilter);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService });

            // Act & Assert - verify no operations with ResourceValue parameters are included
            foreach (var serviceItem in labService.Services)
            {
                foreach (var operation in serviceItem.OperationItems)
                {
                    foreach (var param in operation.OperationMetadata.Parameters)
                    {
                        Assert.That(param.Type, Is.Not.EqualTo(typeof(ResourceValue)),
                            $"Operation {operation.DisplayName} parameter {param.Name} should not be ResourceValue");
                        Assert.That(param.Type, Is.Not.EqualTo(typeof(ResourceValue[])),
                            $"Operation {operation.DisplayName} parameter {param.Name} should not be ResourceValue[]");
                    }
                }
            }
        }

        [Test]
        public void OperationFilter_AllowsNormalOperations()
        {
            // Arrange - no filter (allow all)
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService });

            // Act
            var authServiceItem = labService.Services.First(s => s.Service == fakeAuthService);

            // Assert - should have operations
            Assert.That(authServiceItem.OperationItems.Length, Is.GreaterThan(0),
                "AuthenticationService should have at least one operation");

            // Find Authenticate operation (has no parameters, returns Task<IhcUser>)
            var authenticateOp = authServiceItem.OperationItems.FirstOrDefault(op => op.DisplayName == "Authenticate");
            Assert.That(authenticateOp, Is.Not.Null, "Authenticate operation should exist");
            Assert.That(authenticateOp.MethodParameterCount, Is.EqualTo(0), "Authenticate should have no parameters");
        }

        [Test]
        public void OperationFilter_CombinedFilters_MatchesExpectedCount()
        {
            // Arrange - combined filter matching MetadataHelper logic
            var operationFilter = new Func<ServiceOperationMetadata, bool>(operation =>
            {
                // Exclude AsyncEnumerable
                if (operation.Kind == ServiceOperationKind.AsyncEnumerable)
                    return false;

                // Exclude array and ResourceValue parameters
                foreach (var param in operation.Parameters)
                {
                    if (param.IsArray || param.Type == typeof(ResourceValue) || param.Type == typeof(ResourceValue[]))
                        return false;
                }

                return true;
            });

            var labService = new LabAppService(null, operationFilter);
            var labServiceNoFilter = new LabAppService(null, null);

            // Act
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService });
            labServiceNoFilter.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService });

            // Assert - filtered service should have fewer or equal operations
            for (int i = 0; i < labService.Services.Length; i++)
            {
                var filteredCount = labService.Services[i].OperationItems.Length;
                var unfilteredCount = labServiceNoFilter.Services[i].OperationItems.Length;

                Assert.That(filteredCount, Is.LessThanOrEqualTo(unfilteredCount),
                    $"Service {labService.Services[i].DisplayName} filtered count should be <= unfiltered count");

                // Log for diagnostic purposes
                TestContext.Out.WriteLine($"Service: {labService.Services[i].DisplayName}, Filtered: {filteredCount}, Unfiltered: {unfilteredCount}");
            }
        }

        #endregion

        #region Argument Persistence Tests

        [Test]
        public void ArgumentPersistence_SwitchOperationsWithinService_PreservesArguments()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            // Get first operation with parameters
            var serviceItem = labService.Services[0];
            var operation1Index = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            Assert.That(operation1Index, Is.GreaterThanOrEqualTo(0), "GetRuntimeValue should exist");

            // Select first operation and set argument
            labService.SelectedServiceIndex = 0;
            labService.SelectedOperationIndex = operation1Index;
            labService.SelectedOperation.SetMethodArgument(0, 12345);

            // Switch to another operation
            var operation2Index = (operation1Index + 1) % serviceItem.OperationItems.Length;
            labService.SelectedOperationIndex = operation2Index;

            // Switch back to first operation
            labService.SelectedOperationIndex = operation1Index;

            // Assert - argument should be preserved
            Assert.That(labService.SelectedOperation.MethodArguments[0], Is.EqualTo(12345),
                "Argument should be preserved when switching back to operation");
        }

        [Test]
        public void ArgumentPersistence_SwitchServices_PreservesArgumentsPerService()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService });

            // Service 0 - Auth - no parameters (just verify selection works)
            labService.SelectedServiceIndex = 0;
            var authOpIndex = labService.Services[0].LookupFirstOperationIndexByDisplayName(fullDisplayName: "Authenticate");
            labService.SelectedOperationIndex = authOpIndex;

            // Service 1 - Resource - set argument
            labService.SelectedServiceIndex = 1;
            var resourceOpIndex = labService.Services[1].LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            labService.SelectedOperationIndex = resourceOpIndex;
            labService.SelectedOperation.SetMethodArgument(0, 99999);

            // Switch back to Service 0
            labService.SelectedServiceIndex = 0;

            // Switch back to Service 1
            labService.SelectedServiceIndex = 1;
            labService.SelectedOperationIndex = resourceOpIndex;

            // Assert - argument should be preserved
            Assert.That(labService.SelectedOperation.MethodArguments[0], Is.EqualTo(99999),
                "Argument should be preserved when switching services and back");
        }

        [Test]
        public void ArgumentPersistence_ComplexNavigation_PreservesArguments()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService });

            // Service 1 - Set argument on operation A
            labService.SelectedServiceIndex = 1;
            var opAIndex = labService.Services[1].LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            labService.SelectedOperationIndex = opAIndex;
            labService.SelectedOperation.SetMethodArgument(0, 11111);

            // Switch to Service 2
            labService.SelectedServiceIndex = 2;
            var opBIndex = 0;
            labService.SelectedOperationIndex = opBIndex;

            // Switch to Service 0
            labService.SelectedServiceIndex = 0;

            // Return to Service 1, operation A
            labService.SelectedServiceIndex = 1;
            labService.SelectedOperationIndex = opAIndex;

            // Assert - argument should still be preserved
            Assert.That(labService.SelectedOperation.MethodArguments[0], Is.EqualTo(11111),
                "Argument should be preserved through complex navigation");
        }

        [Test]
        public void ArgumentPersistence_GetArgumentsAsArray_ReturnsCurrentValues()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            labService.SelectedServiceIndex = 0;
            var opIndex = labService.Services[0].LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            labService.SelectedOperationIndex = opIndex;

            // Set argument
            labService.SelectedOperation.SetMethodArgument(0, 77777);

            // Act - get arguments as array
            var argumentsCopy = labService.SelectedOperation.GetMethodArgumentsAsArray();

            // Assert
            Assert.That(argumentsCopy.Length, Is.EqualTo(1));
            Assert.That(argumentsCopy[0], Is.EqualTo(77777), "GetArgumentsAsArray should return current argument values");
        }

        [Test]
        public void ArgumentPersistence_SetArgumentsFromArray_PreservesValues()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            labService.SelectedServiceIndex = 0;
            var opIndex = labService.Services[0].LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            labService.SelectedOperationIndex = opIndex;

            // Act - set arguments via array
            labService.SelectedOperation.SetMethodArgumentsFromArray(new object[] { 55555 });

            // Switch to another operation and back
            var otherOpIndex = (opIndex + 1) % labService.Services[0].OperationItems.Length;
            labService.SelectedOperationIndex = otherOpIndex;
            labService.SelectedOperationIndex = opIndex;

            // Assert - argument should be preserved
            Assert.That(labService.SelectedOperation.MethodArguments[0], Is.EqualTo(55555),
                "Arguments set via SetArgumentsFromArray should be preserved");
        }

        #endregion

        #region ArgumentChanged Event Tests

        [Test]
        public void ArgumentChanged_SetArgument_FiresEvent()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            bool eventFired = false;
            LabAppService.MethodArgumentChangedEventArgs? capturedArgs = null;

            operation.ArgumentChanged += (sender, e) =>
            {
                eventFired = true;
                capturedArgs = e;
            };

            // Act
            operation.SetMethodArgument(0, 12345);

            // Assert
            Assert.That(eventFired, Is.True, "ArgumentChanged event should fire");
            Assert.That(capturedArgs, Is.Not.Null);
            Assert.That(capturedArgs!.Index, Is.EqualTo(0));
            Assert.That(capturedArgs.NewValue, Is.EqualTo(12345));
        }

        [Test]
        public void ArgumentChanged_SetArgumentSameValue_DoesNotFireEvent()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            // Set initial value
            operation.SetMethodArgument(0, 99999);

            int eventFireCount = 0;
            operation.ArgumentChanged += (sender, e) =>
            {
                eventFireCount++;
            };

            // Act - set same value again
            operation.SetMethodArgument(0, 99999);

            // Assert - event should not fire because value didn't change
            Assert.That(eventFireCount, Is.EqualTo(0), "ArgumentChanged should not fire when value is unchanged");
        }

        [Test]
        public void ArgumentChanged_SetArgumentsFromArray_FiresEventsForChangedValues()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            // Set initial value
            operation.SetMethodArgument(0, 100);

            var firedEvents = new List<LabAppService.MethodArgumentChangedEventArgs>();
            operation.ArgumentChanged += (sender, e) =>
            {
                firedEvents.Add(e);
            };

            // Act - change value via SetArgumentsFromArray
            operation.SetMethodArgumentsFromArray(new object[] { 200 });

            // Assert
            Assert.That(firedEvents.Count, Is.EqualTo(1), "Should fire one event for the changed value");
            Assert.That(firedEvents[0].Index, Is.EqualTo(0));
            Assert.That(firedEvents[0].OldValue, Is.EqualTo(100));
            Assert.That(firedEvents[0].NewValue, Is.EqualTo(200));
        }

        [Test]
        public void ArgumentChanged_SetArgumentsFromArray_NoEventsForUnchangedValues()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            // Set initial value
            operation.SetMethodArgument(0, 777);

            int eventFireCount = 0;
            operation.ArgumentChanged += (sender, e) =>
            {
                eventFireCount++;
            };

            // Act - set same value via SetArgumentsFromArray
            operation.SetMethodArgumentsFromArray(new object[] { 777 });

            // Assert - no event should fire
            Assert.That(eventFireCount, Is.EqualTo(0), "No events should fire when values are unchanged");
        }

        [Test]
        public void ArgumentChanged_EventArgs_ContainsCorrectIndex()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            LabAppService.MethodArgumentChangedEventArgs? capturedArgs = null;
            operation.ArgumentChanged += (sender, e) => { capturedArgs = e; };

            // Act
            operation.SetMethodArgument(0, 555);

            // Assert
            Assert.That(capturedArgs, Is.Not.Null);
            Assert.That(capturedArgs!.Index, Is.EqualTo(0), "Index should be 0");
        }

        [Test]
        public void ArgumentChanged_EventArgs_ContainsCorrectOldValue()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            // Set initial value
            operation.SetMethodArgument(0, 111);

            LabAppService.MethodArgumentChangedEventArgs? capturedArgs = null;
            operation.ArgumentChanged += (sender, e) => { capturedArgs = e; };

            // Act
            operation.SetMethodArgument(0, 222);

            // Assert
            Assert.That(capturedArgs, Is.Not.Null);
            Assert.That(capturedArgs!.OldValue, Is.EqualTo(111), "OldValue should be 111");
        }

        [Test]
        public void ArgumentChanged_EventArgs_ContainsCorrectNewValue()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            LabAppService.MethodArgumentChangedEventArgs? capturedArgs = null;
            operation.ArgumentChanged += (sender, e) => { capturedArgs = e; };

            // Act
            operation.SetMethodArgument(0, 333);

            // Assert
            Assert.That(capturedArgs, Is.Not.Null);
            Assert.That(capturedArgs!.NewValue, Is.EqualTo(333), "NewValue should be 333");
        }

        [Test]
        public void ArgumentChanged_NullToValue_OldValueIsNull()
        {
            // Arrange - need an operation with a nullable parameter
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService });

            // Find an operation that accepts null values
            LabAppService.OperationItem? operationWithNullableParam = null;
            foreach (var svcItem in labService.Services)
            {
                foreach (var op in svcItem.OperationItems)
                {
                    foreach (var param in op.OperationMetadata.Parameters)
                    {
                        if (!param.Type.IsValueType || Nullable.GetUnderlyingType(param.Type) != null)
                        {
                            operationWithNullableParam = op;
                            break;
                        }
                    }
                    if (operationWithNullableParam != null) break;
                }
                if (operationWithNullableParam != null) break;
            }

            if (operationWithNullableParam == null)
            {
                Assert.Pass("No operation with nullable parameter found, cannot test null handling");
                return;
            }

            // Find the nullable parameter index
            int nullableParamIndex = -1;
            for (int i = 0; i < operationWithNullableParam.OperationMetadata.Parameters.Length; i++)
            {
                var param = operationWithNullableParam.OperationMetadata.Parameters[i];
                if (!param.Type.IsValueType || Nullable.GetUnderlyingType(param.Type) != null)
                {
                    nullableParamIndex = i;
                    break;
                }
            }

            // Ensure it starts with a null or default value
            operationWithNullableParam.SetMethodArgument(nullableParamIndex, null);

            LabAppService.MethodArgumentChangedEventArgs? capturedArgs = null;
            operationWithNullableParam.ArgumentChanged += (sender, e) => { capturedArgs = e; };

            // Act - set from null to a value
            operationWithNullableParam.SetMethodArgument(nullableParamIndex, "test-value");

            // Assert
            Assert.That(capturedArgs, Is.Not.Null);
            Assert.That(capturedArgs!.OldValue, Is.Null, "OldValue should be null");
            Assert.That(capturedArgs.NewValue, Is.EqualTo("test-value"));
        }

        [Test]
        public void ArgumentChanged_MultipleSubscribers_AllReceiveEvent()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            bool subscriber1Fired = false;
            bool subscriber2Fired = false;
            bool subscriber3Fired = false;

            operation.ArgumentChanged += (sender, e) => { subscriber1Fired = true; };
            operation.ArgumentChanged += (sender, e) => { subscriber2Fired = true; };
            operation.ArgumentChanged += (sender, e) => { subscriber3Fired = true; };

            // Act
            operation.SetMethodArgument(0, 999);

            // Assert
            Assert.That(subscriber1Fired, Is.True, "Subscriber 1 should receive event");
            Assert.That(subscriber2Fired, Is.True, "Subscriber 2 should receive event");
            Assert.That(subscriber3Fired, Is.True, "Subscriber 3 should receive event");
        }

        [Test]
        public void ArgumentChanged_Unsubscribe_StopsReceivingEvents()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            int eventFireCount = 0;
            EventHandler<LabAppService.MethodArgumentChangedEventArgs> handler = (sender, e) => { eventFireCount++; };

            operation.ArgumentChanged += handler;

            // First change - should fire event
            operation.SetMethodArgument(0, 100);
            Assert.That(eventFireCount, Is.EqualTo(1), "Event should fire before unsubscribe");

            // Unsubscribe
            operation.ArgumentChanged -= handler;

            // Act - second change - should NOT fire event
            operation.SetMethodArgument(0, 200);

            // Assert
            Assert.That(eventFireCount, Is.EqualTo(1), "Event should not fire after unsubscribe");
        }

        [Test]
        public void ArgumentChanged_TypeValidationFailure_NoEventFired()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services.First(s => s.Service == fakeResourceService);
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            int eventFireCount = 0;
            operation.ArgumentChanged += (sender, e) => { eventFireCount++; };

            // Act - try to set invalid type (should throw exception)
            Assert.Throws<ArgumentException>(() => operation.SetMethodArgument(0, "not-an-int"));

            // Assert - event should NOT fire because validation failed
            Assert.That(eventFireCount, Is.EqualTo(0), "Event should not fire when type validation fails");
        }

        #endregion

        #region CurrentOperationChanged Event Tests

        [Test]
        public void CurrentOperationChanged_ChangeServiceIndex_FiresEvent()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeAuthService, fakeResourceService });

            bool eventFired = false;
            LabAppService.CurrentOperationChangedEventArgs? capturedArgs = null;

            labService.CurrentOperationChanged += (sender, e) =>
            {
                eventFired = true;
                capturedArgs = e;
            };

            // Act - change service index
            labService.SelectedServiceIndex = 1;

            // Assert
            Assert.That(eventFired, Is.True, "CurrentOperationChanged should fire when service changes");
            Assert.That(capturedArgs, Is.Not.Null);
            Assert.That(capturedArgs.OldServiceIndex, Is.EqualTo(0));
            Assert.That(capturedArgs.NewServiceIndex, Is.EqualTo(1));
        }

        [Test]
        public void CurrentOperationChanged_ChangeOperationIndex_FiresEvent()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var service = labService.Services[0];
            Assert.That(service.OperationItems.Length, Is.GreaterThan(1), "Need multiple operations for test");

            bool eventFired = false;
            LabAppService.CurrentOperationChangedEventArgs? capturedArgs = null;

            labService.CurrentOperationChanged += (sender, e) =>
            {
                eventFired = true;
                capturedArgs = e;
            };

            // Act - change operation index within same service
            labService.SelectedOperationIndex = 1;

            // Assert
            Assert.That(eventFired, Is.True, "CurrentOperationChanged should fire when operation changes");
            Assert.That(capturedArgs, Is.Not.Null);
            Assert.That(capturedArgs.OldOperationIndex, Is.EqualTo(0));
            Assert.That(capturedArgs.NewOperationIndex, Is.EqualTo(1));
            Assert.That(capturedArgs.OldServiceIndex, Is.EqualTo(capturedArgs.NewServiceIndex), "Service should stay the same");
        }

        [Test]
        public void CurrentOperationChanged_SameSelection_DoesNotFireEvent()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            int eventFireCount = 0;
            labService.CurrentOperationChanged += (sender, e) => { eventFireCount++; };

            // Act - set to same service index
            labService.SelectedServiceIndex = 0;

            // Assert
            Assert.That(eventFireCount, Is.EqualTo(0), "Event should not fire when selection unchanged");
        }

        #endregion

        #region ServicesChanged Event Tests

        [Test]
        public void ServicesChanged_Configure_FiresEvent()
        {
            // Arrange
            var labService = new LabAppService(null, null);

            bool eventFired = false;
            labService.ServicesChanged += (sender, e) => { eventFired = true; };

            // Act
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            // Assert
            Assert.That(eventFired, Is.True, "ServicesChanged should fire when Configure is called");
        }

        [Test]
        public void ServicesChanged_SetServicesProperty_FiresEvent()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            bool eventFired = false;
            labService.ServicesChanged += (sender, e) => { eventFired = true; };

            // Act - set Services property
            var newServices = new[] { new LabAppService.ServiceItem(fakeAuthService, op => true) };
            labService.Services = newServices;

            // Assert
            Assert.That(eventFired, Is.True, "ServicesChanged should fire when Services property is set");
        }

        #endregion

        #region ResetMethodArguments Event Tests

        [Test]
        public void ResetMethodArguments_FiresMethodArgumentChangedEvents()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            // Modify argument from default
            operation.SetMethodArgument(0, 12345);

            var firedEvents = new List<LabAppService.MethodArgumentChangedEventArgs>();
            operation.ArgumentChanged += (sender, e) => { firedEvents.Add(e); };

            // Act - reset to defaults
            operation.ResetMethodArguments();

            // Assert
            Assert.That(firedEvents.Count, Is.EqualTo(1), "Should fire event for changed argument");
            Assert.That(firedEvents[0].OldValue, Is.EqualTo(12345));
            Assert.That(firedEvents[0].Index, Is.EqualTo(0));
        }

        [Test]
        public void ResetMethodArguments_FiresOnlyForChangedValues()
        {
            // Arrange
            var labService = new LabAppService(null, null);
            labService.Configure(settings, new IIHCApiService[] { fakeResourceService });

            var serviceItem = labService.Services[0];
            var opIndex = serviceItem.LookupFirstOperationIndexByDisplayName(fullDisplayName: "GetRuntimeValue");
            var operation = serviceItem.OperationItems[opIndex];

            // Don't modify argument - it's already at default

            int eventFireCount = 0;
            operation.ArgumentChanged += (sender, e) => { eventFireCount++; };

            // Act - reset (should not fire since already at default)
            operation.ResetMethodArguments();

            // Assert
            Assert.That(eventFireCount, Is.EqualTo(0), "Should not fire events when values already at defaults");
        }

        #endregion
    }
}
