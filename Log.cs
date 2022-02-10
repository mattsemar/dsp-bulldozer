using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace Bulldozer
{
    public class Log
    {
        public static ManualLogSource logger;

        public static void LogAndPopupMessage(string message, bool playSound = false)
        {
            UIRealtimeTip.Popup(message, playSound);
            logger.LogWarning($"Popped up message {message}");
        }
        
        
        public static void Debug(string message)
        {
            logger.LogDebug($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }
        public static void Warn(string message)
        {
            logger.LogWarning($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
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
        
        public static void Trace(string msg)
        {
#if DEBUG
            logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
#endif
        }
    }
}