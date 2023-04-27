using Akka.Actor;
using Akka.TestKit.NUnit3;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using Risk.Akka.Actors;
using Risk.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Risk.Akka.Test
{
    class AkkaActorTests : TestKit
    {
        [SetUp]
        public void Setup()
        {
            startOptions = new GameStartOptions() { Height = 3, StartingArmiesPerPlayer = 3, Width = 3 };
        }
        GameStartOptions startOptions;

        [Test]
        public void BadPasswordAuthentication()
        {
            //Arrange
            var gameActor = Sys.ActorOf(Props.Create(() => new GameActor("SecretCode")), ActorNames.Game);

            //Act
            gameActor.Tell(new StartGameMessage("BogusSecretCode", startOptions));

            //Assert
            AwaitAssert(() =>
            {
                Assert.NotNull(ExpectMsg<InvalidSecretCodeMessage>());
            });
        }

        [Test]
        public void SuccessfulPasswordAuthentication()
        {
            //Arrange
            var gameActor = Sys.ActorOf(Props.Create(() => new GameActor("SecretCode")), ActorNames.Game);

            //Act
            gameActor.Tell(new StartGameMessage("SecretCode", startOptions));

            //Assert
            AwaitAssert(() =>
            {
                //by this point the secret code has been validated...we just don't have players.
                Assert.NotNull(ExpectMsg<NotEnoughPlayersToStartGameMessage>());
            });
        }

        [Test]
        public void UniqueConnectionId()
        {
            //Arrange
            var gameActor = Sys.ActorOf(Props.Create(() => new GameActor("banana55")));

            var signups = new List<(string assignedName, string connectionId)>();
            var mockBridge = new Mock<IRiskIOBridge>();
            mockBridge.Setup(m => m.JoinConfirmation(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((assignedName, connectionId) =>
                {
                    signups.Add((assignedName, connectionId));
                });
            mockBridge.Setup(m => m.JoinFailed(It.IsAny<string>()))
                .Callback<string>((connectionId) =>
                {
                    //Assert
                    if (signups.Count == 1)
                        Assert.Pass("You cannot sign up with the same connection ID twice.");
                    else
                        Assert.Fail("There isn't a successful signup.");
                });

            var ioActor = Sys.ActorOf(Props.Create(() => new IOActor(mockBridge.Object)));

            //Act
            ioActor.Tell(new SignupMessage("Test", "12345"));
            ioActor.Tell(new SignupMessage("Test", "12345"));
        }

        [Test]
        public void UniquePlayerName()
        {
            //Arrange
            var gameActor = Sys.ActorOf(Props.Create(() => new GameActor("banana55")));

            var signups = new List<(string assignedName, string connectionId)>();
            var mockBridge = new Mock<IRiskIOBridge>();
            mockBridge.Setup(m => m.JoinConfirmation(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((assignedName, connectionId) =>
                {
                    signups.Add((assignedName, connectionId));
                });

            var ioActor = Sys.ActorOf(Props.Create(() => new IOActor(mockBridge.Object)));

            //Act
            ioActor.Tell(new SignupMessage("Bogus", "12345"));
            ioActor.Tell(new SignupMessage("Bogus", "54321"));

            AwaitAssert(() =>
            {
                signups.Count.Should().Be(2);
                signups.First().assignedName.Should().Be("Bogus");
                signups.Skip(1).First().assignedName.Should().Be("Bogus2");
            });
        }

        [Test]
        public void StartGame()
        {
            //arrange
            var gameActor = Sys.ActorOf(Props.Create(() => new GameActor("banana55")), ActorNames.Game);
            var mockBridge = new Mock<IRiskIOBridge>();
            string connectionIdOfFirstPlayer = null;
            mockBridge.Setup(m => m.AskUserDeploy(It.IsAny<string>(), It.IsAny<Board>()))
                .Callback<string, Board>((connectionId, board) =>
                {
                    connectionIdOfFirstPlayer = connectionId;
                });
            var ioActor = Sys.ActorOf(Props.Create(() => new IOActor(mockBridge.Object)), ActorNames.IO);

            ioActor.Tell(new SignupMessage("Bogus", "12345"));
            ioActor.Tell(new SignupMessage("Bogus", "54321"));

            //act
            ioActor.Tell(new StartGameMessage("banana55", new GameStartOptions { ArmiesDeployedPerTurn = 5, Height = 5, Width = 5, StartingArmiesPerPlayer = 10 }));

            //Assert
            AwaitAssert(() =>
            {
                connectionIdOfFirstPlayer.Should().BeOneOf("12345", "54321");
            }, duration: TimeSpan.FromSeconds(1));
        }

        [Test]
        public void SampleGameLifecycle()
        {
            //arrange
            var gameActor = Sys.ActorOf(Props.Create(() => new GameActor("banana55")), ActorNames.Game);
            var mockBridge = new Mock<IRiskIOBridge>();
            var ioActor = Sys.ActorOf(Props.Create(() => new IOActor(mockBridge.Object)), ActorNames.IO);
            Board mostRecentBoard = null;
            GameStatus mostRecentStatus = null;
            mockBridge.Setup(m => m.AskUserDeploy(It.IsAny<string>(), It.IsAny<Board>()))
                .Callback<string, Board>(async (connectionId, board) =>
                {
                    mostRecentBoard = board;
                    var To = board.Territories.First(t => t.Owner == null).Location;
                    TestContext.WriteLine($"{connectionId} deploying to {To}");
                    ioActor.Tell(new BridgeDeployMessage(To, connectionId));
                });
            mockBridge.Setup(m => m.AskUserAttack(It.IsAny<string>(), It.IsAny<Board>()))
                .Callback<string, Board>((connectionId, board) =>
                {
                    var myPlayerName = $"Bogus{connectionId}";
                    foreach (var myTerritory in board.SerializableTerritories
                                                     .Where(t => t.OwnerName == myPlayerName)
                                                     .OrderByDescending(t => t.Armies))
                    {
                        var myNeighbors = GetNeighbors(myTerritory, board.SerializableTerritories);
                        var destination = myNeighbors.Where(t => t.OwnerName != myPlayerName).OrderBy(t => t.Armies).FirstOrDefault();
                        if (destination != null)
                        {
                            TestContext.WriteLine($"{connectionId} attacking from {myTerritory.Location} to {destination.Location}");
                            ioActor.Tell(new BridgeAttackMessage(myTerritory.Location, destination.Location, connectionId));
                            return;
                        }
                    }
                    ioActor.Tell(new BridgeCeaseAttackingMessage(connectionId));
                });
            mockBridge.Setup(m => m.SendGameStatus(It.IsAny<GameStatus>()))
                .Callback<GameStatus>(status =>
                {
                    mostRecentStatus = status;
                });

            ioActor.Tell(new SignupMessage("Bogus1", "1"));
            ioActor.Tell(new SignupMessage("Bogus2", "2"));
            ioActor.Tell(new SignupMessage("Bogus3", "3"));

            //act
            ioActor.Tell(new StartGameMessage("banana55", new GameStartOptions
            {
                Height = 5,
                Width = 5,
                ArmiesDeployedPerTurn = 10,
                StartingArmiesPerPlayer = 5
            }));

            if (Debugger.IsAttached)
            {
                Thread.Sleep(10000);
            }

            //Assert
            AwaitAssert(() =>
            {
                mostRecentStatus.PlayerStats.Count.Should().Be(3);
                mostRecentStatus.GameState.Should().Be(GameState.GameOver);
            }, duration: TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));
        }

        protected static IEnumerable<BoardTerritory> GetNeighbors(BoardTerritory territory, IEnumerable<BoardTerritory> board)
        {
            var l = territory.Location;
            var neighborLocations = new[] {
                new Location(l.Row+1, l.Column-1),
                new Location(l.Row+1, l.Column),
                new Location(l.Row+1, l.Column+1),
                new Location(l.Row, l.Column-1),
                new Location(l.Row, l.Column+1),
                new Location(l.Row-1, l.Column-1),
                new Location(l.Row-1, l.Column),
                new Location(l.Row-1, l.Column+1),
            };
            return board.Where(t => neighborLocations.Contains(t.Location));
        }
    }
}
