using NUnit.Framework;

namespace AIFarmNPC.Agent.Tests
{
    public sealed class NaturalLanguageFarmParserTests
    {
        [Test]
        public void Parse_ChineseNaturalRequest_ExtractsFullCycleCropAndPlot()
        {
            var command = new NaturalLanguageFarmParser().Parse("请在地块A帮我种小麦并照料到收获");

            Assert.That(command.Intent, Is.EqualTo(FarmIntent.FullCycle));
            Assert.That(command.CropId, Is.EqualTo("wheat"));
            Assert.That(command.PlotId, Is.EqualTo("A"));
            Assert.That(command.HasExplicitPlot, Is.True);
            Assert.That(command.Language, Is.EqualTo("zh"));
        }

        [Test]
        public void Parse_EnglishNaturalRequest_ExtractsFullCycleCropAndPlot()
        {
            var command = new NaturalLanguageFarmParser().Parse("Please grow corn on plot 3");

            Assert.That(command.Intent, Is.EqualTo(FarmIntent.FullCycle));
            Assert.That(command.CropId, Is.EqualTo("corn"));
            Assert.That(command.PlotId, Is.EqualTo("plot-3"));
            Assert.That(command.Language, Is.EqualTo("en"));
        }

        [TestCase("给田2浇水", FarmIntent.Water)]
        [TestCase("给田2施肥", FarmIntent.Fertilize)]
        [TestCase("给田2除草", FarmIntent.Weed)]
        [TestCase("收获田2", FarmIntent.Harvest)]
        [TestCase("sow wheat on plot 2", FarmIntent.Sow)]
        public void Parse_SingleStepCommands_ReturnExpectedIntent(string input, FarmIntent expected)
        {
            Assert.That(new NaturalLanguageFarmParser().Parse(input).Intent, Is.EqualTo(expected));
        }

        [Test]
        public void Parse_UnrelatedText_IsUnknown()
        {
            Assert.That(new NaturalLanguageFarmParser().Parse("今天天气不错").IsValid, Is.False);
        }

        [Test]
        public void Parse_RequestWithoutPlot_PreservesFallbackButMarksItAsAutomatic()
        {
            var command = new NaturalLanguageFarmParser().Parse("帮我种胡萝卜并照料到收获", "plot-4");

            Assert.That(command.PlotId, Is.EqualTo("plot-4"));
            Assert.That(command.HasExplicitPlot, Is.False);
        }

        [Test]
        public void Parse_NumericChinesePlot_NormalizesToCorePlotId_AndUnderstandsTurnip()
        {
            var command = new NaturalLanguageFarmParser().Parse("帮我在地块2种芜菁");

            Assert.That(command.PlotId, Is.EqualTo("plot-2"));
            Assert.That(command.CropId, Is.EqualTo("turnip"));
            Assert.That(command.Intent, Is.EqualTo(FarmIntent.FullCycle));
        }
    }
}
