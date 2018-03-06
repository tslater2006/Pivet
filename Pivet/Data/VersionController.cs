using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using LibGit2Sharp;
namespace Pivet.Data
{
    internal class VersionController
    {
        private Repository _repository;
        RepositoryConfig config;
        private string _repoBase;
        double lastProgress;
        internal void InitRepository(string path, RepositoryConfig config)
        {
            this.config = config;
            Logger.Write("Initializing Repository.");
            _repoBase = path;
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }

            if (Directory.Exists(path + Path.DirectorySeparatorChar + ".git"))
            {
                Logger.Write("Repository found, opening.");
                /* already a git repo */
                _repository = new Repository(_repoBase);

            }
            else
            {
                Repository.Init(_repoBase);
                _repository = new Repository(_repoBase);
            }

        }

        private void ReportProgress(double progress)
        {
            if (Program.ShowProgress)
            {
                if (lastProgress != progress)
                {
                    Console.CursorLeft = 0;
                    Console.CursorTop--;

                    Console.WriteLine("Progress: " + string.Format("{0:N2}%", progress));
                    lastProgress = progress;
                }
            }
        }

        internal void ProcessChanges(List<ChangedItem> adds)
        {
            Logger.Write("Processing repository changes...");
            Logger.Write("");
            double current = 0;
            ReportProgress(0);

            List<string> changedOrNewItems = _repository.RetrieveStatus().Where(p => p.State == FileStatus.ModifiedInWorkdir || p.State == FileStatus.NewInWorkdir).Select(o => o.FilePath).ToList();
            List<string> deletedFiles = _repository.RetrieveStatus().Where(p => p.State == FileStatus.DeletedFromWorkdir || p.State == FileStatus.DeletedFromIndex).Select(o => o.FilePath).ToList();
            double total = 0;
            if (config.CommitByOprid)
            {
                List<ChangedItem> newOrModifiedFiles = new List<ChangedItem>();
                total = changedOrNewItems.Count;

                foreach (var f in changedOrNewItems)
                {
                    newOrModifiedFiles.Add(adds.Where(p => p.RepoPath == f).First());
                    current++;
                    ReportProgress(((int)(((current / total) * 10000)) / (double)100));
                }

                Logger.Write("Processing OPRID groups...");
                var opridGroups = newOrModifiedFiles.GroupBy(p => p.OperatorId);

                Logger.Write("Processing staged changes...");
                Logger.Write("");
                foreach (var opr in opridGroups)
                {
                    var staged = opr.Select(o => o.RepoPath).ToList();
                    Commands.Stage(_repository, staged);

                    if (newOrModifiedFiles.Count > 0)
                    {
                        var oprid = opr.Key;
                        Signature author = new Signature(oprid, oprid, DateTime.Now);
                        Signature committer = author;
                        Commit commit = _repository.Commit("Changes made by " + oprid, author, committer);
                    }
                }
            }
            else //not by oprid
            {
                if (changedOrNewItems.Count > 0)
                {
                    Commands.Stage(_repository, changedOrNewItems);
                    Signature author = new Signature("PIVET", "PIVET", DateTime.Now);
                    Signature committer = author;
                    Commit commit = _repository.Commit("Changes captured by Pivet", author, committer);
                }
            }

            if (deletedFiles.Count > 0)
            {
                Commands.Stage(_repository, deletedFiles);
                Signature author = new Signature("SYSTEM", "SYSTEM", DateTime.Now);
                Signature committer = author;
                Commit commit = _repository.Commit("Deleted Objects", author, committer);
            }
        }
    }
}
