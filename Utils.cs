using System;
using System.Linq;

namespace Bulldozer
{
    public static class Utils
    {
        public static bool IsOtherAssemblyLoaded(string otherAssemblyStringMatcher)
        {
            var matchingAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .ToList()
                .FindAll(ass => !string.IsNullOrEmpty(ass.FullName) && ass.FullName.ToLower().Contains(otherAssemblyStringMatcher.ToLower()))
                .ToList();
            if (matchingAssemblies.Count == 0)
            {
                Log.Debug($"No '{otherAssemblyStringMatcher}' found");
                return false;
            }

            return true;
        }
    }
}