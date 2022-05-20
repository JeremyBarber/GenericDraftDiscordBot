using GenericDraftDiscordBot.Modules.DraftManagement.State;
using GreetingsBot.Common;

namespace GenericDraftDiscordBot.Modules
{
    public partial class DraftState : IDisposable
    {
        public event EventHandler<ReadyToDealEventArgs> ReadyToDeal;
        public event EventHandler<EventArgs> DraftCompleted;

        private readonly CancellationTokenSource CancellationTokenSource = new();

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
                Task.Delay(CycleTime);

                if (UserItemBank.Values.All(x => x.Count == FinalBankSize))
                {
                    DraftCompleted?.Invoke(this, EventArgs.Empty);
                    Finished = true;
                    break;
                }

                if (UserItemBank.Values.All(x => x.Count == Round))
                {
                    Round++;
                    RotateUserHands();
                    ReadyToDeal?.Invoke(this, new ReadyToDealEventArgs(Id, Round, DealOutHands()));
                }
            }
        }

        private void RotateUserHands()
        {
            foreach (var channel in Channels)
            {
                var user = channel.Name;
                UserHandAssignments[user] = UserHandAssignments[user]++ % Channels.Count;
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
