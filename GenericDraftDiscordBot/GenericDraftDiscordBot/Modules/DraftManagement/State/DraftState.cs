using Discord;
using GenericDraftDiscordBot.Modules.DraftManagement.Helpers;
using GenericDraftDiscordBot.Modules.DraftManagement.State;
using GreetingsBot.Common;
using Medallion;
using System.Collections.Specialized;
using System.Text;

namespace GenericDraftDiscordBot.Modules
{
    public class DraftState : IDisposable
    {
        public readonly string Id;
        public readonly string Description;
        public readonly IUser Owner;
        public readonly IUserMessage Message;

        public event EventHandler<DraftStateBroadcastEventArgs> ReadyToDeal;
        public event EventHandler<DraftStateBroadcastEventArgs> DraftCompleted;

        public int InitialHandSize
        {
            get => _initialHandSize;
            set
            {
                ThrowIfStarted();
                _initialHandSize = value;
            }
        }
        private int _initialHandSize;

        public int FinalBankSize
        {
            get => _finalBankSize;
            set
            {
                ThrowIfStarted();
                _finalBankSize = value;
            }
        }
        private int _finalBankSize;

        public List<OrderedDictionary> Items
        {
            get => _items;
            set
            {
                ThrowIfStarted();
                _items = value;
            }
        }
        private List<OrderedDictionary> _items = new();

        public Dictionary<IUser, IMessageChannel> UserChannels
        {
            get => _channels;
            set
            {
                ThrowIfStarted();
                _channels = value;
            }
        }
        private Dictionary<IUser, IMessageChannel> _channels = new(new UserIdEqualityComparer());

        private readonly Dictionary<IUser, List<OrderedDictionary>> UserItemBank = new(new UserIdEqualityComparer());
        private readonly Dictionary<int, List<OrderedDictionary>> DraftingHands = new();
        private readonly Dictionary<IUser, int> UserHandAssignments = new(new UserIdEqualityComparer());

        private readonly CancellationTokenSource CancellationTokenSource = new();
        private readonly TimeSpan CycleTime = TimeSpan.FromSeconds(5);

        private bool Started = false;
        private bool Finished = false;
        private int Round = 0;

        private int RequiredNumberOfItems => InitialHandSize * UserChannels.Count;

        public DraftState(string id, string description, IUserMessage message, IUser owner)
        {
            Id = id;
            Description = description;
            Message = message;
            Owner = owner;

            Logger.Log(LogSeverity.Verbose, nameof(DraftState), $"{Owner.Username} created new DraftState with Id {id} for '{Description}'");
        }

