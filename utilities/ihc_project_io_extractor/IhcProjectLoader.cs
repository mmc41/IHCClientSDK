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

            XPathNavigator navigator = Dom.DocumentElement.CreateNavigator();

            string datalineName;
            switch (ioType) {
                case IOType.Input: datalineName = "dataline_input"; break;
                case IOType.Output: datalineName = "dataline_output"; break;
                default: throw new Exception("Unknown iotype " + ioType);
            }

            var inputNodes = navigator.Select("//group/product_dataline/"+datalineName);

            foreach (XPathNavigator item in inputNodes)
            {
                XPathNavigator parentNavigator = item.Clone();
                parentNavigator.MoveToParent();

                int productId=Convert.ToInt32(parentNavigator.SelectSingleNode("@id").Value.Substring(1), 16);
                string productName = parentNavigator.SelectSingleNode("@name").Value;
                string productPosition =parentNavigator.SelectSingleNode("@position").Value;
                string productNote =parentNavigator.SelectSingleNode("@note").Value;

                parentNavigator.MoveToParent();
                int groupId=Convert.ToInt32(parentNavigator.SelectSingleNode("@id").Value.Substring(1), 16);
                string groupName = parentNavigator.SelectSingleNode("@name").Value;

                int id=Convert.ToInt32(item.SelectSingleNode("@id").Value.Substring(1), 16);
                string name=item.SelectSingleNode("@name").Value;
                string note =item.SelectSingleNode("@note").Value;

                result.Add(new IOMeta() { ResourceId = id, ProductId = productId, GroupId = groupId, GroupName = groupName, DatalineName = name, ProductName = productName, ProductPosition = productPosition, ProductNote = productNote, DatalineNote = note });
            }

            return result.ToArray<IOMeta>();
        }
    }
}