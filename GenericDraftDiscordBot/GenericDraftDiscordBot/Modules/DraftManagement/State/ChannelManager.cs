using Discord;
using Discord.Commands;
using Discord.Rest;
using GreetingsBot.Common;

namespace GenericDraftDiscordBot.Modules.DraftManagement.State
{
    public class ChannelManager : IChannelManager
    {
        private readonly Dictionary<string, List<RestTextChannel>> ManagedChannels = new();
        private readonly Dictionary<string, ICategoryChannel> ManagedCategories = new();

        public async Task<Dictionary<IUser, IMessageChannel>> CreatePrivateChannels(string id, ShardedCommandContext context, List<IUser> users)
        {
            Logger.Log(LogSeverity.Verbose, nameof(ChannelManager), $"Creating private channels for {id}");

            var channels = new Dictionary<IUser, RestTextChannel>();

            var category = await context.Guild.CreateCategoryChannelAsync(id);
            foreach (var user in users)
            {
                var newChannel = await context.Guild.CreateTextChannelAsync(user.Username, prop => prop.CategoryId = category.Id);

                await newChannel.AddPermissionOverwriteAsync(context.Guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Deny));
                await newChannel.AddPermissionOverwriteAsync(user, new OverwritePermissions(viewChannel: PermValue.Allow));

                channels.Add(user, newChannel);
            }

            ManagedCategories.Add(id, category);
            ManagedChannels.Add(id, channels.Values.ToList());

            return channels.ToDictionary(x => x.Key, x => (IMessageChannel)x.Value);
        }

        public async Task RemovePrivateChannels(string id)
        {
            Logger.Log(LogSeverity.Verbose, nameof(ChannelManager), $"Deleting private channels for {id}");

            foreach (var channel in ManagedChannels[id])
            {
                await channel.DeleteAsync();
            }

            await ManagedCategories[id].DeleteAsync();

            ManagedChannels.Remove(id);
            ManagedCategories.Remove(id);
        }
    }
}
