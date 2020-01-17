using Newtonsoft.Json;
using Pivet.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Pivet
{    class Program
    {
        public static List<Assembly> LoadedAssemblies = new List<Assembly>();
        public static bool ShowProgress;
        public static Config GlobalConfig;
        public static string CustomCommitMessage;
        static void Main(string[] args)
        {
            /* Add main Pivet assembly */
            LoadedAssemblies.Add(Assembly.GetExecutingAssembly());
            /* Load any plugin DLLs */
            if (Directory.Exists("plugins"))
            {
                DirectoryInfo dir = new DirectoryInfo("plugins");

                foreach (FileInfo file in dir.GetFiles("*.dll"))
                {
                    Logger.Write("Loaded plugin: " + file.Name);
                    Assembly assembly = Assembly.LoadFrom(file.FullName);

                    if (assembly.GetTypes().Where(p => (typeof(IDataProcessor).IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract)).Count() > 0)
                    {
                        LoadedAssemblies.Add(assembly);
                    }
                }
            }
            /* by default no custom commit message */
            CustomCommitMessage = "";

            var configFile = "config.json";
            var jobToRun = "";
            var wantsBuilder = false;
            var passwordEncryptMode = false;
            var degreeOfParallel = 1;
            ShowProgress = false;

            if (args.Contains("-e"))
            {
                passwordEncryptMode = true;
            }

            if (args.Length > 1)
            {
                for (var x = 0; x < args.Length - 1; x++)
                {
                    if (args[x].ToLower().Equals("-c"))
                    {
                        configFile = args[x + 1];
                        x++;
                    }
                    if (args[x].ToLower().Equals("-j"))
                    {
                        jobToRun = args[x + 1];
                        x++;
                    }
                    if (args[x].ToLower().Equals("-b"))
                    {
                        wantsBuilder = true;
                    }
                    if (args[x].ToLower().Equals("-v"))
                    {
                        ShowProgress = true;
                    }
                    if (args[x].ToLower().Equals("-m"))
                    {
                        CustomCommitMessage = args[x + 1];
                        x++;
                    }
                    if (args[x].ToLower().Equals("-p"))
                    {
                        degreeOfParallel = int.Parse(args[x + 1]);
                        x++;
                    }
                }
            }
            else if (args.Length == 1)
            {
                if (args[0].ToLower().Equals("-b"))
                {
                    wantsBuilder = true;
                }
                if (args[0].ToLower().Equals("-v"))
                {
                    ShowProgress = true;
                }
            }

            if (passwordEncryptMode)
            {
                bool passwordMatch = false;
                string pass = "";
                while (passwordMatch == false)
                {
                    Console.Write("Enter the password you want to encrypt: ");
                    pass = ReadPassword('*');
                    Console.Write("Please confirm the password: ");
                    string passConfirm = ReadPassword('*');

                    if (pass.Equals(passConfirm))
                    {
                        passwordMatch = true;
                    } else
                    {
                        Console.WriteLine("Passwords did not match. Please try again.");
                    }
                }

                Console.WriteLine("Encrypted: " + PasswordCrypto.EncryptPassword(pass));
                return;
            }

            if (File.Exists(configFile) == false)
            {
                if (wantsBuilder)
                {
                    configFile = ConfigBuilder.RunBuilder();
                }

                if (configFile == "")
                {
                    Logger.Error("Pivet cannot run without a configuration file.");
                    return;
                }
            }
            else
            {
                if (wantsBuilder)
                {
                    Console.Write("Found an existing config file, would you like to modify it? (y/n)");
                    if (Console.ReadLine() == "y")
                    {
                        configFile = ConfigBuilder.RunBuilder(configFile);
                    }
                }
            }

            string j = File.ReadAllText(configFile);
            try
            {
                GlobalConfig = JsonConvert.DeserializeObject<Config>(j);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to parse config.json, please validate all required fields are present.");
                Logger.Error(ex.ToString());
                Console.ReadKey();
                return;
            }

            Logger.Write($"Config loaded. {GlobalConfig.Environments.Count} Environment(s) found, {GlobalConfig.Profiles.Count} Profile(s) found.");


            List<JobConfig> jobsToRun = new List<JobConfig>();
            foreach (var job in GlobalConfig.Jobs)
            {
                if (jobToRun.Length > 0)
                {
                    if (job.Name.Equals(jobToRun))
                    {
                        EnvironmentConfig environment = GlobalConfig.Environments.Where(e => e.Name.Equals(job.EnvironmentName)).FirstOrDefault();
                        if (environment == null)
                        {
                            Logger.Error($"Could not run profile '{jobToRun}', unable to find environment named '{job.EnvironmentName}'");
                            return;
                        }
                        else
                        {
                            jobsToRun.Add(job);
                        }
                    }
                }
                else
                {
                    EnvironmentConfig environment = GlobalConfig.Environments.Where(e => e.Name.Equals(job.EnvironmentName)).FirstOrDefault();
                    if (environment == null)
                    {
                        Logger.Error($"Could not run profile '{jobToRun}', unable to find environment named '{job.EnvironmentName}'");
                    }
                    else
                    {
                        jobsToRun.Add(job);
                    }
                }

            }

            Task<Tuple<bool, string>>[] taskList = new Task<Tuple<bool, string>>[jobsToRun.Count];
            if (jobsToRun.Count > 1 && degreeOfParallel > 1)
            {
                /* Suppress logger... */
                Logger.Quiet = true;
            }
            Stopwatch sw = new Stopwatch();
            sw.Start();

            /* Parallel, 4 at a time */
            Parallel.ForEach(jobsToRun, new ParallelOptions() { MaxDegreeOfParallelism = degreeOfParallel }, job =>
            {
                Console.WriteLine("Starting job: " + job.Name);
                JobRunner.Run(GlobalConfig, job);
                Console.WriteLine("Finished job: " + job.Name);
            });

            sw.Stop();
            Logger.Quiet = false;
            Logger.Write("All jobs finished in: " + sw.Elapsed.TotalSeconds + " seconds.");
            Logger.Write("All done!");

        }
        public static string ReadPassword(char mask)
        {
            const int ENTER = 13, BACKSP = 8, CTRLBACKSP = 127;
            int[] FILTERED = { 0, 27, 9, 10 /*, 32 space, if you care */ }; // const


            SecureString securePass = new SecureString();

            char chr = (char)0;

            while ((chr = System.Console.ReadKey(true).KeyChar) != ENTER)
            {
                if (((chr == BACKSP) || (chr == CTRLBACKSP))
                    && (securePass.Length > 0))
                {
                    System.Console.Write("\b \b");
                    securePass.RemoveAt(securePass.Length - 1);

                }
                // Don't append * when length is 0 and backspace is selected
                else if (((chr == BACKSP) || (chr == CTRLBACKSP)) && (securePass.Length == 0))
                {
                }

                // Don't append when a filtered char is detected
                else if (FILTERED.Count(x => chr == x) > 0)
                {
                }

                // Append and write * mask
                else
                {
                    securePass.AppendChar(chr);
                    System.Console.Write(mask);
                }
            }

            System.Console.WriteLine();
            IntPtr ptr = new IntPtr();
            ptr = Marshal.SecureStringToBSTR(securePass);
            string plainPass = Marshal.PtrToStringBSTR(ptr);
            Marshal.ZeroFreeBSTR(ptr);
            return plainPass;
        }
    }

    internal class Logger
    {
        internal static bool Quiet { get; set; }

        internal static void Write(string str)
        {
            if (!Quiet)
            {
                DateTime now = DateTime.Now;
                Console.WriteLine($"[MSG] [{now}] {str}");
            }
        }

        internal static void Error(string str)
        {
            Console.WriteLine($"[ERR] {str}");
        }
    }
}
