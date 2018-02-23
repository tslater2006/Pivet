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

        public int LoadItems(OracleConnection conn, FilterConfig filters)
        {
            throw new NotImplementedException();
        }

        public void ProcessDeletes(string rootFolder)
        {
            throw new NotImplementedException();
        }

        public System.Collections.Generic.List<ChangedItem> SaveToDisk(string rootFolder)
        {
            throw new NotImplementedException();
        }
    }
}
