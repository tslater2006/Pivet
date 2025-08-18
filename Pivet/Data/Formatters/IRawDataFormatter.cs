using System;

namespace Pivet.Data.Formatters
{
    /// <summary>
    /// Interface for formatting raw data items into different output formats
    /// </summary>
    public interface IRawDataFormatter
    {
        /// <summary>
        /// Unique identifier for this formatter
        /// </summary>
        string FormatterID { get; }

        /// <summary>
        /// Human-readable name for this formatter
        /// </summary>
        string FormatName { get; }

        /// <summary>
        /// File extension to use for files created by this formatter (without the dot)
        /// </summary>
        string FileExtension { get; }

        /// <summary>
        /// Formats a raw data item into the desired output format
        /// </summary>
        /// <param name="item">The raw data item to format</param>
        /// <param name="filePath">The intended file path (for context, if needed)</param>
        /// <returns>The formatted content as a string</returns>
        string FormatData(RawDataItem item, string filePath);
    }
}