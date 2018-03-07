using Newtonsoft.Json;
using Pivet.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Pivet
{
    class Program
    {
        public static List<Assembly> LoadedAssemblies = new List<Assembly>();
        public static bool ShowProgress;
        public static Config GlobalConfig;
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

            var configFile = "config.json";
            var profileToRun = "";
            var wantsBuilder = false;
            ShowProgress = false;

            if (args.Length > 1)
            {
                for (var x = 0; x < args.Length - 1; x++)
                {
                    if (args[x].ToLower().Equals("-c"))
                    {
                        configFile = args[x + 1];
                    }
                    if (args[x].ToLower().Equals("-p"))
                    {
                        profileToRun = args[x + 1];
                    }
                    if (args[x].ToLower().Equals("-b"))
                    {
                        wantsBuilder = true;
                    }
                    if (args[x].ToLower().Equals("-p"))
                    {
                        ShowProgress = true;
                    }
                }
            } else if (args.Length == 1)
            {
                if (args[0].ToLower().Equals("-b"))
                {
                    wantsBuilder = true;
                }
                if (args[0].ToLower().Equals("-p"))
                {
                    ShowProgress = true;
                }
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
            
            foreach (var profile in GlobalConfig.Profiles)
            {
                if (profileToRun.Length > 0)
                {
                    if (profile.Name.Equals(profileToRun))
                    {
                        EnvironmentConfig environment = GlobalConfig.Environments.Where(e => e.Name.Equals(profile.EnvironmentName)).FirstOrDefault();
                        if (environment == null)
                        {
                            Logger.Error($"Could not run profile '{profileToRun}', unable to find environment named '{profile.EnvironmentName}'");
                            return;
                        }
                        else
                        {
                            ProfileRunner.Run(profile, environment);
                        }
                    }
                }
                else
                {
                    EnvironmentConfig environment = GlobalConfig.Environments.Where(e => e.Name.Equals(profile.EnvironmentName)).FirstOrDefault();
                    if (environment == null)
                    {
                        Logger.Error($"Could not run profile '{profileToRun}', unable to find environment named '{profile.EnvironmentName}'");
                    }
                    else
                    {
                        ProfileRunner.Run(profile, environment); 
                    }
                }

            }
            Logger.Write("All done!");

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
