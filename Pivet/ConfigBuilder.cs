using Newtonsoft.Json;
using Pivet;
using Pivet.Data;
using Pivet.Data.Processors;
using Pivet.Data.Connection;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pivet
{
    class ConfigBuilder
    {
        static Config configFile;
        static string configPath;

        public static string RunBuilder()
        {
            return RunBuilder("config.json");
        }

        public static string RunBuilder(string path)
        {
            configPath = path;

            if (configPath == null)
            {
                PromptWithDefault("Enter the path to the configuration file", ref configPath);
            }
            FileInfo f = new FileInfo(configPath);
            if (File.Exists(configPath))
            {
                /* Load the configuration file */
                Console.WriteLine($"Loading file: {configPath}");
                string configText = File.ReadAllText(configPath);
                try
                {
                    configFile = JsonConvert.DeserializeObject<Config>(configText);
                    ProcessConfigQuestions();
                }
                catch (Exception ex)
                {
                    Console.Write("Failed to parse configuation file, please validate all required fields are present.");
                    Console.WriteLine(ex.ToString());
                    Console.ReadKey();
                    return "";
                }
            }
            else
            {
                var createNew = "y";
                PromptWithDefault("No configuration file found, would you like to create a new one? (y/n)", ref createNew);

                if (createNew == "y")
                {
                    configFile = new Config();
                    ProcessConfigQuestions();
                }

            }

            if (File.Exists(configPath))
            {
                return configPath;
            } else
            {
                return "";
            }
        }

        static void ProcessConfigQuestions()
        {
            /* Check for environments, if none we need to add one before moving on to profiles */
            if (configFile.Environments.Count == 0)
            {
                Console.WriteLine("There are currently no environments defined. Making a new one.");
                AddEnvironment();
            }

            var modifyEnv = "n";
            do
            {
                PromptWithDefault("Would you like to modify an existing environment? (y/n)", ref modifyEnv);

                if (modifyEnv == "y")
                {
                    EnvironmentConfig toModify = PromptWithList("Select the environment you wish to modify", configFile.Environments);
                    ModifyEnvironment(toModify);
                }
            } while (modifyEnv == "y");

            var testEnv = "n";
            do
            {
                PromptWithDefault("Would you like to test an environment connection? (y/n)", ref testEnv);

                if (testEnv == "y")
                {
                    EnvironmentConfig toTest = PromptWithList("Select the environment connection to test", configFile.Environments);
                    TestConnection(toTest.Connection);
                }
            } while (testEnv == "y");

            var addEnv = "n";
            do
            {
                PromptWithDefault("Would you like to create a new environment? (y/n)", ref addEnv);

                if (addEnv == "y")
                {
                    AddEnvironment();
                }
            } while (addEnv == "y");


            /* Check for profiles, if none we need to add one */
            if (configFile.Profiles.Count == 0)
            {
                Console.WriteLine("There are currently no profiles defined. Making a new one.");
                AddProfile();
            }

            var modifyProfile = "n";
            do
            {
                PromptWithDefault("Would you like to modify an existing profile? (y/n)", ref modifyProfile);

                if (modifyProfile == "y")
                {
                    ProfileConfig toModify = PromptWithList("Select the profile you wish to modify", configFile.Profiles);
                    ModifyProfile(toModify);
                }
            } while (modifyProfile == "y");


            var addProfile = "n";
            do
            {
                PromptWithDefault("Would you like to create a new profile? (y/n)", ref addProfile);

                if (addProfile == "y")
                {
                    AddProfile();
                }
            } while (addProfile == "y");


            /* Check for jobs, if none we need to add one */
            if (configFile.Jobs.Count == 0)
            {
                Console.WriteLine("There are currently no jobs defined. Making a new one.");
                AddJob();
            }

            var modifyJob = "n";
            do
            {
                PromptWithDefault("Would you like to modify an existing job? (y/n)", ref modifyJob);

                if (modifyJob == "y")
                {
                    JobConfig toModify = PromptWithList("Select the job you wish to modify", configFile.Jobs);
                    ModifyJob(toModify);
                }
            } while (modifyJob == "y");

            var addJob = "n";
            do
            {
                PromptWithDefault("Would you like to create a new job? (y/n)", ref addJob);

                if (addJob == "y")
                {
                    AddJob();
                }
            } while (addJob == "y");

            // Validate configuration before saving
            ValidateConfiguration();

            SaveConfig();
        }

        private static void ModifyEnvironment(EnvironmentConfig toModify)
        {
            var name = toModify.Name;

            PromptWithDefault("Please enter a name for the environment", ref name);

            toModify.Name = name;

            ConnectionConfig conn = toModify.Connection;

            var newProvider = PromptWithEnum<ConnectionProvider>("Please select the connection type");

            conn.Provider = newProvider;

            switch (conn.Provider)
            {
                case ConnectionProvider.Bootstrap:
                    if (conn.BootstrapParameters == null)
                    {
                        conn.BootstrapParameters = new BootstrapParams();
                    }

                    PromptWithDefault("Enter the path to TNS_ADMIN", ref conn.TNS_ADMIN);
                    PromptWithDefault("Enter the TNS connection name", ref conn.TNS);
                    PromptWithDefault("Enter the schema name", ref conn.Schema);
                    PromptWithDefault("Enter the username", ref conn.BootstrapParameters.User);
                    string tempPassword = "";
                    PromptWithDefault("Enter the password", ref tempPassword);
                    conn.BootstrapParameters.Password = tempPassword;
                    break;
            }

            // Offer to test the connection
            var testConnection = "y";
            PromptWithDefault("Would you like to test this database connection? (y/n)", ref testConnection);
            if (testConnection == "y")
            {
                TestConnection(toModify.Connection);
            }

            SaveConfig();
        }

        static void AddEnvironment()
        {
            EnvironmentConfig env = new EnvironmentConfig();

            ModifyEnvironment(env);

            configFile.Environments.Add(env);
            SaveConfig();
        }

        static void AddProfile()
        {
            ProfileConfig profile = new ProfileConfig();

            ModifyProfile(profile);

            configFile.Profiles.Add(profile);
            SaveConfig();
        }

        static void AddJob()
        {
            JobConfig job = new JobConfig();

            ModifyJob(job);

            configFile.Jobs.Add(job);
            SaveConfig();
        }
        static List<string> FindProviders() {
            var type = typeof(IDataProcessor);
            return AppDomain.CurrentDomain.GetAssemblies().Where(a => Program.LoadedAssemblies.Contains(a)).SelectMany(s => s.GetTypes()).Where(p => type.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract).Select(t => (Activator.CreateInstance(t) as IDataProcessor).ProcessorID).ToList();
        }
        static void ModifyProfile(ProfileConfig profile)
        {
            PromptWithDefault("Please give this profile a name", ref profile.Name);
            // PromptWithDefault("Enter the output path", ref profile.OutputFolder);
            //profile.EnvironmentName = PromptWithList("Select the environment this profile should use", configFile.Environments).Name;

            var availableProviders = FindProviders();
            profile.DataProviders = SelectMultipleFromList<string>("Select which data providers you would like", availableProviders);

            /* TODO: Builder support for RawData entries */

            ModifyFilters(profile.Filters, profile.DataProviders.Contains(new MessageCatalogProcessor().ProcessorID));

            if (profile.DataProviders.Contains(new RawDataProcessor().ProcessorID))
            {
                ConfigureRawData(profile.Filters.RawData);
            }

            //var commitByOprid = "y";
            //PromptWithDefault("Would you like commits done by OPRID where possible? (y/n)", ref commitByOprid);
            //profile.Repository.CommitByOprid = (commitByOprid == "y");

            /*var configRemoteRepo = "n";
            PromptWithDefault("Would you like to configure a remote repository? (y/n)", ref configRemoteRepo);
            if (configRemoteRepo == "y")
            {
                var repoConfig = profile.Repository;

                var repoUrl = repoConfig.Url;
                PromptWithDefault("Please enter the URL for the remote repository", ref repoUrl);
                repoConfig.Url = repoUrl;

                var username = repoConfig.User;
                PromptWithDefault("Please enter the username for pushing changes", ref username);
                repoConfig.User = username;

                var password = "***";
                PromptWithDefault("Please enter the password for pushing changes", ref password);
                if (password != "***")
                {
                    repoConfig.Password = password;
                }

            }*/

            SaveConfig();
        }

        static void ModifyJob(JobConfig job)
        {
            PromptWithDefault("Please give this job a name", ref job.Name);
            
            PromptWithDefault("Enter the output folder path", ref job.OutputFolder);

            if (configFile.Environments.Count == 0)
            {
                Console.WriteLine("Warning: No environments defined. You must create environments before configuring jobs.");
                return;
            }

            if (configFile.Profiles.Count == 0)
            {
                Console.WriteLine("Warning: No profiles defined. You must create profiles before configuring jobs.");
                return;
            }

            // Select environment
            EnvironmentConfig selectedEnv = PromptWithList("Select the environment this job should use", configFile.Environments);
            job.EnvironmentName = selectedEnv.Name;

            // Select profile
            ProfileConfig selectedProfile = PromptWithList("Select the profile this job should use", configFile.Profiles);
            job.ProfileName = selectedProfile.Name;

            // Configure repository
            ModifyRepository(job.Repository);

            SaveConfig();
        }

        static void ConfigureRawData(List<RawDataEntry> entries)
        {
            Logger.Write("Pivet supports the concept of Raw Data, which allows for arbitrary tools tables to be included in the version control where builtin support does not exist.");
            string configureRawData = "y";
            PromptWithDefault("Would you like to configure Raw Data entries? (y/n)", ref configureRawData);

            if (configureRawData != "y")
            {
                return;
            }

            if (entries.Count > 0)
            {
                string modifyExisting = "n";
                PromptWithDefault("You have existing Raw Data entries, would you like to modify one? (y/n)", ref modifyExisting);

                while (modifyExisting == "y")
                {
                    var selectedItem = PromptWithList("Please select which existing item you want to modify", entries);

                    ModifyRawData(selectedItem);

                    PromptWithDefault("Would you like to modify another existing item? (y/n)", ref modifyExisting);
                }
            }
            string addNewEntry = (entries.Count > 0 ? "n" : "y");

            PromptWithDefault("Would you like to add a new Raw Data entry? (y/n)", ref addNewEntry);

            while (addNewEntry == "y")
            {
                RawDataEntry entry = new RawDataEntry();
                entries.Add(entry);
                ModifyRawData(entry);

                PromptWithDefault("Would you like to add a new Raw Data entry? (y/n)", ref addNewEntry);
            }
        }

        static void ModifyRawData(RawDataEntry entry)
        {
            var recordName = entry.Record;
            PromptWithDefault("Please enter the record name (ex: PSPNLDEFN)", ref recordName);

            var filterField = entry.FilterField;
            PromptWithDefault("Please enter the field name for item prefix filtering (ex: PNLNAME)", ref filterField);

            var namePattern = entry.NamePattern;
            PromptWithDefault("Please enter the filename pattern for this entry (ex: {PNLNAME}.page)", ref namePattern);

            var folderName = entry.Folder;
            PromptWithDefault("Please enter the root folder name for this entry (ex: Pages)", ref folderName);

            var includedRelated = "n";
            PromptWithDefault("Would you like to include related tables? (y/n)", ref includedRelated);

            var extraCriteria = entry.ExtraCriteria ?? "";
            PromptWithDefault("Enter any extra SQL WHERE criteria (leave blank for none)", ref extraCriteria);

            // Initialize RelatedBlacklist if null
            if (entry.RelatedBlacklist == null)
            {
                entry.RelatedBlacklist = new List<string>();
            }

            // Configure related blacklist if they want to include related tables
            if (includedRelated == "y")
            {
                ManageRelatedBlacklist(entry.RelatedBlacklist);
            }
            
            entry.Record = recordName;
            entry.FilterField = filterField;
            entry.NamePattern = namePattern;
            entry.Folder = folderName;
            entry.IncludeRelated = (includedRelated == "y");
            entry.ExtraCriteria = string.IsNullOrWhiteSpace(extraCriteria) ? null : extraCriteria;
        }

        static void ManageRelatedBlacklist(List<string> blacklist)
        {
            string configureBlacklist = "n";
            PromptWithDefault("Would you like to configure related table blacklist (tables to exclude)? (y/n)", ref configureBlacklist);
            
            if (configureBlacklist != "y")
            {
                return;
            }

            if (blacklist.Count > 0)
            {
                Console.WriteLine("Current blacklisted tables:");
                for (int i = 0; i < blacklist.Count; i++)
                {
                    Console.WriteLine($"   {i + 1}.) {blacklist[i]}");
                }

                string removeItems = "n";
                PromptWithDefault("Would you like to remove any blacklisted tables? (y/n)", ref removeItems);
                
                while (removeItems == "y")
                {
                    var indexToRemove = "1";
                    PromptWithDefault("Which table would you like to remove? (enter number)", ref indexToRemove);
                    
                    if (int.TryParse(indexToRemove, out int index) && index > 0 && index <= blacklist.Count)
                    {
                        string removedTable = blacklist[index - 1];
                        blacklist.RemoveAt(index - 1);
                        Console.WriteLine($"Removed '{removedTable}' from blacklist.");
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection.");
                    }

                    if (blacklist.Count > 0)
                    {
                        PromptWithDefault("Would you like to remove another table? (y/n)", ref removeItems);
                    }
                    else
                    {
                        removeItems = "n";
                    }
                }
            }

            string addItems = "y";
            PromptWithDefault("Would you like to add tables to the blacklist? (y/n)", ref addItems);
            
            while (addItems == "y")
            {
                string tableName = "";
                PromptWithDefault("Enter the table name to blacklist", ref tableName);
                
                if (!string.IsNullOrWhiteSpace(tableName) && !blacklist.Contains(tableName))
                {
                    blacklist.Add(tableName);
                    Console.WriteLine($"Added '{tableName}' to blacklist.");
                }
                else if (blacklist.Contains(tableName))
                {
                    Console.WriteLine($"Table '{tableName}' is already in the blacklist.");
                }

                PromptWithDefault("Would you like to add another table to blacklist? (y/n)", ref addItems);
            }
        }

        static void ModifyRepository(RepositoryConfig repo)
        {
            var configureRepo = "n";
            PromptWithDefault("Would you like to configure remote repository settings? (y/n)", ref configureRepo);

            if (configureRepo != "y")
            {
                return;
            }

            PromptWithDefault("Enter the remote repository URL", ref repo.Url);
            PromptWithDefault("Enter the git username", ref repo.User);
            
            string tempPassword = "";
            PromptWithDefault("Enter the git password/token", ref tempPassword);
            if (!string.IsNullOrEmpty(tempPassword))
            {
                repo.Password = tempPassword;
            }

            var commitByOprid = (repo.CommitByOprid ? "y" : "n");
            PromptWithDefault("Would you like commits done by OPRID where possible? (y/n)", ref commitByOprid);
            repo.CommitByOprid = (commitByOprid == "y");

            var selectedCommitStyle = PromptWithEnum<CommitStyleOptions>("Select the commit style");
            repo.CommitStyle = selectedCommitStyle;
        }

        static void ModifyFilters(FilterConfig filters, bool hasMsgCat)
        {
            filters.Projects = GetMultipleStrings("Please enter any project prefixes that should be used", String.Join(",", filters.Projects));
            filters.Prefixes = GetMultipleStrings("Please enter any item prefixes that should be used", String.Join(",", filters.Prefixes));
            filters.IncludeOprids = GetMultipleStrings("Please enter any Operator IDs that should be included", String.Join(",", filters.IncludeOprids));
            filters.ExcludeOprids = GetMultipleStrings("Please enter any Operator IDs that should be excluded", String.Join(",", filters.ExcludeOprids));

            if (hasMsgCat)
            {
                if (filters.MessageCatalogs == null)
                {
                    filters.MessageCatalogs = new List<MessageCatalogFilter>();
                }
                if (filters.MessageCatalogs.Count == 0)
                {
                    /* Would you like to add a message catalog range? */
                    var addMsgCat = "y";
                    PromptWithDefault("Would you like to add a message catalog range? (y/n)", ref addMsgCat);
                    if (addMsgCat == "y")
                    {
                        var addAnother = "y";
                        while (addAnother == "y")
                        {
                            AddMessageCatalog(filters.MessageCatalogs);
                            PromptWithDefault("Would you like to add another message catalog range? (y/n)", ref addAnother);
                        }
                    }

                } else
                {
                    var modifyMsgCat = "y";
                    PromptWithDefault("Would you like to modify an existing message catalog range? (y/n)", ref modifyMsgCat);
                    if (modifyMsgCat == "y")
                    {
                        var editAnother = "y";
                        while (editAnother == "y")
                        {
                            Console.WriteLine("Here are the existing message catalog ranges: ");
                            var x = 1;
                            foreach (var item in filters.MessageCatalogs)
                            {
                                Console.WriteLine($"   {x++}.) {item.Set} {item.Min}-{item.Max}");
                            }

                            var indexToEdit = "1";
                            PromptWithDefault("Which set would you like to modify?", ref indexToEdit);

                            ModifyMessageCatalog(filters.MessageCatalogs[int.Parse(indexToEdit) - 1]);

                            PromptWithDefault("Would you like to modify another message catalog range? (y/n)", ref editAnother);
                        }
                    }

                    var addMsgCat = "y";
                    PromptWithDefault("Would you like to add a message catalog range? (y/n)", ref addMsgCat);
                    if (addMsgCat == "y")
                    {
                        var addAnother = "y";
                        while (addAnother == "y")
                        {
                            AddMessageCatalog(filters.MessageCatalogs);
                            PromptWithDefault("Would you like to add another message catalog range? (y/n)", ref addAnother);
                        }
                    }
                }
            }
            else
            {
                filters.MessageCatalogs = null;
            }

        }
        static void AddMessageCatalog(List<MessageCatalogFilter> msgCats)
        {
            MessageCatalogFilter filter = new MessageCatalogFilter();
            ModifyMessageCatalog(filter);
            msgCats.Add(filter);
        }
        static void ModifyMessageCatalog(MessageCatalogFilter msgCat)
        {
            var newSet = msgCat.Set.ToString();
            var newMin = msgCat.Min.ToString();
            var newMax = msgCat.Max.ToString();

            PromptWithDefault("Please enter the message set:", ref newSet);
            PromptWithDefault("Please enter the minimum message number:", ref newMin);
            PromptWithDefault("Please enter the maximum message number:", ref newMax);

            msgCat.Set = int.Parse(newSet);
            msgCat.Min = int.Parse(newMin);
            msgCat.Max = int.Parse(newMax);
        }

        static List<string> GetMultipleStrings(string msg, string defValue)
        {
            var choice = defValue;
            PromptWithDefault(msg, ref choice);

            var choices = choice.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());

            return choices.ToList();
        }

        static List<T> SelectMultipleFromList<T>(string msg, List<T> items)
        {

            Console.WriteLine($"{msg}: ");
            var x = 1;
            foreach (T item in items)
            {
                Console.WriteLine($"   {x++}.) {item.ToString()}");
            }
            Console.WriteLine($"   {items.Count + 1}.) All");
            var choice = $"{items.Count + 1}";
            PromptWithDefault("Select one or more Data Providers (comma separated)", ref choice);

            var choiceIndexes = choice.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => int.Parse(s.Trim())).ToList();
            var valueList = new List<T>();
            if ($"{items.Count+1}" == choice)
            {
                return items;
            }

            foreach (var index in choiceIndexes)
            {
                valueList.Add((T)items[index - 1]);
            }

            return valueList;
        }

        static List<T> SelectMultipleWithEnum<T>(string msg)
        {
            var enumValues = Enum.GetValues(typeof(T));

            Console.WriteLine($"{msg}: ");
            var x = 1;
            foreach (T item in enumValues)
            {
                Console.WriteLine($"   {x++}.) {item.ToString()}");
            }

            var choice = "";
            PromptWithDefault("Select one or more Data Providers (comma separated)", ref choice);

            var choiceIndexes = choice.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => int.Parse(s.Trim()));
            var valueList = new List<T>();
            foreach (var index in choiceIndexes)
            {
                valueList.Add((T)enumValues.GetValue(index - 1));
            }

            return valueList;
        }

        static T PromptWithList<T>(string msg, List<T> items)
        {
            Console.WriteLine($"{msg}: ");
            var x = 1;
            foreach (T item in items)
            {
                Console.WriteLine($"   {x++}.) {item.ToString()}");
            }

            var choice = "1";
            PromptWithDefault("Select a choice", ref choice);

            var choiceInt = int.Parse(choice);
            return items[choiceInt - 1];
        }

        static T PromptWithEnum<T>(string msg) where T : struct
        {
            Console.WriteLine($"{msg}: ");
            var x = 1;
            foreach (T item in Enum.GetValues(typeof(T)))
            {
                Console.WriteLine($"   {x}.) {item.ToString()}");
            }

            var choice = "1";
            PromptWithDefault("Select a choice", ref choice);

            var choiceInt = int.Parse(choice);
            return (T)Enum.GetValues(typeof(T)).GetValue(choiceInt - 1);
        }

        static string Prompt(string promptMessage)
        {
            Console.Write($"{promptMessage}: ");
            return Console.ReadLine();
        }

        static void PromptWithDefault(string promptMessage, ref string defaultValue)
        {
            if (defaultValue.Length > 0)
            {
                Console.Write($"{promptMessage} [{defaultValue}]: ");
            }
            else
            {
                Console.Write($"{promptMessage}: ");
            }
            var line = Console.ReadLine();

            if (line != "")
            {
                defaultValue = line;
            }
        }

        static void SaveConfig()
        {
            var configText = JsonConvert.SerializeObject(configFile, Formatting.Indented);
            File.WriteAllText(configPath, configText);
        }

        static void ValidateConfiguration()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();

            // Validate environments
            if (configFile.Environments.Count == 0)
            {
                errors.Add("No environments defined");
            }
            else
            {
                foreach (var env in configFile.Environments)
                {
                    if (string.IsNullOrWhiteSpace(env.Name))
                        errors.Add($"Environment has no name");
                    if (string.IsNullOrWhiteSpace(env.Connection.TNS))
                        errors.Add($"Environment '{env.Name}' has no TNS connection name");
                    if (env.Connection.BootstrapParameters == null || string.IsNullOrWhiteSpace(env.Connection.BootstrapParameters.User))
                        errors.Add($"Environment '{env.Name}' has no bootstrap user");
                }
            }

            // Validate profiles
            if (configFile.Profiles.Count == 0)
            {
                errors.Add("No profiles defined");
            }
            else
            {
                foreach (var profile in configFile.Profiles)
                {
                    if (string.IsNullOrWhiteSpace(profile.Name))
                        errors.Add($"Profile has no name");
                    if (profile.DataProviders.Count == 0)
                        warnings.Add($"Profile '{profile.Name}' has no data providers");
                }
            }

            // Validate jobs
            if (configFile.Jobs.Count == 0)
            {
                errors.Add("No jobs defined");
            }
            else
            {
                foreach (var job in configFile.Jobs)
                {
                    if (string.IsNullOrWhiteSpace(job.Name))
                        errors.Add($"Job has no name");
                    if (string.IsNullOrWhiteSpace(job.OutputFolder))
                        errors.Add($"Job '{job.Name}' has no output folder");
                    if (string.IsNullOrWhiteSpace(job.EnvironmentName))
                        errors.Add($"Job '{job.Name}' has no environment specified");
                    else if (!configFile.Environments.Any(e => e.Name == job.EnvironmentName))
                        errors.Add($"Job '{job.Name}' references non-existent environment '{job.EnvironmentName}'");
                    if (string.IsNullOrWhiteSpace(job.ProfileName))
                        errors.Add($"Job '{job.Name}' has no profile specified");
                    else if (!configFile.Profiles.Any(p => p.Name == job.ProfileName))
                        errors.Add($"Job '{job.Name}' references non-existent profile '{job.ProfileName}'");
                }
            }

            // Display validation results
            if (errors.Count > 0)
            {
                Console.WriteLine("\n*** CONFIGURATION ERRORS ***");
                foreach (var error in errors)
                {
                    Console.WriteLine($"ERROR: {error}");
                }
            }

            if (warnings.Count > 0)
            {
                Console.WriteLine("\n*** CONFIGURATION WARNINGS ***");
                foreach (var warning in warnings)
                {
                    Console.WriteLine($"WARNING: {warning}");
                }
            }

            if (errors.Count == 0 && warnings.Count == 0)
            {
                Console.WriteLine("\nConfiguration validation passed!");
            }
            else if (errors.Count == 0)
            {
                Console.WriteLine($"\nConfiguration is valid but has {warnings.Count} warning(s).");
            }
            else
            {
                Console.WriteLine($"\nConfiguration has {errors.Count} error(s) and {warnings.Count} warning(s).");
                Console.WriteLine("Please review and correct the configuration.");
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        static void TestConnection(ConnectionConfig connectionConfig)
        {
            Console.WriteLine("\n=== Testing Database Connection ===");
            
            try
            {
                // Create the appropriate connection provider based on the provider type
                var connectionProvider = connectionConfig.Provider;
                var providerType = Type.GetType("Pivet.Data.Connection." + connectionProvider + "Connection");
                
                if (providerType == null)
                {
                    Console.WriteLine("❌ ERROR: Unable to find the specified database provider: " + connectionProvider);
                    return;
                }

                var dbProvider = Activator.CreateInstance(providerType) as IConnectionProvider;
                dbProvider.SetParameters(connectionConfig);
                
                Console.WriteLine($"🔄 Testing connection to: {connectionConfig.TNS}");
                Console.WriteLine($"   User: {connectionConfig.BootstrapParameters.User}");
                Console.WriteLine($"   TNS Admin: {connectionConfig.TNS_ADMIN}");
                if (!string.IsNullOrWhiteSpace(connectionConfig.Schema))
                {
                    Console.WriteLine($"   Schema: {connectionConfig.Schema}");
                }
                
                Console.WriteLine("   Connecting...");
                
                var connectionResult = dbProvider.GetConnection();
                
                if (connectionResult.Item2) // Success
                {
                    // Test a simple query to make sure the connection really works
                    using (var conn = connectionResult.Item1)
                    {
                        try
                        {
                            using (var cmd = new OracleCommand("SELECT 1 FROM DUAL", conn))
                            {
                                var result = cmd.ExecuteScalar();
                                Console.WriteLine("✅ SUCCESS: Database connection test passed!");
                                Console.WriteLine("   Connected successfully and executed test query.");
                                
                                // Show some additional connection info
                                Console.WriteLine($"   Oracle Version: {conn.ServerVersion}");
                                Console.WriteLine($"   Database Name: {conn.DatabaseName}");
                            }
                        }
                        catch (Exception queryEx)
                        {
                            Console.WriteLine("⚠️  WARNING: Connected to database but test query failed:");
                            Console.WriteLine($"   {queryEx.Message}");
                        }
                        finally
                        {
                            conn.Close();
                        }
                    }
                }
                else
                {
                    Console.WriteLine("❌ FAILED: Unable to connect to database");
                    Console.WriteLine($"   Error: {connectionResult.Item3}");
                    
                    // Provide some troubleshooting hints
                    Console.WriteLine("\n💡 Troubleshooting tips:");
                    Console.WriteLine("   • Check that TNS_ADMIN path exists and contains tnsnames.ora");
                    Console.WriteLine("   • Verify TNS connection name exists in tnsnames.ora");
                    Console.WriteLine("   • Confirm username and password are correct");
                    Console.WriteLine("   • Ensure database is running and accessible");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ EXCEPTION: An unexpected error occurred during connection test:");
                Console.WriteLine($"   {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner Exception: {ex.InnerException.Message}");
                }
            }
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }
    }
}