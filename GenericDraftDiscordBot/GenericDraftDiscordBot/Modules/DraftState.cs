using Discord;
using GreetingsBot.Common;
using Medallion;
using System.Collections.Specialized;
using System.Text;

namespace GenericDraftDiscordBot.Modules
{
    public partial class DraftState
    {
        public readonly string Id;
        public readonly string Description;
        public readonly IUser Owner;
        public readonly IUserMessage Message;

        public int InitialHandSize { get; set; }
        public int FinalBankSize { get; set; }

        private int Round = 0;

        private readonly Dictionary<string, List<OrderedDictionary>> UserItemBank = new();
        private readonly Dictionary<int, List<OrderedDictionary>> DraftingHands = new();
        private readonly Dictionary<string, int> UserHandAssignments = new();

        private int RequiredNumberOfItems => InitialHandSize * Channels.Count;

        public DraftState(string id, string description, IUserMessage message, IUser owner)
        {
            Id = id;
            Description = description;
            Message = message;
            Owner = owner;

            Logger.Log(LogSeverity.Info, id, $"{Owner.Username} created new DraftState with Id {id} for '{Description}'");
        }

        public void Start()
        {
            ThrowIfUnableToStart();

            // Set a flag to prevent usage of Update methods
            Started = true;

            for (var i = 0; i < Channels.Count; i++)
            {
                UserItemBank.Add(Channels[i].Name, new List<OrderedDictionary>());
                DraftingHands.Add(i, new List<OrderedDictionary>());
            }

            // Pull items from it and place them sequentially into the hands
            Items.Shuffle(new Random());

            for (var i = 0; i < RequiredNumberOfItems; i++)
            {
                DraftingHands[i % Channels.Count].Add(Items[i]);
            }

            // Assign the hands to the players
            for (var i = 0; i < Channels.Count; i++)
            {
                UserHandAssignments[Channels[i].Name] = i;
            }

            // Start the process for checking the draft pick state
            StartBackgroundProcess();
        }

        public Dictionary<IMessageChannel, List<OrderedDictionary>> DealOutHands()
        {
            var hands = new Dictionary<IMessageChannel, List<OrderedDictionary>>();

            foreach (var channel in Channels)
            {
                hands.Add(channel, DraftingHands[UserHandAssignments[channel.Name]]);
            }

            return hands;
        }

        public OrderedDictionary UpdateUserSelection(string username, int choice)
        {
            // Check if the bank for the user equals the Round number. If so, throw
            if (UserItemBank[username].Count == Round)
            {
                throw new UserFacingException("Sorry, I have to accept your first answer!");
            }

            // Update the bank for the user
            var chosenItem = DraftingHands[UserHandAssignments[username]][choice];
            DraftingHands[UserHandAssignments[username]].RemoveAt(choice);
            UserItemBank[username].Add(chosenItem);

            return chosenItem;
        }

        public string Status()
        {
            var statusStringBuilder = new StringBuilder();

            if (!Started && !Finished)
            {
                statusStringBuilder.AppendLine("The draft has not yet begun.");
            }
            else if (Started && !Finished)
            {
                statusStringBuilder.AppendLine("The draft is in progress.");
            }
            else if (Finished)
            {
                statusStringBuilder.AppendLine("The draft has finished.");
            }
            else
            {
                throw new InvalidOperationException("Oops.");
            }

            return statusStringBuilder.ToString();
        }

        private void ThrowIfUnableToStart()
        {
            if (InitialHandSize <= 0)
            {
                throw new UserFacingException($"You need to set a hand size before starting the draft. Currently it's set to {InitialHandSize}.");
            }

            if (FinalBankSize > InitialHandSize || FinalBankSize <= 0)
            {
                throw new UserFacingException($"You need to set a final bank size that is less than the initial hand size before starting the draft. Currently it's set to {FinalBankSize}.");
            }

            if (Channels.Count <= 0)
            {
                throw new UserFacingException($"You need to have at least 2 players before starting the draft. Currently {Channels.Count} players are registered");
            }

            if (RequiredNumberOfItems > Items.Count)
            {
                throw new UserFacingException($"To draft for {Channels.Count} players starting with {InitialHandSize} items you require {RequiredNumberOfItems} items, but you only have {Items.Count} registered. " +
                    $"Please add more before starting the draft");
            }
        }
    }
}
