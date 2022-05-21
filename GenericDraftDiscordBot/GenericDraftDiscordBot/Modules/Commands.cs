using Discord;
using Discord.Commands;
using GenericDraftDiscordBot.Modules.Draft;
using GenericDraftDiscordBot.Modules.DraftManagement.Helpers;
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

                var title = $"{owner.Username} has created a new Draft";
                var fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder().WithName("Hand Size").WithValue(initialHandSize).WithIsInline(true),
                    new EmbedFieldBuilder().WithName("Items To Pick").WithValue(finalBankSize).WithIsInline(true),
                    new EmbedFieldBuilder().WithName("Description").WithValue(description),
                    new EmbedFieldBuilder().WithName("ID").WithValue(phrase)
                };

                var message = await CommandResponse(title, fields);
                await message.AddReactionAsync(DraftStateManager.GetRegistrationEmote());

                DraftStateManager.CreateNew(owner, message, phrase, description, initialHandSize, finalBankSize);
            });

        [Command("DraftItems", RunMode = RunMode.Async)]
        public async Task Items(string id) => await RunWithStandardisedErrorHandling(async () =>
            {
                var itemsRegistered = await DraftStateManager.AssignItemsToDraft(id, Context.Message.Author, Context.Message.Attachments);

                var title = $"Draft '{id}' has {itemsRegistered} items registered";
                var fields = new List<EmbedFieldBuilder>();

                await CommandResponse(title, fields);
            });

        [Command("DraftStart", RunMode = RunMode.Async)]
        public async Task Start(string id) => await RunWithStandardisedErrorHandling(async () =>
            {
                await DraftStateManager.StartDraft(Context, id, Context.Message.Author);

                var title = $"Draft '{id}' has begun";
                var fields = new List<EmbedFieldBuilder>();

                await CommandResponse(title, fields);
            });

        [Command("DraftStop", RunMode = RunMode.Async)]
        public async Task Cancel(string id) => await RunWithStandardisedErrorHandling(async () =>
            {
                DraftStateManager.StopDraft(id, Context.Message.Author);

                var title = $"Draft '{id}' has been stopped";
                var fields = new List<EmbedFieldBuilder>();

                await CommandResponse(title, fields);
            });

        [Command("DraftStatus", RunMode = RunMode.Async)]
        public async Task Status(string id) => await RunWithStandardisedErrorHandling(async () =>
            {
                var status = DraftStateManager.GetStatusOfDraft(id);

                var title = $"Draft '{id}' status";
                var fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder().WithName(id).WithValue(status)
                };

                await CommandResponse(title, fields);
            });

        [Command("DraftHelp", RunMode = RunMode.Async)]
        public async Task Help() => await RunWithStandardisedErrorHandling(async () =>
            {
                var title = $"GenericDraftBot Command Listing";
                var fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder().WithName("!DraftHelp").WithValue("Shows details of the available commands"),
                    new EmbedFieldBuilder().WithName("!DraftSetup [HandSize <int>] [BankSize <int>] [Description <string>]").WithValue("Register a new Draft and define the initial config"),
                    new EmbedFieldBuilder().WithName("!DraftItems [ID <string>] [Items <.csv attachment>]").WithValue("Set what items will be used for a particular Draft. Attachment must be .csv with header row and first column as a unique id"),
                    new EmbedFieldBuilder().WithName("!DraftStart [ID <string>]").WithValue("Begin the Draft"),
                    new EmbedFieldBuilder().WithName("!DraftCancel [ID <string>] ").WithValue("Stop and delete a Draft"),
                    new EmbedFieldBuilder().WithName("!DraftStatus [ID <string>] ").WithValue("Provide information about the state of a Draft")
                };

                await CommandResponse(title, fields);
            });

        private async Task RunWithStandardisedErrorHandling(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex) when (ex is UserFacingException)
            {
                Logger.Log(LogSeverity.Warning, nameof(Commands), $"Sending user exception {ex.Message}");

                var title = ex.Message;
                var fields = new List<EmbedFieldBuilder>();

                await CommandResponse(title, fields);
            }
            catch (Exception ex)
            {
                Logger.Log(LogSeverity.Error, nameof(Commands), ex.Message);

                var title = $"Sorry, GenericDraftBot was unable to complete your requested action due to an internal error.";
                var fields = new List<EmbedFieldBuilder>();

                await CommandResponse(title, fields);
            }
        }

        private async Task<IUserMessage> CommandResponse(string title, List<EmbedFieldBuilder> fields)
        {
            var embeddedMessage = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithFooter($"This action was triggered by '{Context.Message.Author.Username}: {Context.Message.Content}'")
                .WithTitle(title)
                .WithFields(fields)
                .Build();

            Logger.Log(LogSeverity.Info, nameof(Commands), $"Posting message titled '{title}'");

            await Context.Message.DeleteAsync();

            var message = await ReplyAsync("", embed: embeddedMessage);

            return message;
        }
    }
}