using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Pivet.Data
{
    public delegate void ProgressHandler(ProgressEvent evt);

    public class ProgressEvent : EventArgs
    {
        public double Progress { get; set; }

    }
    public static class FilenameUtils
    {
        static Regex removeInvalidChars = new Regex(String.Format("[{0}]", Regex.Escape(new string(Path.GetInvalidFileNameChars().Except(new char[] { '/', '\\' }).ToArray()))), RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        public static string CleanFilePath(this string s)
        {
            s = removeInvalidChars.Replace(s, "");
            return s;
        }
    }
    public interface IDataProcessor
    {
        //int LoadItems(OracleConnection conn, FilterConfig filters, int modifyThreshold, VersionState versionState);
        int LoadItems(OracleConnection conn, FilterConfig filters);
        void ProcessDeletes(string rootFolder);
        List<ChangedItem> SaveToDisk(string rootFolder);

        event ProgressHandler ProgressChanged;
        
        string ItemName { get; }

        string ProcessorID { get; }
    }
}
