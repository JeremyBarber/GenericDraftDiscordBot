using CrypticWizard.RandomWordGenerator;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GenericDraftDiscordBot.Modules;
using static CrypticWizard.RandomWordGenerator.WordGenerator;
using RunMode = Discord.Commands.RunMode;

namespace GreetingsBot.Modules;

public class Commands : ModuleBase<ShardedCommandContext>
{
    private readonly DiscordShardedClient Client;

    public Commands(DiscordShardedClient client)
    {
        Client = client;

        Client.SelectMenuExecuted += ProcessUserDraftSelection;
    }

    private static Dictionary<string, DraftState> DraftStates { get; set; } = new Dictionary<string, DraftState>();

    [Command("DraftHelp", RunMode = RunMode.Async)]
    public async Task Help()
    {
        await ReplyAsync($"The list of commands for the Draft Bot are.....");
    }

    [Command("DraftSetup", RunMode = RunMode.Async)]
    public async Task Setup(int initialHandSize, int finalBankSize, string description)
    {
        // Generate a reasonably unique ID
        var pattern = new List<PartOfSpeech>
        {
            PartOfSpeech.adv,
            PartOfSpeech.adj,
            PartOfSpeech.noun
        };

        var phrase = new WordGenerator().GetPatterns(pattern, '-', 1).Single();

        // Create a new DraftState and subscribe to its events
        var newDraft = new DraftState(phrase, description, Context.Message.Author)
        {
            InitialHandSize = initialHandSize,
            FinalBankSize = finalBankSize
        };
        //newDraft.ReadyToDeal += DealToUsers;
        //newDraft.DraftCompleted += SendFinalStatement;
        DraftStates.Add(phrase, newDraft);

        // Send a message
        var title = $"{Context.User.Username} has created a new Draft!";
        var fields = new List<EmbedFieldBuilder>
        {
            new EmbedFieldBuilder().WithName("Hand Size").WithValue(initialHandSize).WithIsInline(true),
            new EmbedFieldBuilder().WithName("Items To Pick").WithValue(finalBankSize).WithIsInline(true),
            new EmbedFieldBuilder().WithName("Decsription").WithValue(description),
            new EmbedFieldBuilder().WithName("ID").WithValue(phrase)
        };

        var message = await ReplyAsync("", embed: StandardEmbedFormatting(title, fields));
        await message.AddReactionAsync(Emoji.Parse(":white_check_mark:"));
    }

    //[Command("DraftItems", RunMode = RunMode.Async)]
    //public async Task Items()
    //{
    //    // The command must be sent with a json attachment
    //}

    //[Command("DraftStart", RunMode = RunMode.Async)]
    //public async Task Start(string id)
    //{
    //    if (DraftStates.ContainsKey(id))
    //    {
    //        DraftStates[id].Start();
    //    }
    //    else
    //    {

    //    }


    //    await Context.Interaction.RespondWithModalAsync(mb.Build());
    //}

    [Command("DraftCancel", RunMode = RunMode.Async)]
    public async Task Cancel(string id) => await RunIfValidId(id, () => 
        {
            DraftStates[id].Dispose();
            DraftStates.Remove(id);
        });

    [Command("DraftStatus", RunMode = RunMode.Async)]
    public async Task Status(string id) => await RunIfValidId(id, () => ReplyAsync(DraftStates[id].Status()));

    private async Task DealToUsers(string id)
    {
        var handsToDeal = DraftStates[id].DealOutHands();

        foreach (var kvp in handsToDeal)
        {
            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select an option")
                .WithCustomId("menu-1")
                .WithMinValues(1)
                .WithMaxValues(1); 

            for (var i = 0; i < kvp.Value.Count; i++)
            {
                menuBuilder.AddOption(kvp.Value[i], i.ToString(), "");
            }

            var builder = new ComponentBuilder()
                .WithSelectMenu(menuBuilder);

            await kvp.Key.SendMessageAsync("Select next item", components: builder.Build());
        }
    }

    private async Task SendFinalStatement(string id)
    {

    }

    private async Task ProcessUserDraftSelection(SocketMessageComponent arg)
    {
        var text = string.Join(", ", arg.Data.Values);
        await arg.RespondAsync($"You have selected {text}");
    }

    private async Task RunIfValidId(string id, Action action)
    {
        if (DraftStates.ContainsKey(id))
        {
            action();
        }
        else
        {
            await ReplyAsync($"Sorry, the id '{id}' does not appear to refer to any known Draft.");
        }
    }

    private Embed StandardEmbedFormatting(string title, List<EmbedFieldBuilder> fields) => 
        new EmbedBuilder()
            .WithColor(Color.Blue)
            .WithFooter("This action was performed by a bot")
            .WithTitle(title)
            .WithFields(fields)
            .Build();
}