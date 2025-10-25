using System.Xml.Serialization;

namespace Ihc.Envelope {
   /// <summary>
   /// This class represents an empty SOAP request message. Use .NET XmlSerializer to generate the corresponding XML.
   /// </summary>
   [XmlRoot(ElementName="Envelope", Namespace ="http://schemas.xmlsoap.org/soap/envelope/", IsNullable=false)]
   public class EmptyRequestEnvelope {
      [XmlElement(Order = 1, IsNullable=false)]
      public string Header { get; set; }

      [XmlElement(Order = 2, IsNullable=false)]
      public string Body { get; set; }

      private XmlSerializerNamespaces xmlns;

      [XmlNamespaceDeclarations]
      public XmlSerializerNamespaces Xmlns 
      {
         get { return xmlns; }
         set { xmlns = value; }
      }

      public EmptyRequestEnvelope() {
         this.Body = string.Empty;
         this.Header = string.Empty;

         xmlns = new XmlSerializerNamespaces();
         xmlns.Add("utcs", "utcs");
         xmlns.Add("soapenv", "http://schemas.xmlsoap.org/soap/envelope/");
      }
   };

   /// <summary>
   /// This class represents a complete SOAP request for a specific request message (which unlike this class is auto-generated).
   /// Use .NET XmlSerializer to generate the corresponding XML.
   /// Note: Remember to configure the serializer to use utcs namespace for the T data part.
   /// </summary>
   /// <typeparam name="T">Type of the request body.</typeparam>
   [XmlRoot(ElementName="Envelope", Namespace ="http://schemas.xmlsoap.org/soap/envelope/", IsNullable=false)]
   public class RequestEnvelope<T> {
      [XmlElement(Order = 1, IsNullable=false)]
      public string Header { get; set; }

      [XmlElement(Order = 2, IsNullable=false)]
      public T Body;

      private XmlSerializerNamespaces xmlns;

      [XmlNamespaceDeclarations]
      public XmlSerializerNamespaces Xmlns 
      {
         get { return xmlns; }
         set { xmlns = value; }
      }

      public RequestEnvelope(T body) : this() {
         this.Body = body;
         this.Header = string.Empty;
      }

      public RequestEnvelope() {
         xmlns = new XmlSerializerNamespaces();
         xmlns.Add("utcs", "utcs");
         xmlns.Add("soapenv", "http://schemas.xmlsoap.org/soap/envelope/");
      }
   };

   /// <summary>
   /// This class represents a complete SOAP response for a specific response message (which unlike this class is auto-generated).
   /// Use .NET XmlSerializer to generate the corresponding XML.
   /// Note: Remember to configure the serializer to use utcs namespace for the T data part.
   /// </summary>
   /// <typeparam name="T">Type of the response body.</typeparam>
   [XmlRoot(ElementName="Envelope", Namespace ="http://schemas.xmlsoap.org/soap/envelope/", IsNullable=false)]
   public class ResponseEnvelope<T> {
      [XmlElement(Order = 1, IsNullable=false)]
      public T Body;

      private XmlSerializerNamespaces xmlns;

      [XmlNamespaceDeclarations]
      public XmlSerializerNamespaces Xmlns 
      {
         get { return xmlns; }
         set { xmlns = value; }
      }

      public ResponseEnvelope(T body) : this() {
         this.Body = body;
      }

      public ResponseEnvelope() {
         xmlns = new XmlSerializerNamespaces();
         xmlns.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");
         xmlns.Add("SOAP-ENV", "http://schemas.xmlsoap.org/soap/envelope/");
         xmlns.Add("utcs", "utcs");
         xmlns.Add("xsd", "http://www.w3.org/2001/XMLSchema");
      }
   };
}
