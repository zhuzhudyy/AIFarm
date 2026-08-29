using System.Collections.Generic;
using NUnit.Framework;

namespace AIFarmNPC.Agent.Tests
{
    public sealed class TownResidentsTests
    {
        [Test]
        public void Defaults_HaveUniqueResidentsAndDifferentProviders()
        {
            var residents = TownResidentCatalog.CreateDefaultResidents();
            Assert.That(residents.Count, Is.EqualTo(4));
            Assert.That(residents[0].Id, Is.Not.EqualTo(residents[1].Id));
            Assert.That(residents[0].ModelConfig.Provider, Is.Not.EqualTo(residents[1].ModelConfig.Provider));
            Assert.That(residents[1].ModelConfig.Provider, Is.Not.EqualTo(residents[2].ModelConfig.Provider));
            Assert.That(residents[2].ModelConfig.Provider, Is.Not.EqualTo(residents[3].ModelConfig.Provider));
        }

        [Test]
        public void AssignModel_ChangesOnlyThatResidentRouting()
        {
            var residents = TownResidentCatalog.CreateDefaultResidents();
            var replacement = new ResidentModelConfig(ModelProviderKind.OfflineRules, "", "", "", false);
            residents[0].AssignModel(replacement);
            Assert.That(residents[0].ModelConfig.Provider, Is.EqualTo(ModelProviderKind.OfflineRules));
            Assert.That(residents[1].ModelConfig.Provider, Is.EqualTo(ModelProviderKind.Anthropic));
        }

        [Test]
        public void OpenAICompatible_ValidatesAndKeepsRuntimeKeyReady()
        {
            var config = ResidentModelConfig.OpenAICompatible(
                "https://example.test/v1/chat/completions", "demo-model", "secret-value");
            Assert.That(config.Provider, Is.EqualTo(ModelProviderKind.OpenAICompatible));
            Assert.That(config.HasApiKey(), Is.True);
            Assert.That(config.UsesRuntimeApiKey, Is.True);
            Assert.Throws<System.ArgumentException>(() =>
                ResidentModelConfig.OpenAICompatible("not-a-url", "demo", "secret"));
        }

        [Test]
        public void SocialCue_UsesWorldStateButKeepsPersonaSpecificAngleAndEmoji()
        {
            var residents = TownResidentCatalog.CreateDefaultResidents();
            var world = new WorldObservation(2, 11f, 100,
                new Dictionary<string, int> { { "fertilizer", 8 }, { "carrotseed", 12 } },
                new[] { new PlotObservation("plot-1", "carrot", true, true, true, 0.5f) });

            var botanist = ResidentSocialCueFactory.Create(residents[1], world);
            var storekeeper = ResidentSocialCueFactory.Create(residents[3], world);

            Assert.That(botanist.ObservationLabel, Is.EqualTo("杂草警报"));
            Assert.That(botanist.Mood, Is.EqualTo(AgentMood.Worried));
            Assert.That(storekeeper.ObservationLabel, Is.EqualTo(botanist.ObservationLabel));
            Assert.That(storekeeper.PersonaAngle, Is.Not.EqualTo(botanist.PersonaAngle));
            Assert.That(storekeeper.Emoji, Is.Not.EqualTo(botanist.Emoji));
            Assert.That(botanist.Emoji, Does.Contain("🌿"));
            Assert.That(storekeeper.Emoji, Does.Contain("📦"));
        }

        [Test]
        public void SocialCue_PrioritizesMatureCropAsProudObservation()
        {
            var resident = TownResidentCatalog.CreateDefaultResidents()[0];
            var world = new WorldObservation(3, 16f, 100,
                new Dictionary<string, int> { { "fertilizer", 0 }, { "carrotseed", 0 } },
                new[] { new PlotObservation("plot-1", "carrot", true, true, false, 1f) });

            var cue = ResidentSocialCueFactory.Create(resident, world);

            Assert.That(cue.ObservationLabel, Is.EqualTo("成熟提醒"));
            Assert.That(cue.Mood, Is.EqualTo(AgentMood.Proud));
            Assert.That(cue.StateSummary, Does.Contain("第3天"));
            Assert.That(cue.Emoji, Does.Contain("🌾"));
        }
    }
}
