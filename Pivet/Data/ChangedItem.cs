using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pivet.Data
{
    enum ChangedItemState
    {
        CREATE, DELETE
    }
    class ChangedItem
    {
        public string FilePath;
        public string OperatorId;
        public ChangedItemState State;
    }
}
