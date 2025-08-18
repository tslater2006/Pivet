using Pivet.Data.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Pivet.GUI
{
    /// <summary>
    /// Service for discovering and managing available IRawDataFormatter implementations
    /// </summary>
    public static class RawDataFormatterService
    {
        private static List<string> _cachedFormatterNames = null;
        private static List<IRawDataFormatter> _cachedFormatters = null;
        
        /// <summary>
        /// Discovers all available IRawDataFormatter implementations using reflection
        /// </summary>
        /// <returns>List of formatter IDs</returns>
        public static List<string> GetAvailableFormatters()
        {
            if (_cachedFormatterNames != null)
                return _cachedFormatterNames;

            _cachedFormatterNames = new List<string>();
            
            // Get built-in formatters from current assembly
            var currentAssembly = Assembly.GetExecutingAssembly();
            AddFormattersFromAssembly(currentAssembly, _cachedFormatterNames);
            
            // Get formatters from loaded plugin assemblies
            if (Program.LoadedAssemblies != null)
            {
                foreach (var assembly in Program.LoadedAssemblies)
                {
                    AddFormattersFromAssembly(assembly, _cachedFormatterNames);
                }
            }
            
            // Sort for better user experience
            _cachedFormatterNames.Sort();
            
            return _cachedFormatterNames;
        }
        
        /// <summary>
        /// Gets a specific formatter by ID
        /// </summary>
        /// <param name="formatterID">The formatter ID to resolve</param>
        /// <returns>The formatter instance, or null if not found</returns>
        public static IRawDataFormatter GetFormatter(string formatterID)
        {
            if (string.IsNullOrWhiteSpace(formatterID))
                return GetDefaultFormatter();
                
            var formatters = GetAvailableFormatterInstances();
            var formatter = formatters.FirstOrDefault(f => f.FormatterID == formatterID);
            
            // If not found, return default formatter
            return formatter ?? GetDefaultFormatter();
        }
        
        /// <summary>
        /// Gets the default JSON formatter
        /// </summary>
        /// <returns>The default JSON formatter instance</returns>
        public static IRawDataFormatter GetDefaultFormatter()
        {
            return new JsonRawDataFormatter();
        }
        
        /// <summary>
        /// Forces refresh of the formatter cache (useful after loading new plugins)
        /// </summary>
        public static void RefreshFormatterCache()
        {
            _cachedFormatterNames = null;
            _cachedFormatters = null;
        }
        
        /// <summary>
        /// Checks if a formatter ID is available
        /// </summary>
        public static bool IsFormatterAvailable(string formatterID)
        {
            if (string.IsNullOrWhiteSpace(formatterID))
                return true; // Default formatter is always available
                
            var availableFormatters = GetAvailableFormatters();
            return availableFormatters.Contains(formatterID);
        }
        
        /// <summary>
        /// Gets formatter information for display purposes
        /// </summary>
        public static FormatterInfo GetFormatterInfo(string formatterID)
        {
            var formatter = GetFormatter(formatterID);
            
            if (formatter == null)
                return new FormatterInfo { ID = formatterID, Name = formatterID, IsAvailable = false };
                
            return new FormatterInfo 
            { 
                ID = formatter.FormatterID, 
                Name = formatter.FormatName,
                Extension = formatter.FileExtension,
                IsAvailable = true 
            };
        }
        
        private static void AddFormattersFromAssembly(Assembly assembly, List<string> formatterNames)
        {
            try
            {
                var formatterTypes = assembly.GetTypes()
                    .Where(t => typeof(IRawDataFormatter).IsAssignableFrom(t) 
                               && !t.IsInterface 
                               && !t.IsAbstract
                               && t.GetConstructor(Type.EmptyTypes) != null);

                foreach (var type in formatterTypes)
                {
                    try
                    {
                        // Create instance to get FormatterID
                        var instance = Activator.CreateInstance(type) as IRawDataFormatter;
                        if (instance != null && !string.IsNullOrWhiteSpace(instance.FormatterID))
                        {
                            if (!formatterNames.Contains(instance.FormatterID))
                            {
                                formatterNames.Add(instance.FormatterID);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Skip formatters that can't be instantiated
                        Logger.Write($"Warning: Could not instantiate formatter {type.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Warning: Error scanning assembly {assembly.FullName} for formatters: {ex.Message}");
            }
        }
        
        private static List<IRawDataFormatter> GetAvailableFormatterInstances()
        {
            if (_cachedFormatters != null)
                return _cachedFormatters;
                
            _cachedFormatters = new List<IRawDataFormatter>();
            
            // Get built-in formatters from current assembly
            var currentAssembly = Assembly.GetExecutingAssembly();
            AddFormatterInstancesFromAssembly(currentAssembly, _cachedFormatters);
            
            // Get formatters from loaded plugin assemblies
            if (Program.LoadedAssemblies != null)
            {
                foreach (var assembly in Program.LoadedAssemblies)
                {
                    AddFormatterInstancesFromAssembly(assembly, _cachedFormatters);
                }
            }
            
            return _cachedFormatters;
        }
        
        private static void AddFormatterInstancesFromAssembly(Assembly assembly, List<IRawDataFormatter> formatters)
        {
            try
            {
                var formatterTypes = assembly.GetTypes()
                    .Where(t => typeof(IRawDataFormatter).IsAssignableFrom(t) 
                               && !t.IsInterface 
                               && !t.IsAbstract
                               && t.GetConstructor(Type.EmptyTypes) != null);

                foreach (var type in formatterTypes)
                {
                    try
                    {
                        var instance = Activator.CreateInstance(type) as IRawDataFormatter;
                        if (instance != null)
                        {
                            formatters.Add(instance);
                        }
                    }
                    catch (Exception)
                    {
                        // Skip formatters that can't be instantiated
                    }
                }
            }
            catch (Exception)
            {
                // Skip problematic assemblies
            }
        }
    }
    
    /// <summary>
    /// Information about a raw data formatter
    /// </summary>
    public class FormatterInfo
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
        public bool IsAvailable { get; set; }
        
        public override string ToString()
        {
            if (IsAvailable && !string.IsNullOrWhiteSpace(Name))
                return $"{Name} (.{Extension})";
            return ID;
        }
    }
}