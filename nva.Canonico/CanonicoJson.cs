using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace nva.Canonico
{
    public static class CanonicoJson
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            NullValueHandling = NullValueHandling.Include,
            Converters = { new StringEnumConverter() }
        };

        public static string Serialize<T>(T obj)
            => JsonConvert.SerializeObject(obj, Settings);

        public static T Deserialize<T>(string json)
            => JsonConvert.DeserializeObject<T>(json, Settings);
    }
}