using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pivet.Data.Connection
{
    interface IConnectionProvider
    {
        void SetParameters(ConnectionConfig parms);
        Tuple<OracleConnection,bool,string> GetConnection();
    }
}
