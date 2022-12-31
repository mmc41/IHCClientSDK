using System.Xml.Serialization;
using System;
using System.Xml;
using System.IO;
using System.Text;
using System.Linq;

using Ihc;

namespace Ihc {
  /**
   * Responsible for low-level serialization/de-serialization of IHC soap requests/responses
   * so we can communicate with the IHC server without any formal soap libraries involved
   * as this is not (currently) supported by dot net core.
   */
  public class Serialization {
    public static string SerializeXml<A>(A x) where A : class {    
      try {    
        var attrs = new XmlAttributeOverrides();
        var attr = new XmlAttributes();
        var typ = new XmlTypeAttribute();
        typ.Namespace = "utcs";

        attr.XmlType = typ;
        var genericTypes = typeof(A).GetGenericArguments();
        foreach(var genericType in genericTypes)
            attrs.Add(genericType, attr);
 
        // Retrive ns specifiation from object if it is there and use it 
        // explictly for the serializer - as a side effect this will also
        // cause build-in xsi and xsd namespaces to be omitted by default 
        // as we would like for simple requests.
        var optNs = typeof(A).GetProperties()
                    .Where(p => Attribute.IsDefined(p, typeof(XmlNamespaceDeclarationsAttribute)))
                    .Take(1)
                    .Select(p => p.GetValue(x))
                    .FirstOrDefault(p => true) as XmlSerializerNamespaces;

    
        var xmlSerializer = new XmlSerializer(typeof(A), attrs);
        var settings = new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = true, Encoding = new UTF8Encoding(false), NamespaceHandling = NamespaceHandling.OmitDuplicates };

        using (var stream = new MemoryStream())
        using (var writer = XmlWriter.Create(stream, settings)) {
            if (optNs!=null) {
                xmlSerializer.Serialize(writer, x, optNs);
            } else {
                xmlSerializer.Serialize(writer, x);
            }

            writer.Flush();
            var result = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            return result;
        }
      } catch (Exception ex) {
        throw new ErrorWithCodeException(Errors.XML_SERIALIZE_ERROR, ex.Message, ex);
      }
    }

    public static A DeserializeXml<A>(string xml) where A : class {
        try {
            var attrs = new XmlAttributeOverrides();
            var attr = new XmlAttributes();
            var typ = new XmlTypeAttribute();
            typ.Namespace = "utcs";

            attr.XmlType = typ;

            var genericTypes = typeof(A).GetGenericArguments();
            foreach(var genericType in genericTypes)
                attrs.Add(genericType, attr);

            var xmlSerializer = new XmlSerializer(typeof(A), attrs, genericTypes, null, null);
            using (var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(xml)))
            {
                var result = xmlSerializer.Deserialize(stream);
                return result as A;
            }
        } catch (Exception ex) {
            throw new ErrorWithCodeException(Errors.XML_DESERIALIZE_ERROR, ex.Message, ex);
        }
    }
  }
}