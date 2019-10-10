using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using System.Threading.Tasks;

namespace Telegram_Bot_Samples
{
    class Program
    {
        static async Task Main()
        {
            Common.InitHandlers();
            Logger _logger = LogManager.GetCurrentClassLogger();            
            LogManager.ThrowExceptions = true;
            LogManager.ThrowConfigExceptions = true;
            var serviceProvider = BuildDi();

            var bot = serviceProvider.GetRequiredService<Bot>();            
            bot.CreateClient();
            _logger.Info("Telegram client started");
            try
            {
                await Bot.Client.SendTextMessageAsync(Common.GetChatIdToLog(), "Telegram client started").ConfigureAwait(false);
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                _logger.Error(ex.Message);
            }            
            var messageSender = serviceProvider.GetRequiredService<MessageSender>();
            _logger.Info("Message sender initialized");

            var interval = Common.GetParameter<double>("appsettings.json", "timerInterval");
            var timer = new System.Timers.Timer(interval);
            timer.Elapsed += async (s, e) =>
            {
                if (await MessageSender.TimerCheckForUnsendAsync().ConfigureAwait(false))
                {
                    timer.Stop();
                    _logger.Info("There are unsend messages");
                    try
                    {
                        await Bot.Client.TestApiAsync();
                        await messageSender.SendMessages().ConfigureAwait(false);
                    }
                    catch (System.Net.Http.HttpRequestException ex)
                    {
                        _logger.Error(ex.Message);
                    }                    
                    timer.Start();
                    _logger.Info("Timer started after sending");
                }
            };
            _logger.Info("Timer created");
            timer.Start();
            _logger.Info("Timer started");
            try
            {
                await Task.Delay(-1);
            }
             finally
            {
                timer.Dispose();
            }          
        }
        private static ServiceProvider BuildDi()
        {
            return new ServiceCollection()
                .AddLogging(builder => {
                    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
                    builder.AddNLog(new NLogProviderOptions
                    {
                        CaptureMessageTemplates = true,
                        CaptureMessageProperties = true
                    });
                })
                .AddSingleton<MessageSender>()
                .AddSingleton<Bot>()
                .BuildServiceProvider();
        }
    }
}
