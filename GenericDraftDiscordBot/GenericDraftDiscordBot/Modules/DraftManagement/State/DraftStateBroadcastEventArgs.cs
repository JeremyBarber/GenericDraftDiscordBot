using Discord;
using System.Collections.Specialized;

namespace GenericDraftDiscordBot.Modules.DraftManagement.State
{
    public class DraftStateBroadcastEventArgs : EventArgs
    {
        public readonly string Id;
        public readonly int Round;
        public readonly IUser Owner;
        public readonly IUserMessage Message;
        public readonly Dictionary<IUser, List<OrderedDictionary>> UserItemBank;
        public readonly Dictionary<IUser, List<OrderedDictionary>> Hands;
        public readonly Dictionary<IUser, IMessageChannel> MessageChannels;

        public DraftStateBroadcastEventArgs(
            string id,
            int round,
            IUser owner,
            IUserMessage message,
            Dictionary<IUser, List<OrderedDictionary>> userItemBank,
            Dictionary<IUser, List<OrderedDictionary>> hands,
            Dictionary<IUser, IMessageChannel> messageChannels)
        {
            Id = id;
            Round = round;
            Owner = owner;
            Message = message;
            UserItemBank = userItemBank;
            Hands = hands;
            MessageChannels = messageChannels;
        }
    }
}
