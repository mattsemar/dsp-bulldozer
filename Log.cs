using System.Collections.Generic;
using BepInEx.Logging;

namespace Bulldozer
{
    public class Log
    {
        public static ManualLogSource logger;

        public static void LogAndPopupMessage(string message)
        {
            UIRealtimeTip.Popup(message);
            logger.LogWarning($"Popped up message {message}");
        }
        
        
        public static void Debug(string message)
        {
            logger.LogDebug(message);
        }
        public static void Warn(string message)
        {
            logger.LogWarning(message);
        }

        private static Dictionary<string, int> _logCount = new ();
        public static void LogNTimes(string msg, int maxTimes,  params object[] args)
        {
            if (!_logCount.ContainsKey(msg) || maxTimes < 0)
                _logCount[msg] = 0;
            else if (_logCount[msg] > maxTimes)
                return;
    
            _logCount[msg]++;
            Debug(string.Format(msg, args));
        }
    }
}