using System;
using System.Collections.Generic;

namespace AICompanion.Core
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service)
        {
            Type type = typeof(T);
            if (Services.ContainsKey(type))
            {
                Services[type] = service;
            }
            else
            {
                Services.Add(type, service);
            }
        }

        public static T Resolve<T>()
        {
            Type type = typeof(T);
            if (Services.TryGetValue(type, out object service))
            {
                return (T)service;
            }
            throw new Exception($"Service of type {type.Name} is not registered.");
        }
    }
}
