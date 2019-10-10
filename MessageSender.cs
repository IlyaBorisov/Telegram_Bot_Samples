using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace Telegram_Bot_Samples
{
    class Message
    {
        public int Id { get; set; }
        public int TgId { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public string Phone { get; set; }
    }
    class MessageSender
    {
        private readonly static string connStr;
        private readonly ILogger<MessageSender> _logger;
        public MessageSender(ILogger<MessageSender> logger) => _logger = logger;
        static MessageSender() =>
            connStr = Common.GetParameter<string>("appsettings.json", "connStr");
        public static async Task<bool> TimerCheckForUnsendAsync()
        {
            using var conn = new SqlConnection(connStr);
            using var cmd = new SqlCommand("select top 1 1 from sourcemessages where Processed=0", conn);
            await conn.OpenAsync().ConfigureAwait(false);
            return (int)(await cmd.ExecuteScalarAsync().ConfigureAwait(false) ?? 0) == 1;
        }
        public async Task SendMessages()
        {
            var attempts = Common.GetParameter<int>("appsettings.json", "maxAttempts");
            var client = Bot.Client;
            while (attempts-- > 0)
            {
                try
                {
                    var messages = new List<Message>();
                    using (var conn = new SqlConnection(connStr))
                    {                        
                        await conn.OpenAsync().ConfigureAwait(false);
                        var cmd = conn.CreateCommand();
                        cmd.Connection = conn;
                        cmd.CommandText = @"select someMessages
                                            from source_messages
                                            where Processed = 0";

                        using (var adapter = new SqlDataAdapter(cmd))
                        using (var dataset = new DataSet())
                        {
                            adapter.Fill(dataset);                     
                            foreach (DataRow row in dataset.Tables[0].Rows)
                            {
                                messages.Add(new Message
                                {
                                    Id = row.Field<int>("Id"),
                                    TgId = row.Field<int>("telegram_id"),
                                    Title = row.Field<string>("Title") ?? "",
                                    Text = row.Field<string>("Body") ?? "",
                                    Phone = row.Field<string>("Phone") ?? ""
                                });
                            }
                            _logger.LogInformation($"List of messages received from database");
                        }
                        if (messages.Count == 0)
                        {
                            _logger.LogWarning("List of messages is empty.");
                            await client.SendTextMessageAsync(Common.GetChatIdToLog(), "List of messages is empty.").ConfigureAwait(false);
                            break;
                        }                            
                        int tgOrds = 0;
                        messages.ForEach((mes) =>
                        {
                            if (mes.TgId != 0)
                                tgOrds++;
                        });
                        _logger.LogInformation($"There are {messages.Count} messages and {tgOrds} are nonnull");

                        foreach (var message in messages)
                        {
                            try
                            {
                                if (message.TgId == 0 || (message.Text == "" && message.Title == ""))
                                {                                    
                                    cmd.CommandText = $"DELETE FROM source_messages WHERE Id={message.Id}";
                                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) == 0)
                                        throw new Exception("Deletion affected 0 rows");
                                    _logger.LogInformation($"Message {message.Id} with tgId=0 deleted from database");
                                }
                                else
                                {
                                    cmd.CommandText = $"UPDATE source_messages SET Processed=1, UpdateTime=getdate() WHERE Id={message.Id} AND Processed=0";
                                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) == 0)
                                        throw new Exception($"Nonexistent message Id={message.Id}, TgIg={message.TgId}, Phone={message.Phone}");
                                    await client.SendTextMessageAsync(message.TgId, $"{message.Title} {message.Text}").ConfigureAwait(false);
                                    _logger.LogInformation($"Message {message.Id} was sent to user {message.TgId} and send field updated to 1");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex.Message);
                                await client.SendTextMessageAsync(Common.GetChatIdToLog(), ex.Message).ConfigureAwait(false);
                            }                            
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{ex.Message} in attempts left {attempts}");
                    await client.SendTextMessageAsync(Common.GetChatIdToLog(), $"{ex.Message} in attempts left {attempts}").ConfigureAwait(false);
                    await Task.Delay(1000);
                    continue;
                }
            }
        }
    }
}
