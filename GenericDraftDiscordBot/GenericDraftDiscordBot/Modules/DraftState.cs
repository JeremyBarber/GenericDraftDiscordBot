using Discord.WebSocket;
using GreetingsBot.Common;
using Medallion;
using System.Text;

namespace GenericDraftDiscordBot.Modules
{
    internal partial class DraftState : DraftStateBase
    {
        public readonly string Id;
        public readonly string Description;
        public readonly SocketUser Owner;

        public int InitialHandSize { get; set; }
        public int FinalBankSize { get; set; }

        private int Round = 0;

        private readonly Dictionary<SocketUser, List<string>> UserItemBank = new();
        private readonly Dictionary<int, List<string>> DraftingHands = new();
        private readonly Dictionary<SocketUser, int> UserHandAssignments = new();

        private int RequiredNumberOfItems => InitialHandSize * Users.Count;

        public DraftState(string id, string description, SocketUser owner)
        {
            Id = id;
            Description = description;
            Owner = owner;

            Logger.Log(Discord.LogSeverity.Info, id, $"{Owner.Username} created new DraftState with Id {id} for '{Description}'");
        }

        public void Start()
        {
            ThrowIfUnableToStart();

            // Set a flag to prevent usage of Update methods
            Started = true;

            for (var i = 0; i < Users.Count; i++)
            {
                UserItemBank.Add(Users[i], new List<string>());
                DraftingHands.Add(i, new List<string>());
            }

            // Pull items from it and place them sequentially into the hands
            Items.Shuffle(new Random());

            for (var i = 0; i < RequiredNumberOfItems; i++)
            {
                DraftingHands[i % Users.Count].Add(Items[i]);
            }

            // Assign the hands to the players
            for (var i = 0; i < Users.Count; i++)
            {
                UserHandAssignments[Users[i]] = i;
            }

            // Start the process for checking the draft pick state
            StartBackgroundProcess();
        }

        public Dictionary<SocketUser, List<string>> DealOutHands()
        {
            var hands = new Dictionary<SocketUser, List<string>>();

            foreach (var user in Users)
            {
                hands.Add(user, DraftingHands[UserHandAssignments[user]]);
            }

            return hands;
        }

        public string UpdateUserSelection(SocketUser user, int choice)
        {
            // Check if the bank for the user equals the Round number. If so, throw
            if (UserItemBank[user].Count == Round)
            {
                throw new InvalidOperationException("Sorry, I have to accept your first answer!");
            }

            // Update the bank for the user
            var chosenItem = DraftingHands[UserHandAssignments[user]][choice];
            DraftingHands[UserHandAssignments[user]].RemoveAt(choice);
            UserItemBank[user].Add(chosenItem);

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
                throw new InvalidOperationException($"You need to set a hand size before starting the draft. Currently it's set to {InitialHandSize}.");
            }

            if (FinalBankSize > InitialHandSize || FinalBankSize <= 0)
            {
                throw new InvalidOperationException($"You need to set a final bank size that is less than the initial hand size before starting the draft. Currently it's set to {FinalBankSize}.");
            }

            if (Users.Count <= 1)
            {
                throw new InvalidOperationException($"You need to have at least two players before starting the draft. Currently only {Users.Count} players are registered");
            }

            if (RequiredNumberOfItems > Items.Count)
            {
                throw new InvalidOperationException($"To draft for {Users.Count} players starting with {InitialHandSize} items you require {RequiredNumberOfItems} items, but you only have {Items.Count} registered. " +
                    $"Please add more before starting the draft");
            }
        }
    }
}
