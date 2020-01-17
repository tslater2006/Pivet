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
        static Regex removeInvalidChars = new Regex(String.Format("[{0}]", Regex.Escape(new string(Path.GetInvalidFileNameChars()))), RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        public static string CleanFileName(this string s)
        {
            s = removeInvalidChars.Replace(s, "");
            return s;
        }
    }
    public interface IDataProcessor
    {
        //int LoadItems(OracleConnection conn, FilterConfig filters, int modifyThreshold, VersionState versionState);
        int SaveItems(OracleConnection conn, FilterConfig filters, string outputFolder);
        void Cleanup(string rootFolder);

        event ProgressHandler ProgressChanged;
        
        string ItemName { get; }

        string ProcessorID { get; }
    }
}
