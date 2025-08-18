using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace Pivet.Data.Formatters
{
    /// <summary>
    /// Default JSON formatter for raw data items - maintains the existing JSON output format
    /// </summary>
    public class JsonRawDataFormatter : IRawDataFormatter
    {
        public string FormatterID => "JsonFormatter";
        public string FormatName => "JSON";
        public string FileExtension => "json";

        public string FormatData(RawDataItem item, string filePath)
        {
            var itemAsJObject = JObject.FromObject(item);
            CanonicalizeItem(itemAsJObject);
            return itemAsJObject.ToString(Formatting.Indented);
        }

        /// <summary>
        /// Canonicalizes JSON tokens by sorting properties alphabetically and handling special cases
        /// This method is extracted from the original RawDataProcessor.CanonicalizeItem
        /// </summary>
        public static void CanonicalizeItem(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = (JObject)token;
                    var properties = obj.Properties().ToList();
                    
                    // Special handling for objects with TableName property (related tables)
                    var tableNameProperty = properties.FirstOrDefault(p => p.Name == "TableName");
                    if (tableNameProperty != null)
                    {
                        // Create ordered list with TableName first, then others alphabetically
                        var orderedProperties = new System.Collections.Generic.List<JProperty>();
                        
                        // Add TableName first
                        orderedProperties.Add(tableNameProperty);
                        
                        // Add all other properties alphabetically
                        var otherProps = properties.Where(p => p.Name != "TableName")
                                                  .OrderBy(p => p.Name, StringComparer.Ordinal)
                                                  .ToList();
                        orderedProperties.AddRange(otherProps);
                        
                        // Rebuild object in desired order
                        obj.RemoveAll();
                        foreach (var prop in orderedProperties)
                        {
                            CanonicalizeItem(prop.Value);
                            obj.Add(prop);
                        }
                    }
                    else
                    {
                        // Standard alphabetical sorting for all other objects
                        properties.Sort((p1, p2) => string.CompareOrdinal(p1.Name, p2.Name));

                        obj.RemoveAll();

                        foreach (var property in properties)
                        {
                            CanonicalizeItem(property.Value);
                            obj.Add(property);
                        }
                    }
                    break;

                case JTokenType.Array:
                    var array = (JArray)token;
                    foreach (var item in array)
                    {
                        CanonicalizeItem(item);
                    }
                    var sortedArray = new JArray(array.OrderBy(t => t.ToString(), StringComparer.Ordinal));
                    array.Replace(sortedArray);
                    break;
            }
        }
    }
}