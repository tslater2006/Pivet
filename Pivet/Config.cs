using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
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

        [JsonProperty(Required = Required.Always)]
        public List<string> DataProviders = new List<string>();

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
        public string EncryptedPassword = "";
        [JsonIgnore]
        public string Password
        {
            get => PasswordCrypto.DecryptPassword(EncryptedPassword);
            set => EncryptedPassword = PasswordCrypto.EncryptPassword(value);
        }
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
        public string EncryptedPassword = "";

        [JsonIgnore]
        public string Password {
            get => PasswordCrypto.DecryptPassword(EncryptedPassword);
            set => EncryptedPassword = PasswordCrypto.EncryptPassword(value);
        }
    }

    public enum ConnectionProvider
    {
        Bootstrap
    }

    public class PasswordCrypto
    {
        private static byte[] TripleDesKey = new byte[] { 0x98, 0x1b, 0x63, 0xa1, 0x4e, 0x83, 0xdd, 0xc4, 0x28, 0xeb, 0x7d, 0xc7, 0xef, 0x5a, 0x0b, 0x8d, 0x9a, 0x6b, 0xcf, 0x23, 0x39, 0x2e, 0xfe, 0xfd };
        private static byte[] TripleDesIV = new byte[] { 0xed, 0xf6, 0xd5, 0x0c, 0x1a, 0xe4, 0x0f, 0x99 };

        internal static string DecryptPassword(string pass)
        {

            byte[] cipherData = Convert.FromBase64String(pass);

            byte[] plainText = TripleDesDecrypt(cipherData, TripleDesKey, TripleDesIV);

            string result = System.Text.Encoding.UTF8.GetString(plainText);
            result = result.Split('\0')[0];
            return result;
        }

        internal static string EncryptPassword(string pass)
        {
            byte[] passBytes = Encoding.UTF8.GetBytes(pass);
            byte[] cipherText = TripleDesEncrypt(passBytes, TripleDesKey, TripleDesIV);
            return Convert.ToBase64String(cipherText);
        }

        internal static byte[] TripleDesDecrypt(byte[] data, byte[] key, byte[] iv)
        {
            DesEdeEngine desEngine = new DesEdeEngine();
            CfbBlockCipher cfb = new CfbBlockCipher(desEngine, 64);
            BufferedBlockCipher cipher = new BufferedBlockCipher(cfb);

            KeyParameter keyParam = new KeyParameter(key);
            ParametersWithIV keyIVParam = new ParametersWithIV(keyParam, iv);

            desEngine.Init(false, keyParam);
            //desEngine.
            byte[] output = new byte[8];

            cipher.Init(false, keyIVParam);

            return cipher.DoFinal(data);
        }

        internal static byte[] TripleDesEncrypt(byte[] data, byte[] key, byte[] iv)
        {
            DesEdeEngine desEngine = new DesEdeEngine();
            CfbBlockCipher cfb = new CfbBlockCipher(desEngine, 64);
            BufferedBlockCipher cipher = new BufferedBlockCipher(cfb);

            KeyParameter keyParam = new KeyParameter(key);
            ParametersWithIV keyIVParam = new ParametersWithIV(keyParam, iv);
            
            desEngine.Init(true, keyParam);
            //desEngine.
            byte[] output = new byte[8];

            cipher.Init(true, keyIVParam);

            return cipher.DoFinal(data);
        }
    }
}
