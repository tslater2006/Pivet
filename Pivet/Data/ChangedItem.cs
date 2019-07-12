using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pivet.Data
{
    public class ChangedItem
    {
        public readonly string FilePath;
        public readonly string RepoPath;
        public readonly string OperatorId;

        public ChangedItem(string FilePath, string OperatorId)
        {
            this.FilePath = FilePath;
	        this.RepoPath = FilePath.Replace(JobRunner.profileRepoPath + Path.DirectorySeparatorChar, "").Replace("\\", "/");
	        this.OperatorId = OperatorId;
        }
    }
}
