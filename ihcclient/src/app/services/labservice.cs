using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Text;
using System.Reflection;

namespace Ihc.App
{
    /// <summary>
    /// Hold information about a service and associated operation 
    /// </summary>
    public class LabServiceItem
    {
        public IIHCApiService Service { get; init; }
        public string DisplayName { get; init; }
        public LabServiceOperationItem[] OperationItems { get; init; }
    
        public int SelectedOperationIndex { get; set; }

        public LabServiceItem(IIHCApiService service)
        {
            Service = service;

            // Find the IHC service interface (works for both real services and fakes)
            var serviceInterfaces = service.GetType().GetInterfaces()
                .Where(i => i != typeof(IIHCApiService) && typeof(IIHCApiService).IsAssignableFrom(i))
                .ToList();

            // Use the interface name, stripping the leading 'I' for display
            if (serviceInterfaces.Count > 0)
            {
                var interfaceName = serviceInterfaces[0].Name;
                DisplayName = interfaceName.StartsWith("I") ? interfaceName.Substring(1) : interfaceName;
            }
            else
            {
                // Fallback to type name if no interface found
                DisplayName = service.GetType().Name;
            }

            OperationItems = ServiceMetadata.GetOperations(Service)
                .Select(method => new LabServiceOperationItem(method))
                .ToArray();

            // First operation selected initially.
            SelectedOperationIndex = 0;
        }
    }

    /// <summary>
    /// Hold information about a service operation
    /// </summary>
    public class LabServiceOperationItem
    {
        public ServiceOperationMetadata OperationMetadata { get; }
        public string DisplayName { get; }

        public LabServiceOperationItem(ServiceOperationMetadata operationMetadata)
        {
            OperationMetadata = operationMetadata;
            DisplayName = operationMetadata.Name;
        }
    }

    /// <summary>
    /// High-level application service for IHC labatory types of test applications, where the user can experiment with individual IHC services.
    /// This application serviceis is intended as a tech-agnostic backend for a GUI or console application where the user can manually select
    /// among individual services and operations, call them and print the result.
    /// </summary>
    public class LabAppService : AppServiceBase
    {
        public LabServiceItem[] Services { get; set; }

        private int _selectedServiceIndex;
        private int _selectedOperationIndex;

        public int SelectedServiceIndex
        {
            get
            {
                return _selectedServiceIndex;
            }
            set
            {
                _selectedServiceIndex = value;
                SelectedOperationIndex = Services[_selectedServiceIndex].SelectedOperationIndex;
            }
        }

        public int SelectedOperationIndex
        {
            get
            {
                return _selectedOperationIndex;
            }

            set
            {
                _selectedOperationIndex = value;
            }
        }
        
        public LabServiceOperationItem SelectedOperation
        {
            get
            {
                return Services[SelectedServiceIndex].OperationItems[SelectedOperationIndex];
            }
        }

        /// <summary>
        /// Create an LabService with no services. 
        /// Call configure post-construction to initialize.
        /// </summary>
        public LabAppService()
        {
            Services = [];
            this.SelectedServiceIndex = 0;
            this.SelectedOperationIndex = 0;
        }

        /// <summary>
        /// (Re)Configure LabService with specified settings and services. 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="serviceInterfaces"></param>
        public void Configure(IhcSettings settings, IIHCApiService[] serviceInterfaces)
        {
            Services = serviceInterfaces
                .Select(service => new LabServiceItem(service))
                .ToArray();

            this.SelectedServiceIndex = 0;
            this.SelectedOperationIndex = 0;
        }

        public object ExecuteOperation()
        {
            return null;
        }


    }
}