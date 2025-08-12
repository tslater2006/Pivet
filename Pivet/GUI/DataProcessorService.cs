using Pivet.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Pivet.GUI
{
    /// <summary>
    /// Service for discovering and managing available IDataProcessor implementations
    /// </summary>
    public static class DataProcessorService
    {
        private static List<string> _cachedProcessorNames = null;
        
        /// <summary>
        /// Discovers all available IDataProcessor implementations using reflection
        /// </summary>
        /// <returns>List of processor names (ProcessorID values)</returns>
        public static List<string> GetAvailableProcessors()
        {
            if (_cachedProcessorNames != null)
                return _cachedProcessorNames;

            _cachedProcessorNames = new List<string>();
            
            // Get built-in processors from current assembly
            var currentAssembly = Assembly.GetExecutingAssembly();
            AddProcessorsFromAssembly(currentAssembly, _cachedProcessorNames);
            
            // Get processors from loaded plugin assemblies
            if (Program.LoadedAssemblies != null)
            {
                foreach (var assembly in Program.LoadedAssemblies)
                {
                    AddProcessorsFromAssembly(assembly, _cachedProcessorNames);
                }
            }
            
            // Sort for better user experience
            _cachedProcessorNames.Sort();
            
            return _cachedProcessorNames;
        }
        
        /// <summary>
        /// Forces refresh of the processor cache (useful after loading new plugins)
        /// </summary>
        public static void RefreshProcessorCache()
        {
            _cachedProcessorNames = null;
        }
        
        /// <summary>
        /// Checks if a processor name is available
        /// </summary>
        public static bool IsProcessorAvailable(string processorName)
        {
            if (string.IsNullOrWhiteSpace(processorName))
                return false;
                
            var availableProcessors = GetAvailableProcessors();
            return availableProcessors.Contains(processorName);
        }
        
        /// <summary>
        /// Gets processor information for display purposes
        /// </summary>
        public static ProcessorInfo GetProcessorInfo(string processorName)
        {
            var processors = GetAvailableProcessorInstances();
            var processor = processors.FirstOrDefault(p => p.ProcessorID == processorName);
            
            if (processor == null)
                return new ProcessorInfo { Name = processorName, IsAvailable = false };
                
            return new ProcessorInfo 
            { 
                Name = processor.ProcessorID, 
                ItemName = processor.ItemName,
                IsAvailable = true 
            };
        }
        
        private static void AddProcessorsFromAssembly(Assembly assembly, List<string> processorNames)
        {
            try
            {
                var processorTypes = assembly.GetTypes()
                    .Where(t => typeof(IDataProcessor).IsAssignableFrom(t) 
                               && !t.IsInterface 
                               && !t.IsAbstract
                               && t.GetConstructor(Type.EmptyTypes) != null);

                foreach (var type in processorTypes)
                {
                    try
                    {
                        // Create instance to get ProcessorID
                        var instance = Activator.CreateInstance(type) as IDataProcessor;
                        if (instance != null && !string.IsNullOrWhiteSpace(instance.ProcessorID))
                        {
                            if (!processorNames.Contains(instance.ProcessorID))
                            {
                                processorNames.Add(instance.ProcessorID);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Skip processors that can't be instantiated
                        Logger.Write($"Warning: Could not instantiate processor {type.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Warning: Error scanning assembly {assembly.FullName}: {ex.Message}");
            }
        }
        
        private static List<IDataProcessor> GetAvailableProcessorInstances()
        {
            var processors = new List<IDataProcessor>();
            
            // Get built-in processors from current assembly
            var currentAssembly = Assembly.GetExecutingAssembly();
            AddProcessorInstancesFromAssembly(currentAssembly, processors);
            
            // Get processors from loaded plugin assemblies
            if (Program.LoadedAssemblies != null)
            {
                foreach (var assembly in Program.LoadedAssemblies)
                {
                    AddProcessorInstancesFromAssembly(assembly, processors);
                }
            }
            
            return processors;
        }
        
        private static void AddProcessorInstancesFromAssembly(Assembly assembly, List<IDataProcessor> processors)
        {
            try
            {
                var processorTypes = assembly.GetTypes()
                    .Where(t => typeof(IDataProcessor).IsAssignableFrom(t) 
                               && !t.IsInterface 
                               && !t.IsAbstract
                               && t.GetConstructor(Type.EmptyTypes) != null);

                foreach (var type in processorTypes)
                {
                    try
                    {
                        var instance = Activator.CreateInstance(type) as IDataProcessor;
                        if (instance != null)
                        {
                            processors.Add(instance);
                        }
                    }
                    catch (Exception)
                    {
                        // Skip processors that can't be instantiated
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
    /// Information about a data processor
    /// </summary>
    public class ProcessorInfo
    {
        public string Name { get; set; }
        public string ItemName { get; set; }
        public bool IsAvailable { get; set; }
        
        public override string ToString()
        {
            if (IsAvailable && !string.IsNullOrWhiteSpace(ItemName))
                return $"{Name} ({ItemName})";
            return Name;
        }
    }
}