using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pivet
{
    public class Config
    {
        [JsonProperty(Required = Required.Always)]
        public List<EnvironmentConfig> Environments = new List<EnvironmentConfig>();

        [JsonProperty(Required = Required.Always)]
        public List<ProfileConfig> Profiles = new List<ProfileConfig>();

    }

    public class EnvironmentConfig
    {
        [JsonProperty(Required = Required.Always)]
        public string Name = "";

        public ConnectionConfig Connection = new ConnectionConfig();

        public override string ToString()
        {
            return this.Name;
        }
    }

    public class ProfileConfig
    {
        [JsonProperty(Required = Required.Always)]
        public string Name = "";

        [JsonProperty(Required = Required.Always)]
        public string OutputFolder = "";

        [JsonProperty(Required = Required.Always)]
        public string EnvironmentName = "";

        [JsonProperty(Required = Required.Always, ItemConverterType = typeof(StringEnumConverter))]
        public List<DataProvider> DataProviders = new List<DataProvider>();

        public List<RawDataEntry> RawData = new List<RawDataEntry>();

        [JsonProperty(Required = Required.Always)]
        public FilterConfig Filters = new FilterConfig();

        public RepositoryConfig Repository = new RepositoryConfig();

        public override string ToString()
        {
            return this.Name;
        }
    }

    public class RawDataEntry
    {
        public string Record = "";
        public string FilterField = "";
        public string NamePattern = "";
        public string Folder = "";
        public bool IncludeRelated = false;
    }

    public class ConnectionConfig
    {
        [JsonProperty(Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ConnectionProvider Provider;

        [JsonProperty(Required = Required.Always)]
        public string TNS = "";
        [JsonProperty(Required = Required.Always)]
        public string TNS_ADMIN = "";

        public string Schema = "";

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public BootstrapParams BootstrapParameters;
    }

    public class FilterConfig
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Projects = new List<string>();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Prefixes = new List<string>();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> IncludeOprids = new List<string>();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> ExcludeOprids = new List<string>();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<MessageCatalogFilter> MessageCatalogs;
    }

    public class RepositoryConfig
    {
        [JsonProperty(Required = Required.Always)]
        public string Url = "";
        [JsonProperty(Required = Required.Always)]
        public string User = "";
        [JsonProperty(Required = Required.Always)]
        public string Password = "";
    }

    public class MessageCatalogFilter
    {
        public int Set;
        public int Min;
        public int Max;
    }

    public class BootstrapParams
    {
        [JsonProperty(Required = Required.Always)]
        public string User = "";

        [JsonProperty(Required = Required.Always)]
        public string Password = "";


    }

    public enum ConnectionProvider
    {
        Bootstrap
    }

    public enum DataProvider
    {
        HTML, MessageCatalog, PeopleCode, Registry, SQL, Stylesheet, TranslateValue
    }
}
