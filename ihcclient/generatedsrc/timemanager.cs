﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Ihc.Soap.Timemanager
{
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ServiceModel.ServiceContractAttribute(Namespace="utcs", ConfigurationName="Ihc.Soap.Timemanager.TimeManagerService")]
    public interface TimeManagerService
    {
        
        [System.ServiceModel.OperationContractAttribute(Action="getTimeFromServer", ReplyAction="*")]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults=true)]
        System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName1> getTimeFromServerAsync(Ihc.Soap.Timemanager.inputMessageName1 request);
        
        [System.ServiceModel.OperationContractAttribute(Action="getCurrentLocalTime", ReplyAction="*")]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults=true)]
        System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName2> getCurrentLocalTimeAsync(Ihc.Soap.Timemanager.inputMessageName2 request);
        
        [System.ServiceModel.OperationContractAttribute(Action="getSettings", ReplyAction="*")]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults=true)]
        System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName3> getSettingsAsync(Ihc.Soap.Timemanager.inputMessageName3 request);
        
        [System.ServiceModel.OperationContractAttribute(Action="setSettings", ReplyAction="*")]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults=true)]
        System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName4> setSettingsAsync(Ihc.Soap.Timemanager.inputMessageName4 request);
        
        [System.ServiceModel.OperationContractAttribute(Action="getUptime", ReplyAction="*")]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults=true)]
        System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName5> getUptimeAsync(Ihc.Soap.Timemanager.inputMessageName5 request);
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="utcs")]
    public partial class WSTimeServerConnectionResult
    {
        
        private bool connectionWasSuccessfulField;
        
        private long dateFromServerField;
        
        private bool connectionFailedDueToUnknownHostField;
        
        private bool connectionFailedDueToOtherErrorsField;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=0)]
        public bool connectionWasSuccessful
        {
            get
            {
                return this.connectionWasSuccessfulField;
            }
            set
            {
                this.connectionWasSuccessfulField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=1)]
        public long dateFromServer
        {
            get
            {
                return this.dateFromServerField;
            }
            set
            {
                this.dateFromServerField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=2)]
        public bool connectionFailedDueToUnknownHost
        {
            get
            {
                return this.connectionFailedDueToUnknownHostField;
            }
            set
            {
                this.connectionFailedDueToUnknownHostField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=3)]
        public bool connectionFailedDueToOtherErrors
        {
            get
            {
                return this.connectionFailedDueToOtherErrorsField;
            }
            set
            {
                this.connectionFailedDueToOtherErrorsField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="utcs")]
    public partial class WSTimeManagerSettings
    {
        
        private bool synchroniseTimeAgainstServerField;
        
        private bool useDSTField;
        
        private int gmtOffsetInHoursField;
        
        private string serverNameField;
        
        private int syncIntervalInHoursField;
        
        private WSDate timeAndDateInUTCField;
        
        private bool online_calendar_update_onlineField;
        
        private string online_calendar_countryField;
        
        private int online_calendar_valid_untilField;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=0)]
        public bool synchroniseTimeAgainstServer
        {
            get
            {
                return this.synchroniseTimeAgainstServerField;
            }
            set
            {
                this.synchroniseTimeAgainstServerField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=1)]
        public bool useDST
        {
            get
            {
                return this.useDSTField;
            }
            set
            {
                this.useDSTField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=2)]
        public int gmtOffsetInHours
        {
            get
            {
                return this.gmtOffsetInHoursField;
            }
            set
            {
                this.gmtOffsetInHoursField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=3)]
        public string serverName
        {
            get
            {
                return this.serverNameField;
            }
            set
            {
                this.serverNameField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=4)]
        public int syncIntervalInHours
        {
            get
            {
                return this.syncIntervalInHoursField;
            }
            set
            {
                this.syncIntervalInHoursField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true, Order=5)]
        public WSDate timeAndDateInUTC
        {
            get
            {
                return this.timeAndDateInUTCField;
            }
            set
            {
                this.timeAndDateInUTCField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=6)]
        public bool online_calendar_update_online
        {
            get
            {
                return this.online_calendar_update_onlineField;
            }
            set
            {
                this.online_calendar_update_onlineField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=7)]
        public string online_calendar_country
        {
            get
            {
                return this.online_calendar_countryField;
            }
            set
            {
                this.online_calendar_countryField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=8)]
        public int online_calendar_valid_until
        {
            get
            {
                return this.online_calendar_valid_untilField;
            }
            set
            {
                this.online_calendar_valid_untilField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="utcs")]
    public partial class WSDate
    {
        
        private int monthWithJanuaryAsOneField;
        
        private int dayField;
        
        private int hoursField;
        
        private int minutesField;
        
        private int secondsField;
        
        private int yearField;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=0)]
        public int monthWithJanuaryAsOne
        {
            get
            {
                return this.monthWithJanuaryAsOneField;
            }
            set
            {
                this.monthWithJanuaryAsOneField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=1)]
        public int day
        {
            get
            {
                return this.dayField;
            }
            set
            {
                this.dayField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=2)]
        public int hours
        {
            get
            {
                return this.hoursField;
            }
            set
            {
                this.hoursField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=3)]
        public int minutes
        {
            get
            {
                return this.minutesField;
            }
            set
            {
                this.minutesField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=4)]
        public int seconds
        {
            get
            {
                return this.secondsField;
            }
            set
            {
                this.secondsField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=5)]
        public int year
        {
            get
            {
                return this.yearField;
            }
            set
            {
                this.yearField = value;
            }
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class inputMessageName1
    {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=0)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public string getTimeFromServer1;
        
        public inputMessageName1()
        {
        }
        
        public inputMessageName1(string getTimeFromServer1)
        {
            this.getTimeFromServer1 = getTimeFromServer1;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class outputMessageName1
    {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=0)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public Ihc.Soap.Timemanager.WSTimeServerConnectionResult getTimeFromServer2;
        
        public outputMessageName1()
        {
        }
        
        public outputMessageName1(Ihc.Soap.Timemanager.WSTimeServerConnectionResult getTimeFromServer2)
        {
            this.getTimeFromServer2 = getTimeFromServer2;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class inputMessageName2
    {
        
        public inputMessageName2()
        {
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class outputMessageName2
    {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=0)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public Ihc.Soap.Timemanager.WSDate getCurrentLocalTime1;
        
        public outputMessageName2()
        {
        }
        
        public outputMessageName2(Ihc.Soap.Timemanager.WSDate getCurrentLocalTime1)
        {
            this.getCurrentLocalTime1 = getCurrentLocalTime1;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class inputMessageName3
    {
        
        public inputMessageName3()
        {
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class outputMessageName3
    {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=0)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public Ihc.Soap.Timemanager.WSTimeManagerSettings getSettings1;
        
        public outputMessageName3()
        {
        }
        
        public outputMessageName3(Ihc.Soap.Timemanager.WSTimeManagerSettings getSettings1)
        {
            this.getSettings1 = getSettings1;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class inputMessageName4
    {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=0)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public Ihc.Soap.Timemanager.WSTimeManagerSettings setSettings1;
        
        public inputMessageName4()
        {
        }
        
        public inputMessageName4(Ihc.Soap.Timemanager.WSTimeManagerSettings setSettings1)
        {
            this.setSettings1 = setSettings1;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class outputMessageName4
    {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=0)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public System.Nullable<int> setSettings2;
        
        public outputMessageName4()
        {
        }
        
        public outputMessageName4(System.Nullable<int> setSettings2)
        {
            this.setSettings2 = setSettings2;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class inputMessageName5
    {
        
        public inputMessageName5()
        {
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class outputMessageName5
    {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=0)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public System.Nullable<long> getUptime1;
        
        public outputMessageName5()
        {
        }
        
        public outputMessageName5(System.Nullable<long> getUptime1)
        {
            this.getUptime1 = getUptime1;
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    public interface TimeManagerServiceChannel : Ihc.Soap.Timemanager.TimeManagerService, System.ServiceModel.IClientChannel
    {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    public partial class TimeManagerServiceClient : System.ServiceModel.ClientBase<Ihc.Soap.Timemanager.TimeManagerService>, Ihc.Soap.Timemanager.TimeManagerService
    {
        
        /// <summary>
        /// Implement this partial method to configure the service endpoint.
        /// </summary>
        /// <param name="serviceEndpoint">The endpoint to configure</param>
        /// <param name="clientCredentials">The client credentials</param>
        static partial void ConfigureEndpoint(System.ServiceModel.Description.ServiceEndpoint serviceEndpoint, System.ServiceModel.Description.ClientCredentials clientCredentials);
        
        public TimeManagerServiceClient() : 
                base(TimeManagerServiceClient.GetDefaultBinding(), TimeManagerServiceClient.GetDefaultEndpointAddress())
        {
            this.Endpoint.Name = EndpointConfiguration.TimeManagerServiceBindingPort.ToString();
            ConfigureEndpoint(this.Endpoint, this.ClientCredentials);
        }
        
        public TimeManagerServiceClient(EndpointConfiguration endpointConfiguration) : 
                base(TimeManagerServiceClient.GetBindingForEndpoint(endpointConfiguration), TimeManagerServiceClient.GetEndpointAddress(endpointConfiguration))
        {
            this.Endpoint.Name = endpointConfiguration.ToString();
            ConfigureEndpoint(this.Endpoint, this.ClientCredentials);
        }
        
        public TimeManagerServiceClient(EndpointConfiguration endpointConfiguration, string remoteAddress) : 
                base(TimeManagerServiceClient.GetBindingForEndpoint(endpointConfiguration), new System.ServiceModel.EndpointAddress(remoteAddress))
        {
            this.Endpoint.Name = endpointConfiguration.ToString();
            ConfigureEndpoint(this.Endpoint, this.ClientCredentials);
        }
        
        public TimeManagerServiceClient(EndpointConfiguration endpointConfiguration, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(TimeManagerServiceClient.GetBindingForEndpoint(endpointConfiguration), remoteAddress)
        {
            this.Endpoint.Name = endpointConfiguration.ToString();
            ConfigureEndpoint(this.Endpoint, this.ClientCredentials);
        }
        
        public TimeManagerServiceClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress)
        {
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName1> Ihc.Soap.Timemanager.TimeManagerService.getTimeFromServerAsync(Ihc.Soap.Timemanager.inputMessageName1 request)
        {
            return base.Channel.getTimeFromServerAsync(request);
        }
        
        public System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName1> getTimeFromServerAsync(string getTimeFromServer1)
        {
            Ihc.Soap.Timemanager.inputMessageName1 inValue = new Ihc.Soap.Timemanager.inputMessageName1();
            inValue.getTimeFromServer1 = getTimeFromServer1;
            return ((Ihc.Soap.Timemanager.TimeManagerService)(this)).getTimeFromServerAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName2> Ihc.Soap.Timemanager.TimeManagerService.getCurrentLocalTimeAsync(Ihc.Soap.Timemanager.inputMessageName2 request)
        {
            return base.Channel.getCurrentLocalTimeAsync(request);
        }
        
        public System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName2> getCurrentLocalTimeAsync()
        {
            Ihc.Soap.Timemanager.inputMessageName2 inValue = new Ihc.Soap.Timemanager.inputMessageName2();
            return ((Ihc.Soap.Timemanager.TimeManagerService)(this)).getCurrentLocalTimeAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName3> Ihc.Soap.Timemanager.TimeManagerService.getSettingsAsync(Ihc.Soap.Timemanager.inputMessageName3 request)
        {
            return base.Channel.getSettingsAsync(request);
        }
        
        public System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName3> getSettingsAsync()
        {
            Ihc.Soap.Timemanager.inputMessageName3 inValue = new Ihc.Soap.Timemanager.inputMessageName3();
            return ((Ihc.Soap.Timemanager.TimeManagerService)(this)).getSettingsAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName4> Ihc.Soap.Timemanager.TimeManagerService.setSettingsAsync(Ihc.Soap.Timemanager.inputMessageName4 request)
        {
            return base.Channel.setSettingsAsync(request);
        }
        
        public System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName4> setSettingsAsync(Ihc.Soap.Timemanager.WSTimeManagerSettings setSettings1)
        {
            Ihc.Soap.Timemanager.inputMessageName4 inValue = new Ihc.Soap.Timemanager.inputMessageName4();
            inValue.setSettings1 = setSettings1;
            return ((Ihc.Soap.Timemanager.TimeManagerService)(this)).setSettingsAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName5> Ihc.Soap.Timemanager.TimeManagerService.getUptimeAsync(Ihc.Soap.Timemanager.inputMessageName5 request)
        {
            return base.Channel.getUptimeAsync(request);
        }
        
        public System.Threading.Tasks.Task<Ihc.Soap.Timemanager.outputMessageName5> getUptimeAsync()
        {
            Ihc.Soap.Timemanager.inputMessageName5 inValue = new Ihc.Soap.Timemanager.inputMessageName5();
            return ((Ihc.Soap.Timemanager.TimeManagerService)(this)).getUptimeAsync(inValue);
        }
        
        public virtual System.Threading.Tasks.Task OpenAsync()
        {
            return System.Threading.Tasks.Task.Factory.FromAsync(((System.ServiceModel.ICommunicationObject)(this)).BeginOpen(null, null), new System.Action<System.IAsyncResult>(((System.ServiceModel.ICommunicationObject)(this)).EndOpen));
        }
        
        public virtual System.Threading.Tasks.Task CloseAsync()
        {
            return System.Threading.Tasks.Task.Factory.FromAsync(((System.ServiceModel.ICommunicationObject)(this)).BeginClose(null, null), new System.Action<System.IAsyncResult>(((System.ServiceModel.ICommunicationObject)(this)).EndClose));
        }
        
        private static System.ServiceModel.Channels.Binding GetBindingForEndpoint(EndpointConfiguration endpointConfiguration)
        {
            if ((endpointConfiguration == EndpointConfiguration.TimeManagerServiceBindingPort))
            {
                System.ServiceModel.BasicHttpBinding result = new System.ServiceModel.BasicHttpBinding();
                result.MaxBufferSize = int.MaxValue;
                result.ReaderQuotas = System.Xml.XmlDictionaryReaderQuotas.Max;
                result.MaxReceivedMessageSize = int.MaxValue;
                result.AllowCookies = true;
                return result;
            }
            throw new System.InvalidOperationException(string.Format("Could not find endpoint with name \'{0}\'.", endpointConfiguration));
        }
        
        private static System.ServiceModel.EndpointAddress GetEndpointAddress(EndpointConfiguration endpointConfiguration)
        {
            if ((endpointConfiguration == EndpointConfiguration.TimeManagerServiceBindingPort))
            {
                return new System.ServiceModel.EndpointAddress("http://localhost/TimeManagerService");
            }
            throw new System.InvalidOperationException(string.Format("Could not find endpoint with name \'{0}\'.", endpointConfiguration));
        }
        
        private static System.ServiceModel.Channels.Binding GetDefaultBinding()
        {
            return TimeManagerServiceClient.GetBindingForEndpoint(EndpointConfiguration.TimeManagerServiceBindingPort);
        }
        
        private static System.ServiceModel.EndpointAddress GetDefaultEndpointAddress()
        {
            return TimeManagerServiceClient.GetEndpointAddress(EndpointConfiguration.TimeManagerServiceBindingPort);
        }
        
        public enum EndpointConfiguration
        {
            
            TimeManagerServiceBindingPort,
        }
    }
}