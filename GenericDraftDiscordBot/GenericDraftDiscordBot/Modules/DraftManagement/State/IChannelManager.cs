using Discord;
using Discord.Commands;

namespace GenericDraftDiscordBot.Modules.DraftManagement.State
{
    public interface IChannelManager
    {
        Task<Dictionary<IUser, IMessageChannel>> CreatePrivateChannels(string id, ShardedCommandContext context, List<IUser> users);
        Task RemovePrivateChannels(string id);
    }
}