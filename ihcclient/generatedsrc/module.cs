﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Ihc.Soap.Module
{
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ServiceModel.ServiceContractAttribute(Namespace="utcs", ConfigurationName="Ihc.Soap.Module.ModuleService")]
    public interface ModuleService
    {
        
        [System.ServiceModel.OperationContractAttribute(Action="getSceneProjectInfo", ReplyAction="*")]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults=true)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName1> getSceneProjectInfoAsync(Ihc.Soap.Module.inputMessageName1 request);
        
        [System.ServiceModel.OperationContractAttribute(Action="storeSceneProject", ReplyAction="*")]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults=true)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName2> storeSceneProjectAsync(Ihc.Soap.Module.inputMessageName2 request);
        
        [System.ServiceModel.OperationContractAttribute(Action="getSceneProjectSegment", ReplyAction="*")]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults=true)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName3> getSceneProjectSegmentAsync(Ihc.Soap.Module.inputMessageName3 request);
        
        [System.ServiceModel.OperationContractAttribute(Action="storeSceneProjectSegment", ReplyAction="*")]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults=true)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName4> storeSceneProjectSegmentAsync(Ihc.Soap.Module.inputMessageName4 request);
        
        [System.ServiceModel.OperationContractAttribute(Action="getSceneProject", ReplyAction="*")]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults=true)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName5> getSceneProjectAsync(Ihc.Soap.Module.inputMessageName5 request);
        
        [System.ServiceModel.OperationContractAttribute(Action="clearAll", ReplyAction="*")]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults=true)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName6> clearAllAsync(Ihc.Soap.Module.inputMessageName6 request);
        
        [System.ServiceModel.OperationContractAttribute(Action="getSceneProjectSegmentationSize", ReplyAction="*")]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults=true)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName7> getSceneProjectSegmentationSizeAsync(Ihc.Soap.Module.inputMessageName7 request);
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="utcs")]
    public partial class WSSceneProjectInfo
    {
        
        private string nameField;
        
        private int sizeField;
        
        private string filepathField;
        
        private bool remoteField;
        
        private string versionField;
        
        private WSDate createdField;
        
        private WSDate lastmodifiedField;
        
        private string descriptionField;
        
        private long crcField;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=0)]
        public string name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=1)]
        public int size
        {
            get
            {
                return this.sizeField;
            }
            set
            {
                this.sizeField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=2)]
        public string filepath
        {
            get
            {
                return this.filepathField;
            }
            set
            {
                this.filepathField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=3)]
        public bool remote
        {
            get
            {
                return this.remoteField;
            }
            set
            {
                this.remoteField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=4)]
        public string version
        {
            get
            {
                return this.versionField;
            }
            set
            {
                this.versionField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true, Order=5)]
        public WSDate created
        {
            get
            {
                return this.createdField;
            }
            set
            {
                this.createdField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true, Order=6)]
        public WSDate lastmodified
        {
            get
            {
                return this.lastmodifiedField;
            }
            set
            {
                this.lastmodifiedField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=7)]
        public string description
        {
            get
            {
                return this.descriptionField;
            }
            set
            {
                this.descriptionField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=8)]
        public long crc
        {
            get
            {
                return this.crcField;
            }
            set
            {
                this.crcField = value;
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
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="utcs")]
    public partial class WSFile
    {
        
        private byte[] dataField;
        
        private string filenameField;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType="base64Binary", Order=0)]
        public byte[] data
        {
            get
            {
                return this.dataField;
            }
            set
            {
                this.dataField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=1)]
        public string filename
        {
            get
            {
                return this.filenameField;
            }
            set
            {
                this.filenameField = value;
            }
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class inputMessageName1
    {
        
        public inputMessageName1()
        {
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
        public Ihc.Soap.Module.WSSceneProjectInfo getSceneProjectInfo1;
        
        public outputMessageName1()
        {
        }
        
        public outputMessageName1(Ihc.Soap.Module.WSSceneProjectInfo getSceneProjectInfo1)
        {
            this.getSceneProjectInfo1 = getSceneProjectInfo1;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class inputMessageName2
    {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=0)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public Ihc.Soap.Module.WSFile storeSceneProject1;
        
        public inputMessageName2()
        {
        }
        
        public inputMessageName2(Ihc.Soap.Module.WSFile storeSceneProject1)
        {
            this.storeSceneProject1 = storeSceneProject1;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class outputMessageName2
    {
        
        public outputMessageName2()
        {
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class inputMessageName3
    {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=0)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public string getSceneProjectSegment1;
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=1)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public System.Nullable<int> getSceneProjectSegment2;
        
        public inputMessageName3()
        {
        }
        
        public inputMessageName3(string getSceneProjectSegment1, System.Nullable<int> getSceneProjectSegment2)
        {
            this.getSceneProjectSegment1 = getSceneProjectSegment1;
            this.getSceneProjectSegment2 = getSceneProjectSegment2;
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
        public Ihc.Soap.Module.WSFile getSceneProjectSegment3;
        
        public outputMessageName3()
        {
        }
        
        public outputMessageName3(Ihc.Soap.Module.WSFile getSceneProjectSegment3)
        {
            this.getSceneProjectSegment3 = getSceneProjectSegment3;
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
        public Ihc.Soap.Module.WSFile storeSceneProjectSegment1;
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=1)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public System.Nullable<bool> storeSceneProjectSegment2;
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=2)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public System.Nullable<bool> storeSceneProjectSegment3;
        
        public inputMessageName4()
        {
        }
        
        public inputMessageName4(Ihc.Soap.Module.WSFile storeSceneProjectSegment1, System.Nullable<bool> storeSceneProjectSegment2, System.Nullable<bool> storeSceneProjectSegment3)
        {
            this.storeSceneProjectSegment1 = storeSceneProjectSegment1;
            this.storeSceneProjectSegment2 = storeSceneProjectSegment2;
            this.storeSceneProjectSegment3 = storeSceneProjectSegment3;
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
        public System.Nullable<bool> storeSceneProjectSegment4;
        
        public outputMessageName4()
        {
        }
        
        public outputMessageName4(System.Nullable<bool> storeSceneProjectSegment4)
        {
            this.storeSceneProjectSegment4 = storeSceneProjectSegment4;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class inputMessageName5
    {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=0)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public string getSceneProject1;
        
        public inputMessageName5()
        {
        }
        
        public inputMessageName5(string getSceneProject1)
        {
            this.getSceneProject1 = getSceneProject1;
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
        public Ihc.Soap.Module.WSFile getSceneProject2;
        
        public outputMessageName5()
        {
        }
        
        public outputMessageName5(Ihc.Soap.Module.WSFile getSceneProject2)
        {
            this.getSceneProject2 = getSceneProject2;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class inputMessageName6
    {
        
        public inputMessageName6()
        {
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class outputMessageName6
    {
        
        public outputMessageName6()
        {
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class inputMessageName7
    {
        
        public inputMessageName7()
        {
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class outputMessageName7
    {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Namespace="utcs", Order=0)]
        [System.Xml.Serialization.XmlElementAttribute(IsNullable=true)]
        public System.Nullable<int> getSceneProjectSegmentationSize1;
        
        public outputMessageName7()
        {
        }
        
        public outputMessageName7(System.Nullable<int> getSceneProjectSegmentationSize1)
        {
            this.getSceneProjectSegmentationSize1 = getSceneProjectSegmentationSize1;
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    public interface ModuleServiceChannel : Ihc.Soap.Module.ModuleService, System.ServiceModel.IClientChannel
    {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Tools.ServiceModel.Svcutil", "2.0.0")]
    public partial class ModuleServiceClient : System.ServiceModel.ClientBase<Ihc.Soap.Module.ModuleService>, Ihc.Soap.Module.ModuleService
    {
        
        /// <summary>
        /// Implement this partial method to configure the service endpoint.
        /// </summary>
        /// <param name="serviceEndpoint">The endpoint to configure</param>
        /// <param name="clientCredentials">The client credentials</param>
        static partial void ConfigureEndpoint(System.ServiceModel.Description.ServiceEndpoint serviceEndpoint, System.ServiceModel.Description.ClientCredentials clientCredentials);
        
        public ModuleServiceClient() : 
                base(ModuleServiceClient.GetDefaultBinding(), ModuleServiceClient.GetDefaultEndpointAddress())
        {
            this.Endpoint.Name = EndpointConfiguration.ModuleServiceBindingPort.ToString();
            ConfigureEndpoint(this.Endpoint, this.ClientCredentials);
        }
        
        public ModuleServiceClient(EndpointConfiguration endpointConfiguration) : 
                base(ModuleServiceClient.GetBindingForEndpoint(endpointConfiguration), ModuleServiceClient.GetEndpointAddress(endpointConfiguration))
        {
            this.Endpoint.Name = endpointConfiguration.ToString();
            ConfigureEndpoint(this.Endpoint, this.ClientCredentials);
        }
        
        public ModuleServiceClient(EndpointConfiguration endpointConfiguration, string remoteAddress) : 
                base(ModuleServiceClient.GetBindingForEndpoint(endpointConfiguration), new System.ServiceModel.EndpointAddress(remoteAddress))
        {
            this.Endpoint.Name = endpointConfiguration.ToString();
            ConfigureEndpoint(this.Endpoint, this.ClientCredentials);
        }
        
        public ModuleServiceClient(EndpointConfiguration endpointConfiguration, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(ModuleServiceClient.GetBindingForEndpoint(endpointConfiguration), remoteAddress)
        {
            this.Endpoint.Name = endpointConfiguration.ToString();
            ConfigureEndpoint(this.Endpoint, this.ClientCredentials);
        }
        
        public ModuleServiceClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress)
        {
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName1> Ihc.Soap.Module.ModuleService.getSceneProjectInfoAsync(Ihc.Soap.Module.inputMessageName1 request)
        {
            return base.Channel.getSceneProjectInfoAsync(request);
        }
        
        public System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName1> getSceneProjectInfoAsync()
        {
            Ihc.Soap.Module.inputMessageName1 inValue = new Ihc.Soap.Module.inputMessageName1();
            return ((Ihc.Soap.Module.ModuleService)(this)).getSceneProjectInfoAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName2> Ihc.Soap.Module.ModuleService.storeSceneProjectAsync(Ihc.Soap.Module.inputMessageName2 request)
        {
            return base.Channel.storeSceneProjectAsync(request);
        }
        
        public System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName2> storeSceneProjectAsync(Ihc.Soap.Module.WSFile storeSceneProject1)
        {
            Ihc.Soap.Module.inputMessageName2 inValue = new Ihc.Soap.Module.inputMessageName2();
            inValue.storeSceneProject1 = storeSceneProject1;
            return ((Ihc.Soap.Module.ModuleService)(this)).storeSceneProjectAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName3> Ihc.Soap.Module.ModuleService.getSceneProjectSegmentAsync(Ihc.Soap.Module.inputMessageName3 request)
        {
            return base.Channel.getSceneProjectSegmentAsync(request);
        }
        
        public System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName3> getSceneProjectSegmentAsync(string getSceneProjectSegment1, System.Nullable<int> getSceneProjectSegment2)
        {
            Ihc.Soap.Module.inputMessageName3 inValue = new Ihc.Soap.Module.inputMessageName3();
            inValue.getSceneProjectSegment1 = getSceneProjectSegment1;
            inValue.getSceneProjectSegment2 = getSceneProjectSegment2;
            return ((Ihc.Soap.Module.ModuleService)(this)).getSceneProjectSegmentAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName4> Ihc.Soap.Module.ModuleService.storeSceneProjectSegmentAsync(Ihc.Soap.Module.inputMessageName4 request)
        {
            return base.Channel.storeSceneProjectSegmentAsync(request);
        }
        
        public System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName4> storeSceneProjectSegmentAsync(Ihc.Soap.Module.WSFile storeSceneProjectSegment1, System.Nullable<bool> storeSceneProjectSegment2, System.Nullable<bool> storeSceneProjectSegment3)
        {
            Ihc.Soap.Module.inputMessageName4 inValue = new Ihc.Soap.Module.inputMessageName4();
            inValue.storeSceneProjectSegment1 = storeSceneProjectSegment1;
            inValue.storeSceneProjectSegment2 = storeSceneProjectSegment2;
            inValue.storeSceneProjectSegment3 = storeSceneProjectSegment3;
            return ((Ihc.Soap.Module.ModuleService)(this)).storeSceneProjectSegmentAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName5> Ihc.Soap.Module.ModuleService.getSceneProjectAsync(Ihc.Soap.Module.inputMessageName5 request)
        {
            return base.Channel.getSceneProjectAsync(request);
        }
        
        public System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName5> getSceneProjectAsync(string getSceneProject1)
        {
            Ihc.Soap.Module.inputMessageName5 inValue = new Ihc.Soap.Module.inputMessageName5();
            inValue.getSceneProject1 = getSceneProject1;
            return ((Ihc.Soap.Module.ModuleService)(this)).getSceneProjectAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName6> Ihc.Soap.Module.ModuleService.clearAllAsync(Ihc.Soap.Module.inputMessageName6 request)
        {
            return base.Channel.clearAllAsync(request);
        }
        
        public System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName6> clearAllAsync()
        {
            Ihc.Soap.Module.inputMessageName6 inValue = new Ihc.Soap.Module.inputMessageName6();
            return ((Ihc.Soap.Module.ModuleService)(this)).clearAllAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName7> Ihc.Soap.Module.ModuleService.getSceneProjectSegmentationSizeAsync(Ihc.Soap.Module.inputMessageName7 request)
        {
            return base.Channel.getSceneProjectSegmentationSizeAsync(request);
        }
        
        public System.Threading.Tasks.Task<Ihc.Soap.Module.outputMessageName7> getSceneProjectSegmentationSizeAsync()
        {
            Ihc.Soap.Module.inputMessageName7 inValue = new Ihc.Soap.Module.inputMessageName7();
            return ((Ihc.Soap.Module.ModuleService)(this)).getSceneProjectSegmentationSizeAsync(inValue);
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
            if ((endpointConfiguration == EndpointConfiguration.ModuleServiceBindingPort))
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
            if ((endpointConfiguration == EndpointConfiguration.ModuleServiceBindingPort))
            {
                return new System.ServiceModel.EndpointAddress("http://localhost/ModuleService");
            }
            throw new System.InvalidOperationException(string.Format("Could not find endpoint with name \'{0}\'.", endpointConfiguration));
        }
        
        private static System.ServiceModel.Channels.Binding GetDefaultBinding()
        {
            return ModuleServiceClient.GetBindingForEndpoint(EndpointConfiguration.ModuleServiceBindingPort);
        }
        
        private static System.ServiceModel.EndpointAddress GetDefaultEndpointAddress()
        {
            return ModuleServiceClient.GetEndpointAddress(EndpointConfiguration.ModuleServiceBindingPort);
        }
        
        public enum EndpointConfiguration
        {
            
            ModuleServiceBindingPort,
        }
    }
}