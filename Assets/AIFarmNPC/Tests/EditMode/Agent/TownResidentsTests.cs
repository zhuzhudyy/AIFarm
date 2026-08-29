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
    }
}
