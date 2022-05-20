using Discord;
using System.Diagnostics.CodeAnalysis;

namespace GenericDraftDiscordBot.Modules.DraftManagement.Helpers
{
    internal class UserIdEqualityComparer : IEqualityComparer<IUser>
    {
        public bool Equals(IUser? x, IUser? y) => x?.Id == y?.Id;

        public int GetHashCode([DisallowNull] IUser obj) => obj.Id.GetHashCode();
    }
}
