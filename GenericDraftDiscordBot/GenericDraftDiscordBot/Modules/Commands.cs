using Discord;
using Discord.Commands;
using GenericDraftDiscordBot.Modules;
using GenericDraftDiscordBot.Modules.Draft;
using GreetingsBot.Common;
using RunMode = Discord.Commands.RunMode;

namespace GreetingsBot.Modules
{
    public class Commands : ModuleBase<ShardedCommandContext>
    {
        private readonly IDraftStateManager DraftStateManager;
        private readonly IPassphraseGenerator PassphraseGenerator;

        public Commands(IDraftStateManager draftStateManager, IPassphraseGenerator passphraseGenerator)
        {
            DraftStateManager = draftStateManager;
            PassphraseGenerator = passphraseGenerator;
        }

        [Command("DraftSetup", RunMode = RunMode.Async)]
        public async Task Setup(int initialHandSize, int finalBankSize, string description) => await RunWithStandardisedErrorHandling(async () =>
            {
                var owner = Context.User;
                var phrase = PassphraseGenerator.GetNew();

                var title = $"{owner.Username} has created a new Draft!";
                var fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder().WithName("Hand Size").WithValue(initialHandSize).WithIsInline(true),
                    new EmbedFieldBuilder().WithName("Items To Pick").WithValue(finalBankSize).WithIsInline(true),
                    new EmbedFieldBuilder().WithName("Description").WithValue(description),
                    new EmbedFieldBuilder().WithName("ID").WithValue(phrase)
                };

                var message = await StandardReply(title, fields);
                await message.AddReactionAsync(DraftStateManager.GetRegistrationEmote());

                DraftStateManager.CreateNew(owner, message, phrase, description, initialHandSize, finalBankSize);
            });

        [Command("DraftItems", RunMode = RunMode.Async)]
        public async Task Items(string id) => await RunWithStandardisedErrorHandling(async () =>
            {
                var url = new Uri(Context.Message.Attachments.Single().Url);

                var itemsRegistered = await DraftStateManager.AssignItemsToDraft(id, Context.Message.Author, url);

                var title = $"{Context.User.Username} has registered {itemsRegistered} items in this draft";
                var fields = new List<EmbedFieldBuilder>();

                await StandardReply(title, fields);
            });

        [Command("DraftStart", RunMode = RunMode.Async)]
        public async Task Start(string id) => await RunWithStandardisedErrorHandling(async () =>
            {
                await DraftStateManager.StartDraft(Context, id, Context.Message.Author);

                var title = $"Draft Has Begun!";
                var fields = new List<EmbedFieldBuilder>();

                await StandardReply(title, fields);
            });

        [Command("DraftCancel", RunMode = RunMode.Async)]
        public async Task Cancel(string id) => await RunWithStandardisedErrorHandling(async () =>
            {
                DraftStateManager.CancelDraft(id, Context.Message.Author);

                var title = $"Draft {id} Has Been Cancelled";
                var fields = new List<EmbedFieldBuilder>();

                await StandardReply(title, fields);
            });

        [Command("DraftStatus", RunMode = RunMode.Async)]
        public async Task Status(string id) => await RunWithStandardisedErrorHandling(async () =>
            {
                var status = DraftStateManager.GetStatusOfDraft(id);

                var title = $"Draft Status";
                var fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder().WithName(id).WithValue(status)
                };

                await StandardReply(title, fields);
            });

        [Command("DraftHelp", RunMode = RunMode.Async)]
        public async Task Help() => await RunWithStandardisedErrorHandling(async () =>
            {
                var title = $"GenericDraftBot Command Listing";
                var fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder().WithName("!DraftHelp").WithValue("Shows details of the available commands"),
                    new EmbedFieldBuilder().WithName("!DraftSetup").WithValue("Register a new Draft (int: number of items in starting hands, int: number of items to draw, string: description of the draft"),
                    new EmbedFieldBuilder().WithName("!DraftItems").WithValue("Set what items will be used for a particular Draft (string: Draft id, Attachment: .csv containing header row, unique ids, and item properties"),
                    new EmbedFieldBuilder().WithName("!DraftStart").WithValue("Begin the Draft (string: Draft id)"),
                    new EmbedFieldBuilder().WithName("!DraftCancel").WithValue("Stop and delete a Draft (string: Draft id)"),
                    new EmbedFieldBuilder().WithName("!DraftStatus").WithValue("Provide information about the state of a Draft (string: Draft id)")
                };

                await StandardReply(title, fields);
            });

        private async Task RunWithStandardisedErrorHandling(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex) when (ex is UserFacingException)
            {
                Logger.Log(LogSeverity.Error, "Commands", ex.Message);

                var title = ex.Message;
                var fields = new List<EmbedFieldBuilder>();

                await StandardReply(title, fields);
            }
            catch (Exception ex)
            {
                Logger.Log(LogSeverity.Error, "Commands", ex.Message);

                var title = $"Sorry, GenericDraftBot was unable to complete your requested action due to an internal error.";
                var fields = new List<EmbedFieldBuilder>();

                await StandardReply(title, fields);
            }
        }

        private async Task<IUserMessage> StandardReply(string title, List<EmbedFieldBuilder> fields)
        {
            var embeddedMessage = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithFooter("This action was performed by a bot")
                .WithTitle(title)
                .WithFields(fields)
                .Build();

            return await ReplyAsync("", embed: embeddedMessage, messageReference: new MessageReference(Context.Message.Id));
        }
    }
}