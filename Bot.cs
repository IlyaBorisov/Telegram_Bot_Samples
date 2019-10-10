using Microsoft.Extensions.Logging;
using System;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Telegram_Bot_Samples
{
    class Bot
    {
        private readonly ILogger<Bot> _logger;
        public static TelegramBotClient Client { get; private set; }
        public Bot(ILogger<Bot> logger) => _logger = logger;
        public void CreateClient()
        {
            var key = Common.GetParameter<string>("appsettings.json", "Key");
            Client = new TelegramBotClient(key);
            _logger.LogInformation("Telegram client created");
            Client.OnMessage += Client_OnMessageReceived;
            Client.StartReceiving();
        }

        private async void Client_OnMessageReceived(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            switch (e.Message.Text)
            {
                case "/start":
                    {
                        var replyKeybord = new ReplyKeyboardMarkup(
                            new[] { new KeyboardButton("Отправить телефон") { RequestContact = true } },
                            resizeKeyboard: true);
                        await Client.SendTextMessageAsync(e.Message.Chat.Id,
                            $"Здравствуйте, {e.Message.From.FirstName}! " +
                            $"Для подписки на уведомления нажмите на кнопку -Отправить телефон- в самом низу экрана, пожалуйста", replyMarkup: replyKeybord).ConfigureAwait(false);
                        _logger.LogInformation($"Phone request message sent to {e.Message.Chat.Id}");
                        break;
                    }
                default:
                    {
                        if (e.Message.Type == MessageType.Contact)
                        {
                            _logger.LogInformation($"Message with contact received from {e.Message.Chat.Id}");
                            bool success = false;
                            var attempts = Common.GetParameter<int>("appsettings.json", "maxAttempts");
                            var connStr = Common.GetParameter<string>("appsettings.json", "connStr");
                            while (attempts-- > 0)
                            {
                                try
                                {
                                    using (var conn = new SqlConnection(connStr))
                                    {
                                        conn.Open();
                                        var cmd = conn.CreateCommand();
                                        cmd.CommandText = "UPDATE source " +
                                                         "SET telegram_id=@tid " +
                                                         "WHERE RIGHT(cleannum(phone),10)=@patt";
                                        cmd.Parameters.AddWithValue("tid", e.Message.Chat.Id);
                                        var truephone = Regex.Replace(e.Message.Contact.PhoneNumber, @"^\d$", "");
                                        if (truephone.Length > 10)
                                            truephone = truephone.Substring(truephone.Length - 10);
                                        cmd.Parameters.AddWithValue("patt", truephone);
                                        if (cmd.ExecuteNonQuery() > 0)
                                        {
                                            success = true;
                                            _logger.LogInformation($"Contact {e.Message.Chat.Id} found in database");
                                            break;
                                        }
                                        else
                                        {
                                            await Client.SendTextMessageAsync(e.Message.Chat.Id,
                                            $"Извините, ваш номер {e.Message.Contact.PhoneNumber} не найден в базе.",
                                            replyMarkup: new ReplyKeyboardRemove()).ConfigureAwait(false);
                                            _logger.LogInformation($"Contact {e.Message.Chat.Id} not found in database");
                                            return;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"{ex.Message} in attempts left {attempts}");
                                    await Client.SendTextMessageAsync(Common.GetChatIdToLog(), $"{ex.Message} in attempts left {attempts}");
                                    await Task.Delay(1000);
                                    continue;
                                }
                            }
                            if (success)
                            {
                                await Client.SendTextMessageAsync(e.Message.Chat.Id,
                                    $"Спасибо, ваш номер {e.Message.Contact.PhoneNumber} занесен в базу.",
                                    replyMarkup: new ReplyKeyboardRemove());
                                await Client.SendTextMessageAsync(Common.GetChatIdToLog(), $"партнер {e.Message.Contact.PhoneNumber} added");
                                _logger.LogInformation($"Success message sent to {e.Message.Chat.Id}");
                            }                                
                            else
                            {
                                await Client.SendTextMessageAsync(e.Message.Chat.Id,
                                    $"База данных временно недоступна, повторите попытку позже.");
                                _logger.LogInformation($"Error message sent to {e.Message.Chat.Id}");
                            }                                
                        }
                        break;
                    }
            }
        }
    }
}
