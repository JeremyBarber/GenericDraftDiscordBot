using CrypticWizard.RandomWordGenerator;
using static CrypticWizard.RandomWordGenerator.WordGenerator;

namespace GenericDraftDiscordBot.Modules.DraftManagement.Helpers
{
    public class PassphraseGenerator : IPassphraseGenerator
    {
        public string GetNew()
        {
            var pattern = new List<PartOfSpeech>
                {
                    PartOfSpeech.adv,
                    PartOfSpeech.adj,
                    PartOfSpeech.noun
                };

            return new WordGenerator().GetPatterns(pattern, '-', 1).Single();
        }
    }
}
