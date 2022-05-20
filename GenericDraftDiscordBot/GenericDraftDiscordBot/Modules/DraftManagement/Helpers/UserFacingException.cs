namespace GenericDraftDiscordBot.Modules.DraftManagement.Helpers
{
    internal class UserFacingException : Exception
    {
        public UserFacingException(string message) : base(message) { }
    }
}
