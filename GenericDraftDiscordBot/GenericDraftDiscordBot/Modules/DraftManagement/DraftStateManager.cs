using CsvHelper;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GenericDraftDiscordBot.Modules.DraftManagement.State;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;

namespace GenericDraftDiscordBot.Modules.Draft
{
    public class DraftStateManager : IDraftStateManager
    {
        public readonly Dictionary<string, DraftState> DraftStates = new();

        public IEmote GetRegistrationEmote() => Emoji.Parse(":white_check_mark:");

        public DraftStateManager(DiscordShardedClient client)
        {
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
            //newDraft.DraftCompleted += SendFinalStatement;

            // Save the message so we can find it later
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

            DraftStates[id].SetItems(items);

            return items.Count;
        }

        public async Task StartDraft(ShardedCommandContext context, string id, IUser caller)
        {
            ValidateOwnership(id, caller);

            var registeredUsers = await DraftStates[id].Message
                .GetReactionUsersAsync(GetRegistrationEmote(), 100)
                .FlattenAsync();

            var registeredHumanUsers = registeredUsers.Where(x => !x.IsBot).ToList();

            var denyView = new OverwritePermissions(viewChannel: PermValue.Deny);
            var allowView = new OverwritePermissions(viewChannel: PermValue.Allow);

            var channels = new List<IMessageChannel>();

            var category = await context.Guild.CreateCategoryChannelAsync(id);
            foreach (var user in registeredHumanUsers)
            {
                var newChannel = await context.Guild.CreateTextChannelAsync(user.Username, prop => prop.CategoryId = category.Id);
                
                await newChannel.AddPermissionOverwriteAsync(context.Guild.EveryoneRole, denyView);
                await newChannel.AddPermissionOverwriteAsync(user, allowView);

                channels.Add(newChannel);
            }

            DraftStates[id].SetChannels(channels);
            DraftStates[id].Start();
        }

        public void CancelDraft(string id, IUser caller)
        {
            ValidateOwnership(id, caller);

            DraftStates[id].Dispose();
            DraftStates.Remove(id);
        }

        public string GetStatusOfDraft(string id)
        {
            return DraftStates[id].Status();
        }

        private readonly EventHandler<ReadyToDealEventArgs> OnReadyToDeal = async (sender, eventArgs) =>
        {
            foreach (var userItems in eventArgs.Hands)
            {
                var username = userItems.Key;
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

                await StandardMessage(username, $"{username}, please make your next selection for Round {eventArgs.Round} of Draft {eventArgs.Id}", fieldBuilder, menuBuilder);
            }
        };

        private async Task OnProcessUserDraftSelection(SocketMessageComponent arg)
        {
            var channel = arg.Channel;
            var id = arg.Data.CustomId;
            var choice = int.Parse(arg.Data.Values.Single());

            Validate(id);

            DraftStates[id].UpdateUserSelection(channel.Name, choice);

            await arg.RespondAsync($"Your choice of {choice} has been recorded. Please wait while the other User's make their selections.");
        }

        private async Task SendFinalStatement(string id)
        {
            // delete all the channels, delete the category
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

        private static async Task StandardMessage(IMessageChannel channel, string title, List<EmbedFieldBuilder> fields, SelectMenuBuilder menuItems)
        {
            var embeddedMessage = new EmbedBuilder()
                .WithColor(Color.Teal)
                .WithFooter("This action was performed by a bot")
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
