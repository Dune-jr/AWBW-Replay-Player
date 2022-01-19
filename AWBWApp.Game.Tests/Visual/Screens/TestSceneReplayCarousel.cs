﻿using System;
using System.Collections.Generic;
using System.Globalization;
using AWBWApp.Game.API.Replay;
using AWBWApp.Game.IO;
using AWBWApp.Game.UI.Select;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osuTK;

namespace AWBWApp.Game.Tests.Visual.Screens
{
    [TestFixture]
    public class TestSceneReplayCarousel : AWBWAppTestScene
    {
        private ReplayCarousel carousel;

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(10)]
        [TestCase(100)]
        public void TestPanels(int count)
        {
            loadReplays(count);
        }

        [Test]
        public void TestReplayManager()
        {
            bool changed = false;
            var replayManager = new ReplayManager();
            createCarousel(c =>
            {
                carousel.ReplaysChanged = () => changed = true;
                carousel.Replays = replayManager.GetAllKnownReplays();
            });

            AddUntilStep("Wait for load", () => changed);
        }

        private void loadReplays(int count)
        {
            bool changed = false;
            createCarousel(c =>
            {
                var replayDatas = new List<ReplayInfo>();

                var random = new Random();

                var randomEndDate = new DateTime(2021, 12, 31);
                var randomStartDate = new DateTime(2021, 1, 1);

                var span = randomEndDate - randomStartDate;
                Logger.Log(CultureInfo.CurrentCulture.Name);
                Logger.Log("---");

                for (int i = 0; i < count; i++)
                {
                    var dateA = (randomStartDate + new TimeSpan(random.Next(0, (int)span.TotalDays), 0, 0, 0));
                    var dateB = (randomStartDate + new TimeSpan(random.Next(0, (int)span.TotalDays), 0, 0, 0));

                    var playerCount = random.Next(2, 8);

                    var players = new AWBWReplayPlayer[playerCount];

                    for (int j = 0; j < playerCount; j++)
                    {
                        players[j] = new AWBWReplayPlayer()
                        {
                            ID = j,
                            CountryId = j + 1,
                            UniqueId = Guid.NewGuid().ToString()
                        };
                    }

                    var replayInfo = new ReplayInfo
                    {
                        StartDate = (dateA > dateB ? dateB : dateA).ToString("d"),
                        EndDate = (dateA > dateB ? dateA : dateB).ToString("d"),
                        Name = Guid.NewGuid().ToString(),
                        LeagueMatch = random.Next(2) == 1 ? "Yes" : null,
                        Players = players
                    };

                    replayDatas.Add(replayInfo);
                }
                carousel.ReplaysChanged = () => changed = true;
                carousel.Replays = replayDatas;
            });
            AddUntilStep("Wait for load", () => changed);
        }

        private void createCarousel(Action<ReplayCarousel> carouselAdjust = null)
        {
            AddStep("Create carousel", () =>
            {
                carousel = new ReplayCarousel
                {
                    RelativeSizeAxes = Axes.Both,
                    Size = Vector2.One
                };

                carouselAdjust?.Invoke(carousel);

                Child = carousel;
            });
        }
    }
}
