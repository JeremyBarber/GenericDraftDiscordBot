using CsvHelper;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GenericDraftDiscordBot.Modules.DraftManagement.Helpers;
using GenericDraftDiscordBot.Modules.DraftManagement.State;
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
            // Create a new DraftState and subscribe to its events
            var newDraft = new DraftState(phrase, description, message, owner)
            {
                InitialHandSize = initialHandSize,
                FinalBankSize = finalBankSize
            };

            newDraft.ReadyToDeal += OnReadyToDeal;
            newDraft.DraftCompleted += OnSendFinalStatement;

            DraftStates.Add(phrase, newDraft);
        }

        public async Task<int> AssignItemsToDraft(string id, IUser caller, Uri url)
        {
            ValidateOwnership(id, caller);

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
            ValidateOwnership(id, caller);

            await ChannelManager.RemovePrivateChannels(id);

            DraftStates[id].Dispose();
            DraftStates.Remove(id);

        }

        public string GetStatusOfDraft(string id)
        {
            Validate(id);

            return DraftStates[id].Status();
        }

        private readonly EventHandler<DraftStateBroadcastEventArgs> OnReadyToDeal = async (sender, eventArgs) =>
        {
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

                    menuBuilder.AddOption(item[0].ToString(), i.ToString());

                    var field = new EmbedFieldBuilder().WithName(item[0].ToString());
                    var valueString = new StringBuilder();
                    foreach (var itemProperty in item.Keys)
                    {
                        valueString.Append($"{itemProperty} - {item[itemProperty]}, ");
                    }
                    field = field.WithValue(valueString.ToString());

                    fieldBuilder.Add(field);
                }

                await StandardMessageWithMenu(eventArgs.MessageChannels[user], $"{user.Username}, please make your next selection for Round {eventArgs.Round} of Draft {eventArgs.Id}", fieldBuilder, menuBuilder);
            }
        };

        private async Task OnProcessUserDraftSelection(SocketMessageComponent arg)
        {
            var user = arg.User;
            var id = arg.Data.CustomId;
            var choice = int.Parse(arg.Data.Values.Single());

            Validate(id);

            var item = DraftStates[id].BankItemSelection(user, choice);

            var field = new EmbedFieldBuilder().WithName("Choice").WithValue($"{choice} - {item[0].ToString()}");
            await StandardMessage(arg.Channel, "Please wait while the other Users make their selections", new List<EmbedFieldBuilder> { field });
            await arg.Message.DeleteAsync();
        }

        private readonly EventHandler<DraftStateBroadcastEventArgs> OnSendFinalStatement = async (sender, eventArgs) =>
        {
            var fieldBuilder = new List<EmbedFieldBuilder>();
            foreach (var userItems in eventArgs.UserItemBank)
            {
                var user = userItems.Key;
                var items = userItems.Value;

                var field = new EmbedFieldBuilder().WithName(user.Username);
                var valueString = new StringBuilder();
                foreach (var item in items)
                {
                    valueString.Append($"{item[0].ToString()}, ");
                }
                field.WithValue(valueString.ToString());

                await StandardMessage(eventArgs.MessageChannels[user], $"{user}, this Draft has ended. You selection is:", new List<EmbedFieldBuilder> { field });

                fieldBuilder.Add(field);
            }

            var mainChannel = eventArgs.Message.Channel;
            await StandardMessage(mainChannel, $"{mainChannel}, this Draft has ended. Selections are as follows:", fieldBuilder);
        };

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
                .WithColor(Color.Teal)
                .WithFooter("This action was triggered by an event")
                .WithTitle(title)
                .WithFields(fields)
                .Build();

            await channel.SendMessageAsync("", embed: embeddedMessage);
        }

        private static async Task StandardMessageWithMenu(IMessageChannel channel, string title, List<EmbedFieldBuilder> fields, SelectMenuBuilder menuItems)
        {
            var embeddedMessage = new EmbedBuilder()
                .WithColor(Color.Teal)
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

            await channel.SendMessageAsync("", embed: embeddedMessage, components: builder);
        }
    }
}