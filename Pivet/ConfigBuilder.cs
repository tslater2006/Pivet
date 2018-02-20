using Newtonsoft.Json;
using Pivet;
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
            configPath = "config.json";
            PromptWithDefault("Enter the path to the configuration file", ref configPath);
            FileInfo f = new FileInfo(configPath);
            if (File.Exists(configPath))
            {
                /* Load the configuration file */
                Console.WriteLine($"Loading file: {configPath}");
                string configText = File.ReadAllText(configPath);
                try
                {
                    configFile = JsonConvert.DeserializeObject<Config>(configText);
                }
                catch (Exception ex)
                {
                    Console.Write("Failed to parse configuation file, please validate all required fields are present.");
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
            } while (modifyEnv == "y");


            var addProfile = "n";
            do
            {
                PromptWithDefault("Would you like to create a new profile? (y/n)", ref addProfile);

                if (addProfile == "y")
                {
                    AddProfile();
                }
            } while (addProfile == "y");
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
                    PromptWithDefault("Enter the password", ref conn.BootstrapParameters.Password);

                    break;
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

        static void ModifyProfile(ProfileConfig profile)
        {
            PromptWithDefault("Please give this profile a name", ref profile.Name);
            PromptWithDefault("Enter the output path", ref profile.OutputFolder);
            profile.EnvironmentName = PromptWithList("Select the environment this profile should use", configFile.Environments).Name;

            profile.DataProviders = SelectMultipleWithEnum<DataProvider>("Select which data providers you would like");

            /* TODO: Builder support for RawData entries */

            ModifyFilters(profile.Filters, profile.DataProviders.Contains(DataProvider.MessageCatalog));

            SaveConfig();
        }

        static void ModifyFilters(FilterConfig filters, bool hasMsgCat)
        {
            filters.Projects = GetMultipleStrings("Please enter any project prefixes that should be used", String.Join(",", filters.Projects));
            filters.Prefixes = GetMultipleStrings("Please enter any item prefixes that should be used", String.Join(",", filters.Prefixes));
            filters.IncludeOprids = GetMultipleStrings("Please enter any Operator IDs that should be included", String.Join(",", filters.IncludeOprids));
            filters.ExcludeOprids = GetMultipleStrings("Please enter any Operator IDs that should be excluded", String.Join(",", filters.ExcludeOprids));

            if (hasMsgCat)
            {
                /* Todo: prompt for msgcat info */
            }
            else
            {
                filters.MessageCatalogs = null;
            }

        }

        static List<string> GetMultipleStrings(string msg, string defValue)
        {
            var choice = defValue;
            PromptWithDefault(msg, ref choice);

            var choices = choice.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());

            return choices.ToList();
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
    }
}