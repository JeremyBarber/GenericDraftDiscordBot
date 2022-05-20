namespace GenericDraftDiscordBot.Modules
{
    internal class UserFacingException : Exception
    {
        public UserFacingException(string message) : base(message) { }
    }
}
