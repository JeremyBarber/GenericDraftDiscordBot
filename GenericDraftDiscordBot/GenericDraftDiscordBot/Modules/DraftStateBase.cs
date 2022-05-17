using Discord.WebSocket;

namespace GenericDraftDiscordBot.Modules
{
    internal class DraftStateBase
    {
        protected readonly List<string> Items = new();
        protected readonly List<SocketUser> Users = new();

        protected bool Started = false;
        protected bool Finished = false;

        private void ThrowIfStarted()
        {
            if (Started)
            {
                throw new InvalidOperationException("Sorry, but the draft has already started and you can no longer make changes");
            }
        }

        public int SettItems(List<string> items)
        {
            ThrowIfStarted();

            items.Clear();
            Items.AddRange(items);
            return Items.Count;
        }

        public int SetUsers(List<SocketUser> users)
        {
            ThrowIfStarted();

            Users.Clear();
            Users.AddRange(users);
            return Users.Count;
        }

        public List<string> ViewDraftItems()
        {
            return Items;
        }

        public List<SocketUser> ViewUsers()
        {
            return Users;
        }
    }
}
