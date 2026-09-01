using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Text;
using System.Xml.XPath;

namespace Ihc.IOExtractor {
    /**
    * IHC project loader.
    */
    public class IhcProjectLoader {
        private readonly string projectFile;

        public IhcProjectLoader(string projectFile) {
            this.projectFile=projectFile;
        }

        public IOMeta[] GetIO(IOType ioType) {
            var result = new List<IOMeta>(100);

            XmlDocument Dom = new XmlDocument();
            using (var streamReader = new StreamReader(projectFile, Encoding.GetEncoding("ISO-8859-1"))) {
                Dom.Load(streamReader);
            }

            XmlElement documentElement = Dom.DocumentElement
                ?? throw new InvalidDataException($"Project file '{projectFile}' holds no document element.");
            XPathNavigator navigator = documentElement.CreateNavigator()
                ?? throw new InvalidDataException($"Project file '{projectFile}' element <{documentElement.Name}> cannot be navigated.");

            string datalineName;
            switch (ioType) {
                case IOType.Input: datalineName = "dataline_input"; break;
                case IOType.Output: datalineName = "dataline_output"; break;
                default: throw new ArgumentOutOfRangeException(nameof(ioType), ioType, "Unknown iotype");
            }

            var inputNodes = navigator.Select("//group/product_dataline/"+datalineName);

            foreach (XPathNavigator item in inputNodes)
            {
                XPathNavigator parentNavigator = item.Clone();
                parentNavigator.MoveToParent();

                int productId=Convert.ToInt32(RequiredAttribute(parentNavigator, "id").Substring(1), 16);
                string productName = RequiredAttribute(parentNavigator, "name");
                string productPosition =RequiredAttribute(parentNavigator, "position");
                string productNote =RequiredAttribute(parentNavigator, "note");

                parentNavigator.MoveToParent();
                int groupId=Convert.ToInt32(RequiredAttribute(parentNavigator, "id").Substring(1), 16);
                string groupName = RequiredAttribute(parentNavigator, "name");

                int id=Convert.ToInt32(RequiredAttribute(item, "id").Substring(1), 16);
                string name=RequiredAttribute(item, "name");
                string note =RequiredAttribute(item, "note");

                result.Add(new IOMeta() { ResourceId = id, ProductId = productId, GroupId = groupId, GroupName = groupName, DatalineName = name, ProductName = productName, ProductPosition = productPosition, ProductNote = productNote, DatalineNote = note });
            }

            return result.ToArray<IOMeta>();
        }

        /**
        * Reads an attribute every IO entry must carry. A project missing one is malformed rather
        * than empty, so it is reported by attribute and element name instead of failing later as a
        * NullReferenceException with nothing in it to identify the offending node.
        */
        private static string RequiredAttribute(XPathNavigator element, string attributeName) {
            XPathNavigator attribute = element.SelectSingleNode("@" + attributeName)
                ?? throw new InvalidDataException($"Element <{element.Name}> has no {attributeName} attribute.");
            return attribute.Value;
        }
    }
}