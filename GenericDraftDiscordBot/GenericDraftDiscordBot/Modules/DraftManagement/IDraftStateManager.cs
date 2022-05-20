using Discord;
using Discord.Commands;

namespace GenericDraftDiscordBot.Modules.Draft
{
    public interface IDraftStateManager
    {
        IEmote GetRegistrationEmote();
        Task<int> AssignItemsToDraft(string id, IUser caller, Uri url);
        void CancelDraft(string id, IUser caller);
        void CreateNew(IUser owner, IUserMessage message, string phrase, string description, int initialHandSize, int finalBankSize);
        string GetStatusOfDraft(string id);
        Task StartDraft(ShardedCommandContext context, string id, IUser caller);
    }
}