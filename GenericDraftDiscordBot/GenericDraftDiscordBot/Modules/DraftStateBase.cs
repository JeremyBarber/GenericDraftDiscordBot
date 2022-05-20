using Discord;
using System.Collections.Specialized;

namespace GenericDraftDiscordBot.Modules
{
    public partial class DraftState
    {
        protected readonly List<OrderedDictionary> Items = new();
        protected readonly List<IMessageChannel> Channels = new();

        protected bool Started = false;
        protected bool Finished = false;


        private void ThrowIfStarted()
        {
            if (Started)
            {
                throw new UserFacingException("Sorry, but the draft has already started and you can no longer make changes");
            }
        }

        public int SetItems(List<OrderedDictionary> items)
        {
            ThrowIfStarted();

            Items.Clear();
            Items.AddRange(items);
            return Items.Count;
        }

        public int SetChannels(List<IMessageChannel> channels)
        {
            ThrowIfStarted();

            Channels.Clear();
            Channels.AddRange(channels);
            return Channels.Count;
        }

        public List<OrderedDictionary> ViewDraftItems()
        {
            return Items;
        }

        public List<IMessageChannel> ViewChannels()
        {
            return Channels;
        }
    }
}
