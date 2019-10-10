using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.IO;
using System.Text;

namespace Telegram_Bot_Samples
{
    static class Common
    {
        public static int GetChatIdToLog() => GetParameter<int>("appsettings.json", "chatIdToLog");

        public static T GetParameter<T>(string configjson, string propname)
        {
            JObject parameters;
            using (var rdr = new StreamReader(configjson))
            {
                parameters = JObject.Parse(rdr.ReadToEnd());
            }
            return parameters[propname].ToObject<T>();
        }
        public static void InitHandlers()
        {
            Logger _logger;
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    _logger = LogManager.GetCurrentClassLogger();
                    var errorMessage = new StringBuilder(ex.Message + " -> ");
                    while (ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                        errorMessage.Append(ex.Message + " -> ");
                    }
                    _logger.Error($"Unhandled exception: {errorMessage}");
                    Bot.Client.SendTextMessageAsync(GetChatIdToLog(), $"Unhandled exception: {errorMessage}");
                }
            };
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                Bot.Client.SendTextMessageAsync(GetChatIdToLog(), "Telegram client stopped");
                _logger = LogManager.GetCurrentClassLogger();
                _logger.Info("Telegram client stopped");                
                LogManager.Flush();
                LogManager.Shutdown();
            };
        }
    }
}
