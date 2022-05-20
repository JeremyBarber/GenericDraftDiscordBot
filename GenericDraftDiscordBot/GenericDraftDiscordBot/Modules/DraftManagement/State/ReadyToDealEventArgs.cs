using Discord;
using System.Collections.Specialized;

namespace GenericDraftDiscordBot.Modules.DraftManagement.State
{
    public class ReadyToDealEventArgs : EventArgs
    {
        public readonly string Id;

        public readonly int Round;

        public readonly Dictionary<IMessageChannel, List<OrderedDictionary>> Hands;

        public ReadyToDealEventArgs(string id, int round, Dictionary<IMessageChannel, List<OrderedDictionary>> hands)
        {
            Id = id;
            Round = round;
            Hands = hands;
        }
    }
}
