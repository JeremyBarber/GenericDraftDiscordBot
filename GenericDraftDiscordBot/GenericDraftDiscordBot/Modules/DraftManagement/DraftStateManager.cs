using CsvHelper;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GenericDraftDiscordBot.Modules.DraftManagement.Helpers;
using GenericDraftDiscordBot.Modules.DraftManagement.State;
using GreetingsBot.Common;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;

namespace GenericDraftDiscordBot.Modules.Draft
{
    public class DraftStateManager : IDraftStateManager
    {
        public readonly Dictionary<string, DraftState> DraftStates = new();
        public readonly IChannelManager ChannelManager;

        public IEmote GetRegistrationEmote() => Emoji.Parse(":white_check_mark:");

        public DraftStateManager(DiscordShardedClient client, IChannelManager channelManager)
        {
            ChannelManager = channelManager;

            client.SelectMenuExecuted += OnProcessUserDraftSelection;
        }

        public void CreateNew(IUser owner,IUserMessage message, string phrase, string description, int initialHandSize, int finalBankSize)
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftStateManager), $"User {owner.Username} is requesting a new Draft");

            var newDraft = new DraftState(phrase, description, message, owner)
            {
                InitialHandSize = initialHandSize,
                FinalBankSize = finalBankSize
            };

            newDraft.ReadyToDeal += OnReadyToDeal;
            newDraft.DraftCompleted += OnSendFinalStatement;

            DraftStates.Add(phrase, newDraft);
        }

        public async Task<int> AssignItemsToDraft(string id, IUser caller, IReadOnlyCollection<Attachment> attachments)
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftStateManager), $"Request for Draft {id} to read items from file");

            ValidateOwnership(id, caller);

            var url = ValidateAttachments(attachments);

            var items = new List<OrderedDictionary>();

            // Create a new WebClient instance.
            using (var client = new HttpClient())
            using (var response = await client.GetAsync(url))
            using (var streamReader = new StreamReader(await response.Content.ReadAsStreamAsync()))
            using (var csv = new CsvReader(streamReader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader();

                while (csv.Read())
                {
                    var item = new OrderedDictionary();
                    foreach (var field in csv.HeaderRecord)
                    {
                        item.Add(field, csv.GetField(field));
                    }
                    items.Add(item);
                }
            }

            DraftStates[id].Items = items;

            return items.Count;
        }

        public async Task StartDraft(ShardedCommandContext context, string id, IUser caller)
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftStateManager), $"Request to start Draft {id}");

            ValidateOwnership(id, caller);

            var registeredUsers = await DraftStates[id].Message
                .GetReactionUsersAsync(GetRegistrationEmote(), 100)
                .FlattenAsync();

            var registeredHumanUsers = registeredUsers.Where(x => !x.IsBot).ToList();

            var channels = await ChannelManager.CreatePrivateChannels(id, context, registeredHumanUsers);

            DraftStates[id].UserChannels = channels;
            DraftStates[id].Start();
        }

        public async void StopDraft(string id, IUser caller)
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftStateManager), $"Request to stop Draft {id}");

            ValidateOwnership(id, caller);

            await ChannelManager.RemovePrivateChannels(id);

            DraftStates[id].Dispose();
            DraftStates.Remove(id);

        }

        public string GetStatusOfDraft(string id)
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftStateManager), $"Request for status of Draft {id}");

            Validate(id);

            return DraftStates[id].Status();
        }

        private readonly EventHandler<DraftStateBroadcastEventArgs> OnReadyToDeal = async (sender, eventArgs) =>
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftStateManager), $"Request to handle Draft {eventArgs.Id} event in {nameof(OnReadyToDeal)}");

            foreach (var userItems in eventArgs.Hands)
            {
                var user = userItems.Key;
                var items = userItems.Value;

                var menuBuilder = new SelectMenuBuilder()
                    .WithPlaceholder("Pick a Draft Item")
                    .WithCustomId(eventArgs.Id);

                var fieldBuilder = new List<EmbedFieldBuilder>();

                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];

                    menuBuilder.AddOption(item[0]?.ToString(), i.ToString());

                    var field = new EmbedFieldBuilder().WithName(item[0]?.ToString());
                    var valueString = new StringBuilder();
                    foreach (var itemProperty in item.Keys)
                    {
                        valueString.Append($"{itemProperty} - {item[itemProperty]}, ");
                    }
                    field = field.WithValue(valueString.ToString());

                    fieldBuilder.Add(field);
                }

                await StandardMessageWithMenu(eventArgs.MessageChannels[user], $"Round {eventArgs.Round}: {user.Username}, please make your next item", fieldBuilder, menuBuilder);
            }
        };

        private async Task OnProcessUserDraftSelection(SocketMessageComponent arg)
        {
            var user = arg.User;
            var id = arg.Data.CustomId;
            var choice = int.Parse(arg.Data.Values.Single());

            Logger.Log(LogSeverity.Verbose, nameof(DraftStateManager), $"Request to handle Draft {id} event in {nameof(OnProcessUserDraftSelection)}");

            Validate(id);

            var item = DraftStates[id].BankItemSelection(user, choice);

            var field = new EmbedFieldBuilder().WithName("Choice").WithValue($"{choice} - {item[0]?.ToString()}");
            await StandardMessage(arg.Channel, "Please wait while the other Users make their selections", new List<EmbedFieldBuilder> { field });
            await arg.Message.DeleteAsync();
        }

        private readonly EventHandler<DraftStateBroadcastEventArgs> OnSendFinalStatement = async (sender, eventArgs) =>
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftStateManager), $"Request to handle Draft {eventArgs.Id} event in {nameof(OnSendFinalStatement)}");

            var fieldBuilder = new List<EmbedFieldBuilder>();
            foreach (var userItems in eventArgs.UserItemBank)
            {
                var user = userItems.Key;
                var items = userItems.Value;

                var valueString = items.Select(x => x[0]?.ToString());

                var field = new EmbedFieldBuilder()
                    .WithName(user.Username)
                    .WithValue(string.Join(", ", valueString));

                await StandardMessage(eventArgs.MessageChannels[user], $"{user.Username}, this Draft has ended. Your selection is:", new List<EmbedFieldBuilder> { field });

                fieldBuilder.Add(field);
            }

            var mainChannel = eventArgs.Message.Channel;
            await StandardMessage(mainChannel, $"Draft' {eventArgs.Id}' has ended as follows:", fieldBuilder);
        };

        private Uri ValidateAttachments(IReadOnlyCollection<Attachment> attachments)
        {
            var validFiles = attachments.Where(x => x.Filename.EndsWith(".csv"));

            if (validFiles.Count() != 1)
            {
                throw new UserFacingException($"Sorry, we need exactly one valid .csv file attachment for this operation");
            }

            return new Uri(validFiles.Single().Url);
        }

        private void ValidateOwnership(string id, IUser caller)
        {
            Validate(id);

            if (DraftStates[id].Owner != caller)
            {
                throw new UserFacingException($"Sorry, only the owner of Draft '{id}' can take this action.");
            }
        }

        private void Validate(string id)
        {
            if (!DraftStates.ContainsKey(id))
            {
                throw new UserFacingException($"Sorry, the id '{id}' does not appear to refer to any known Draft.");
            }
        }

        private static async Task StandardMessage(IMessageChannel channel, string title, List<EmbedFieldBuilder> fields)
        {
            var embeddedMessage = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithFooter("This action was triggered by an event")
                .WithTitle(title)
                .WithFields(fields)
                .Build();

            Logger.Log(LogSeverity.Info, nameof(DraftStateManager), $"Posting message titled '{title}'");

            await channel.SendMessageAsync("", embed: embeddedMessage);
        }

        private static async Task StandardMessageWithMenu(IMessageChannel channel, string title, List<EmbedFieldBuilder> fields, SelectMenuBuilder menuItems)
        {
            var embeddedMessage = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithFooter("This action was triggered by an event")
                .WithTitle(title)
                .WithFields(fields)
                .Build();

            menuItems = menuItems
                    .WithMinValues(1)
                    .WithMaxValues(1);

            var builder = new ComponentBuilder()
                .WithSelectMenu(menuItems)
                .Build();

            Logger.Log(LogSeverity.Info, nameof(DraftStateManager), $"Posting message titled '{title}'");

            await channel.SendMessageAsync("", embed: embeddedMessage, components: builder);
        }
    }
}