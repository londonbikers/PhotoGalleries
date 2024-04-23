using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace LB.PhotoGalleries;

public static class SessionExtensions
{
    public static void Set<T>(this ISession session, string key, T value)
    {
        var serializedContent = JsonConvert.SerializeObject(value);
        session.SetString(key, serializedContent);
    }

    public static T Get<T>(this ISession session, string key)
    {
        var value = session.GetString(key);
        return value == null ? default : JsonConvert.DeserializeObject<T>(value);
    }
}