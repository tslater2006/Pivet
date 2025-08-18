using System;
using System.Xml;
using System.Xml.Linq;
using System.Linq;

namespace Pivet.Data.Formatters
{
    /// <summary>
    /// XML formatter for raw data items - converts JSON-like structures to XML format
    /// </summary>
    public class XmlRawDataFormatter : IRawDataFormatter
    {
        public string FormatterID => "XmlFormatter";
        public string FormatName => "XML";
        public string FileExtension => "xml";

        public string FormatData(RawDataItem item, string filePath)
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null));
            var root = new XElement("RawDataItem");
            
            // Add Fields section
            var fieldsElement = new XElement("Fields");
            foreach (var field in item.Fields.OrderBy(f => f.Key))
            {
                var fieldElement = new XElement("Field");
                fieldElement.SetAttributeValue("name", field.Key);
                fieldElement.SetValue(field.Value?.ToString() ?? "");
                fieldsElement.Add(fieldElement);
            }
            root.Add(fieldsElement);
            
            // Add RelatedTables section if present
            if (item.RelatedTables != null && item.RelatedTables.Count > 0)
            {
                var relatedTablesElement = new XElement("RelatedTables");
                foreach (var relatedTable in item.RelatedTables.OrderBy(rt => rt.TableName))
                {
                    var tableElement = new XElement("Table");
                    tableElement.SetAttributeValue("name", relatedTable.TableName);
                    
                    foreach (var row in relatedTable.Rows)
                    {
                        var rowElement = new XElement("Row");
                        foreach (var column in row.OrderBy(c => c.Key))
                        {
                            var columnElement = new XElement("Column");
                            columnElement.SetAttributeValue("name", column.Key);
                            columnElement.SetValue(column.Value?.ToString() ?? "");
                            rowElement.Add(columnElement);
                        }
                        tableElement.Add(rowElement);
                    }
                    
                    relatedTablesElement.Add(tableElement);
                }
                root.Add(relatedTablesElement);
            }
            
            doc.Add(root);
            return doc.ToString();
        }
    }
}