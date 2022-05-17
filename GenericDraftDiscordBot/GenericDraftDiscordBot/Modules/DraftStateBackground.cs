using GreetingsBot.Common;

namespace GenericDraftDiscordBot.Modules
{
    internal partial class DraftState : IDisposable
    {
        public event EventHandler<EventArgs> ReadyToDeal;
        public event EventHandler<EventArgs> DraftCompleted;

        private readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

        private readonly TimeSpan CycleTime = TimeSpan.FromSeconds(5);

        protected void StartBackgroundProcess()
        {
            if (!Finished)
            {
                new Task(BackgroundEventTrigger, CancellationTokenSource.Token).Start();
            }
        }

        private void BackgroundEventTrigger()
        {
            while (true)
            {
                Thread.Sleep(CycleTime);

                if (UserItemBank.Values.All(x => x.Count == FinalBankSize))
                {
                    DraftCompleted?.Invoke(this, EventArgs.Empty);
                    Finished = true;
                    break;
                }

                // If all users have the correct number of cards in their bank, rotate
                if (UserItemBank.Values.All(x => x.Count == Round))
                {
                    Round++;
                    RotateUserHands();
                    ReadyToDeal?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void RotateUserHands()
        {
            foreach (var user in Users)
            {
                UserHandAssignments[user] = UserHandAssignments[user]++ % Users.Count;
            }

            if (Round == InitialHandSize)
            {
                Finished = true;
            }
        }

        public void Dispose()
        {
            Logger.Log(Discord.LogSeverity.Info, Id, $"Disposing of Draft '{Id}'");
            CancellationTokenSource.Cancel();
        }
    }
}
