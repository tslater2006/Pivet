using Oracle.ManagedDataAccess.Client;
using Pivet.Data.Connection;
using System;
using System.Collections.Generic;

namespace Pivet
{
    /// <summary>
    /// Manages database connections during ConfigBuilder operations
    /// Provides connection caching, validation, and lifecycle management
    /// </summary>
    public class ConfigBuilderConnectionManager : IDisposable
    {
        private readonly Dictionary<string, OracleConnection> _activeConnections;
        private readonly Dictionary<string, EnvironmentConfig> _environments;
        private bool _disposed = false;

        public ConfigBuilderConnectionManager()
        {
            _activeConnections = new Dictionary<string, OracleConnection>();
            _environments = new Dictionary<string, EnvironmentConfig>();
        }

        /// <summary>
        /// Registers an environment for potential database connections
        /// </summary>
        public void RegisterEnvironment(EnvironmentConfig environment)
        {
            if (environment == null || string.IsNullOrWhiteSpace(environment.Name))
                return;

            _environments[environment.Name] = environment;
        }

        /// <summary>
        /// Gets an active database connection for the specified environment
        /// Creates and caches the connection if it doesn't exist
        /// </summary>
        public ConnectionResult GetConnection(string environmentName)
        {
            try
            {
                // Check if we already have an active connection
                if (_activeConnections.ContainsKey(environmentName))
                {
                    var existingConn = _activeConnections[environmentName];
                    if (existingConn.State == System.Data.ConnectionState.Open)
                    {
                        return new ConnectionResult(existingConn, true, "Using cached connection");
                    }
                    else
                    {
                        // Clean up dead connection
                        _activeConnections.Remove(environmentName);
                        existingConn?.Dispose();
                    }
                }

                // Get environment configuration
                if (!_environments.ContainsKey(environmentName))
                {
                    return new ConnectionResult(null, false, $"Environment '{environmentName}' not registered");
                }

                var environment = _environments[environmentName];
                
                // Create new connection using the same logic as JobRunner
                var connectionProvider = environment.Connection.Provider;
                var providerType = Type.GetType("Pivet.Data.Connection." + connectionProvider + "Connection");
                
                if (providerType == null)
                {
                    return new ConnectionResult(null, false, $"Unable to find database provider: {connectionProvider}");
                }

                var dbProvider = Activator.CreateInstance(providerType) as IConnectionProvider;
                dbProvider.SetParameters(environment.Connection);
                var connectionResult = dbProvider.GetConnection();

                if (connectionResult.Item2) // Success
                {
                    _activeConnections[environmentName] = connectionResult.Item1;
                    return new ConnectionResult(connectionResult.Item1, true, "Connection established");
                }
                else
                {
                    return new ConnectionResult(null, false, connectionResult.Item3);
                }
            }
            catch (Exception ex)
            {
                return new ConnectionResult(null, false, $"Error creating connection: {ex.Message}");
            }
        }

        /// <summary>
        /// Tests if a connection can be established to the specified environment
        /// Does not cache the connection
        /// </summary>
        public ConnectionResult TestConnection(string environmentName)
        {
            if (!_environments.ContainsKey(environmentName))
            {
                return new ConnectionResult(null, false, $"Environment '{environmentName}' not registered");
            }

            var environment = _environments[environmentName];
            
            try
            {
                var connectionProvider = environment.Connection.Provider;
                var providerType = Type.GetType("Pivet.Data.Connection." + connectionProvider + "Connection");
                
                if (providerType == null)
                {
                    return new ConnectionResult(null, false, $"Unable to find database provider: {connectionProvider}");
                }

                var dbProvider = Activator.CreateInstance(providerType) as IConnectionProvider;
                dbProvider.SetParameters(environment.Connection);
                var connectionResult = dbProvider.GetConnection();

                if (connectionResult.Item2) // Success
                {
                    // Test with a simple query
                    using (var testCmd = new OracleCommand("SELECT 1 FROM DUAL", connectionResult.Item1))
                    {
                        testCmd.ExecuteScalar();
                    }
                    
                    connectionResult.Item1.Close();
                    connectionResult.Item1.Dispose();
                    
                    return new ConnectionResult(null, true, "Connection test successful");
                }
                else
                {
                    return new ConnectionResult(null, false, connectionResult.Item3);
                }
            }
            catch (Exception ex)
            {
                return new ConnectionResult(null, false, $"Connection test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a list of registered environment names
        /// </summary>
        public List<string> GetRegisteredEnvironments()
        {
            return new List<string>(_environments.Keys);
        }

        /// <summary>
        /// Checks if the specified environment has an active connection
        /// </summary>
        public bool HasActiveConnection(string environmentName)
        {
            if (!_activeConnections.ContainsKey(environmentName))
                return false;

            var connection = _activeConnections[environmentName];
            return connection?.State == System.Data.ConnectionState.Open;
        }

        /// <summary>
        /// Closes and removes the connection for the specified environment
        /// </summary>
        public void CloseConnection(string environmentName)
        {
            if (_activeConnections.ContainsKey(environmentName))
            {
                var connection = _activeConnections[environmentName];
                connection?.Close();
                connection?.Dispose();
                _activeConnections.Remove(environmentName);
            }
        }

        /// <summary>
        /// Gets connection information for display purposes
        /// </summary>
        public ConnectionInfo GetConnectionInfo(string environmentName)
        {
            if (!_environments.ContainsKey(environmentName))
                return new ConnectionInfo { EnvironmentName = environmentName, IsRegistered = false };

            var environment = _environments[environmentName];
            var hasActiveConnection = HasActiveConnection(environmentName);

            return new ConnectionInfo
            {
                EnvironmentName = environmentName,
                IsRegistered = true,
                HasActiveConnection = hasActiveConnection,
                TNSName = environment.Connection.TNS,
                Username = environment.Connection.BootstrapParameters?.User ?? "Unknown",
                Schema = environment.Connection.Schema
            };
        }

        /// <summary>
        /// Disposes all active connections
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var connection in _activeConnections.Values)
                {
                    try
                    {
                        connection?.Close();
                        connection?.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }
                _activeConnections.Clear();
                _environments.Clear();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Result of a database connection operation
    /// </summary>
    public class ConnectionResult
    {
        public OracleConnection Connection { get; }
        public bool IsSuccess { get; }
        public string Message { get; }

        public ConnectionResult(OracleConnection connection, bool isSuccess, string message)
        {
            Connection = connection;
            IsSuccess = isSuccess;
            Message = message;
        }
    }

    /// <summary>
    /// Information about a database connection
    /// </summary>
    public class ConnectionInfo
    {
        public string EnvironmentName { get; set; }
        public bool IsRegistered { get; set; }
        public bool HasActiveConnection { get; set; }
        public string TNSName { get; set; }
        public string Username { get; set; }
        public string Schema { get; set; }

        public override string ToString()
        {
            if (!IsRegistered)
                return $"{EnvironmentName} (Not Registered)";

            var status = HasActiveConnection ? "Connected" : "Not Connected";
            var schemaInfo = !string.IsNullOrWhiteSpace(Schema) ? $" Schema: {Schema}" : "";
            return $"{EnvironmentName} ({TNSName} - {Username}{schemaInfo}) [{status}]";
        }
    }
}