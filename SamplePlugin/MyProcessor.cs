using System;
using Oracle.ManagedDataAccess.Client;
using Pivet;
using Pivet.Data;

namespace SamplePlugin
{
    public class MyProcessor : IDataProcessor
    {
        public string ItemName => "MyItem";

        public string ProcessorID => "MyItemProcessor";

        public event ProgressHandler ProgressChanged;

        public int SaveItems(OracleConnection conn, FilterConfig filters, string outputFolder)
        {
            throw new NotImplementedException();
        }

        public void Cleanup(string rootFolder)
        {
            throw new NotImplementedException();
        }

        public System.Collections.Generic.List<ChangedItem> SaveToDisk(string rootFolder)
        {
            throw new NotImplementedException();
        }
    }
}
