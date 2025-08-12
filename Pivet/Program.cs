using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NStack;
using Pivet.Data;
using Pivet.GUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using Terminal.Gui;

namespace Pivet
{
   class Program
    {
        public static List<Assembly> LoadedAssemblies = new List<Assembly>();
        public static bool ShowProgress;
        public static Config GlobalConfig;
        public static string CurrentConfigPath;
        public static string CustomCommitMessage;

        /* GUI components */
        private static MenuBar menu;
        private static Window mainWindow;
        static void BuildMainWindow()
        {

            mainWindow = new Window("Pivet")
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1
            };


            Button environments = new Button("Environments")
            {
                X = Pos.Center(),
                Y = Pos.Center(),
                TextAlignment = TextAlignment.Centered
            };

            environments.Clicked += Environments_Clicked;

            Button profiles = new Button("Profiles")
            {
                X = Pos.Center(),
                Y = Pos.Bottom(environments) + 1,
                TextAlignment = TextAlignment.Centered
            };

            profiles.Clicked += Profiles_Clicked;

            Button jobs = new Button("Jobs")
            {
                X = Pos.Center(),
                Y = Pos.Bottom(profiles) + 1,
                TextAlignment = TextAlignment.Centered
            };

            jobs.Clicked += Jobs_Clicked;

            mainWindow.Add(environments);
            mainWindow.Add(profiles);
            mainWindow.Add(jobs);
        }

        static void RunGUI()
        {
            Application.Init();

            BuildMainWindow();

            menu = new MenuBar(new MenuBarItem[] {
            new MenuBarItem ("_File", new MenuItem [] {
                new MenuItem ("_Create Config...", "", () =>
                {
                    var sd = new SaveDialog("Create Config","Choose where to create a new Pivet configuration.",new List<string>(){".json" });
                    Application.Run(sd);

                    if (sd.FileName != null)
                    {
                        CurrentConfigPath = sd.FileName.ToString();

                        GlobalConfig = new Config();
                        var configText = JsonConvert.SerializeObject(GlobalConfig);
                        File.WriteAllText(CurrentConfigPath, configText);
                        
                    }
                }),
                new MenuItem ("_Load Config...", "", () =>
                {
                    var od = new OpenDialog("Load Config", "Select a configuration file to load.",new List<string>(){".json"});

                    Application.Run(od);

                    var filePath = od.FilePath.ToString();
                    GlobalConfig = JsonConvert.DeserializeObject<Config>(File.ReadAllText(filePath));
                    CurrentConfigPath = filePath;
                }),
                new MenuItem ("_Save Config...", "", () =>
                {
                    // Validate configuration before saving
                    var validationResult = GUI.ConfigValidator.ValidateConfig(GlobalConfig);
                    
                    if (!validationResult.IsValid)
                    {
                        var errorMessages = string.Join("\n", validationResult.Errors.Select(e => e.Message));
                        MessageBox.ErrorQuery("Configuration Validation Errors", 
                            $"Cannot save configuration due to errors:\n\n{errorMessages}", "OK");
                        return;
                    }
                    
                    if (validationResult.HasWarnings)
                    {
                        var warningMessages = string.Join("\n", validationResult.Warnings.Select(w => w.Message));
                        var result = MessageBox.Query("Configuration Validation Warnings", 
                            $"Configuration has warnings:\n\n{warningMessages}\n\nSave anyway?", "Yes", "No");
                        if (result != 0) return;
                    }
                    
                    try
                    {
                        var configText = JsonConvert.SerializeObject(GlobalConfig, Formatting.Indented);
                        File.WriteAllText(CurrentConfigPath, configText);
                        
                        var successMessage = validationResult.HasMessages 
                            ? $"Configuration saved successfully!\n\nValidation summary:\n- Errors: {validationResult.Errors.Count}\n- Warnings: {validationResult.Warnings.Count}\n- Info: {validationResult.InfoMessages.Count}"
                            : "Configuration saved successfully!";
                            
                        MessageBox.Query("Save Complete", successMessage, "OK");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery("Save Error", $"Failed to save configuration:\n{ex.Message}", "OK");
                    }
                }),
                new MenuItem ("_Quit", "", () => {
                    Application.RequestStop ();
                })
            }),
            new MenuBarItem ("_Run", new MenuItem [] {
                new MenuItem ("_Job", "", () =>
                {

                }),
                new MenuItem ("_All Jobs", "", () =>
                {

                })
            }),
        });

            GlobalConfig = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
            CurrentConfigPath = "config.json";
            // Add both menu and win in a single call
            Application.Top.Add(menu, mainWindow);

            Application.Run();
            Application.Shutdown();
        }