        public void Start()
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftState), $"Draft {Id} is starting");

            ThrowIfUnableToStart();

            Started = true;

            for (var i = 0; i < UserChannels.Count; i++)
            {
                var user = UserChannels.Keys.ToList()[i];
                UserItemBank.Add(user, new List<OrderedDictionary>());
                DraftingHands.Add(i, new List<OrderedDictionary>());
                UserHandAssignments.Add(user, i);
            }

            Items.Shuffle(new Random());
            for (var i = 0; i < RequiredNumberOfItems; i++)
            {
                DraftingHands[i % UserChannels.Count].Add(Items[i]);
            }

            Task.Run(DraftingEventTrigger);
        }

        public OrderedDictionary BankItemSelection(IUser user, int choice)
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftState), $"Draft {Id} is receiving choice {choice} from {user.Username}");

            if (UserItemBank[user].Count == Round)
            {
                throw new UserFacingException($"Sorry {user}, I have to accept your first answer");
            }

            var chosenItem = DraftingHands[UserHandAssignments[user]][choice];
            DraftingHands[UserHandAssignments[user]].RemoveAt(choice);
            UserItemBank[user].Add(chosenItem);

            return chosenItem;
        }

        public string Status()
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftState), $"Draft {Id} is producing status message");

            var statusStringBuilder = new StringBuilder();

            if (!Started && !Finished)
            {
                statusStringBuilder.AppendLine("The draft has not yet begun.");
            }
            else if (Started && !Finished)
            {
                statusStringBuilder.AppendLine($"The draft is in progress and is currently in Round {Round} of {FinalBankSize}");

                var respondedUsers = UserItemBank.Where(x => x.Value.Count == Round).Select(x => x.Key.Username);
                var waitingUsers = UserItemBank.Where(x => x.Value.Count != Round).Select(x => x.Key.Username);

                statusStringBuilder.AppendLine($"The following Users have registered a choice this round: {string.Join(", ", respondedUsers)} ");
                statusStringBuilder.AppendLine($"The following Users have yet to register a choice this round: {string.Join(", ", waitingUsers)}");
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

        private void DraftingEventTrigger()
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftState), $"Draft {Id} event trigger is starting");

            var cancellationToken = CancellationTokenSource.Token;

            while (!Finished && !cancellationToken.IsCancellationRequested)
            {
                Task.Delay(CycleTime);

                if (UserItemBank.Values.All(x => x.Count == FinalBankSize))
                {
                    Logger.Log(LogSeverity.Verbose, nameof(DraftState), $"Draft {Id} is firing event {nameof(DraftCompleted)}");
                    Finished = true;
                    DraftCompleted?.Invoke(this, BuildEventArgs());
                }
                else if (UserItemBank.Values.All(x => x.Count == Round))
                {
                    Logger.Log(LogSeverity.Verbose, nameof(DraftState), $"Draft {Id} is firing event {nameof(ReadyToDeal)}");
                    Round++;
                    RotateUserHands();
                    ReadyToDeal?.Invoke(this, BuildEventArgs());
                }
            }

            Logger.Log(LogSeverity.Verbose, nameof(DraftState), $"Draft {Id} event trigger is stopping");
        }

        private void RotateUserHands()
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftState), $"Draft {Id} is rotating user hands");

            foreach (var userChannel in UserChannels)
            {
                var user = userChannel.Key;
                UserHandAssignments[user] = UserHandAssignments[user]++ % UserChannels.Count;
            }
        }

        private DraftStateBroadcastEventArgs BuildEventArgs()
        {
            var hands = new Dictionary<IUser, List<OrderedDictionary>>(new UserIdEqualityComparer());

            foreach (var userChannel in UserChannels)
            {
                hands.Add(userChannel.Key, DraftingHands[UserHandAssignments[userChannel.Key]]);
            }

            return new DraftStateBroadcastEventArgs(Id, Round, Owner, Message, UserItemBank, hands, UserChannels);
        }

        private void ThrowIfStarted()
        {
            if (Started)
            {
                throw new UserFacingException("Sorry, but the draft has already started and you can no longer make changes");
            }
        }

        private void ThrowIfUnableToStart()
        {
            if (Finished)
            {
                throw new UserFacingException($"This draft has already completed");
            }

            if (InitialHandSize <= 0)
            {
                throw new UserFacingException($"You need to set a hand size before starting the draft. Currently it's set to {InitialHandSize}.");
            }

            if (FinalBankSize > InitialHandSize || FinalBankSize <= 0)
            {
                throw new UserFacingException($"You need to set a final bank size that is less than the initial hand size before starting the draft. Currently it's set to {FinalBankSize}.");
            }

            if (UserChannels.Count <= 0)
            {
                throw new UserFacingException($"You need to have at least 2 players before starting the draft. Currently {UserChannels.Count} players are registered");
            }

            if (RequiredNumberOfItems > Items.Count)
            {
                throw new UserFacingException($"To draft for {UserChannels.Count} players starting with {InitialHandSize} items you require {RequiredNumberOfItems} items, but you only have {Items.Count} registered. " +
                    $"Please add more before starting the draft");
            }
        }

        public void Dispose()
        {
            Logger.Log(LogSeverity.Verbose, nameof(DraftState), $"Draft {Id} is disposing");
            CancellationTokenSource.Cancel();
        }
    }
}