        private static void Environments_Clicked()
        {
            var environmentsWindow = new EnvironmentEditorWindow(GlobalConfig)
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1
            };

            //Application.Top.Remove(mainWindow);
            Application.Run(environmentsWindow);
            //Application.Top.Add(environmentsWindow);
        }

        private static void Profiles_Clicked()
        {
            var profilesWindow = new ProfileEditorWindow(GlobalConfig)
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1
            };

            Application.Run(profilesWindow);
        }

        private static void Jobs_Clicked()
        {
            var jobsWindow = new JobEditorWindow(GlobalConfig)
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1
            };

            //Application.Top.Remove(mainWindow);
            Application.Run(jobsWindow);
            //Application.Top.Add(environmentsWindow);
        }

        static void Main(string[] args)
        {
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

            LoadedAssemblies.Add(Assembly.GetExecutingAssembly());

            if (args.Contains("--gui") || args.Contains ("-g")) {
                RunGUI();
                return;
            }

            /* by default no custom commit message */
            CustomCommitMessage = "";

            var configFile = "config.json";
            var externalFile = "vars.json";
            var jobToRun = "";
            var wantsBuilder = false;
            var passwordEncryptMode = false;
            var rawDataTestMode = false;
            ShowProgress = false;

            if (args.Contains("-e"))
            {
                passwordEncryptMode = true;
            }

            if (args.Length > 1)
            {
                for (var x = 0; x < args.Length; x++)
                {
                    if (args[x].ToLower().Equals("-c"))
                    {
                        configFile = args[x + 1];
                        x++;
                    }
                    if (args[x].ToLower().Equals("-vars"))
                    {
                        externalFile = args[x + 1];
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
                    if (args[x].ToLower().Equals("--raw-data-test"))
                    {
                        Console.WriteLine("Running in raw data test mode, no jobs will be executed");
                        rawDataTestMode = true;
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
                if (args[0].ToLower().Equals("--raw-data-test"))
                {
                    Console.WriteLine("Running in raw data test mode, no jobs will be executed");
                    rawDataTestMode = true;
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
                var myObj = JObject.Load(new ConfigJsonTextReader(new StringReader(j), externalFile));
                GlobalConfig = myObj.ToObject<Config>();
                //GlobalConfig = JsonConvert.DeserializeObject<Config>(j);
                
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
                            if (rawDataTestMode)
                            {
                                JobRunner.RunRawDataTest(GlobalConfig, job);
                            }
                            else
                            {
                                //jobsToRun.Add(job);
                                JobRunner.Run(GlobalConfig, job);
                            }
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
                        if (rawDataTestMode)
                        {
                            JobRunner.RunRawDataTest(GlobalConfig, job);
                        }
                        else
                        {
                            //jobsToRun.Add(job);
                            JobRunner.Run(GlobalConfig, job); 
                        }
                    }
                }

            }

            /* Task<Tuple<bool, string>>[] taskList = new Task<Tuple<bool, string>>[jobsToRun.Count];

            for (var x = 0; x < jobsToRun.Count; x++)
            {
                taskList[x] = Task<Tuple<bool, string>>.Factory.StartNew(() =>
                {
                    Console.WriteLine("Starting job: " + jobsToRun[x].Name);
                    return JobRunner.Run(GlobalConfig, jobsToRun[x]);
                });
            }

            Task<Tuple<bool, string>>.WaitAll(taskList);*/

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

    internal class ConfigJsonTextReader : JsonTextReader
    {
        bool hasExternalVars = false;
        JObject externalVars;
        public ConfigJsonTextReader(TextReader reader, string externalVarPath) : base(reader)
        {
            if (File.Exists(externalVarPath))
            {
                hasExternalVars = true;
                externalVars = JObject.Load(new JsonTextReader(new StringReader(File.ReadAllText(externalVarPath))));
            }
        }

        public override bool Read()
        {
            bool success = base.Read();

            if (hasExternalVars == false)
            {
                return success;
            }

            var originalValue = this.Value;
            if (success && this.TokenType == JsonToken.String)
            {
                string curValue = (string)this.Value;
                if (curValue.StartsWith("%") && curValue.EndsWith("%"))
                {
                    /* external value */
                    /* variable name */
                    string varName = curValue.Substring(1, curValue.Length - 2);
                    if (externalVars.ContainsKey(varName))
                    {
                        this.SetToken(JsonToken.String, externalVars[varName].ToString());
                        return success;
                    } else
                    {
                        return success;
                    }
                }
            }
            return success;
        }
        
    }
}
