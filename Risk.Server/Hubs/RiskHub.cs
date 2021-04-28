using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Risk.Game;
using Microsoft.Extensions.Configuration;
using Risk.Shared;
using Akka.Actor;
using Risk.Akka;
using static Risk.Shared.ActorNames;

namespace Risk.Server.Hubs
{
    public class RiskHub : Hub<IRiskHub>
    {
        private readonly ILogger<RiskHub> logger;
        private readonly IConfiguration config;
        private readonly ActorSystem actorSystem;
        private readonly ActorSelection IOActor;


        public RiskHub(ILogger<RiskHub> logger, IConfiguration config, ActorSystem actorSystem)
        {
            this.logger = logger;
            this.config = config;
            this.actorSystem = actorSystem;
            IOActor = actorSystem.ActorSelection(Path("akka://Risk", ActorNames.IO));
        }
        public override async Task OnConnectedAsync()
        {
            logger.LogInformation(Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            IOActor.Tell(new UserDisconnectedMessage(Context.ConnectionId));
            return Task.CompletedTask;
        }

        public async Task SendMessage(string user, string message)
        {
            await Clients.Client(user).SendMessage("Server", message);
        }

        public async Task SendStatus(GameStatus status)
        {
            await Clients.All.SendStatus(status);
        }

        public async Task AskUserDeploy(string connectionId, Board board)
        {
            await Clients.Client(connectionId).YourTurnToDeploy(board.SerializableTerritories);
        }

        public async Task Signup(string requestedName)
        {
            await Task.FromResult(false);
            IOActor.Tell(new SignupMessage(requestedName, Context.ConnectionId));
        }

        private async Task BroadCastMessage(string message)
        {
            await Task.FromResult(false);
            await Clients.All.SendMessage("Server", message);
        }

        public async Task AskUserAttack(string connectionId, Board board)
        {
            await Clients.Client(connectionId).YourTurnToAttack(board.SerializableTerritories);
        }

        public Task StartGame(string Password, GameStartOptions startOptions)
        {
            IOActor.Tell(new StartGameMessage(Password, startOptions));
            return Task.CompletedTask;
        }


        public Task DeployRequest(Location l)
        {
            logger.LogInformation("Received DeployRequest from {connectionId}", Context.ConnectionId);

            IOActor.Tell(new BridgeDeployMessage(l, Context.ConnectionId));
            return Task.CompletedTask;
        }

        public Task AttackRequest(Location from, Location to)
        {
            IOActor.Tell(new BridgeAttackMessage(to, from, Context.ConnectionId));
            return Task.FromResult(false);
        }

        

        public Task AttackComplete()
        {
            IOActor.Tell(new BridgeCeaseAttackingMessage(Context.ConnectionId));
            return Task.FromResult(false);
        }

        public async Task SendGameOverAsync(GameStatus gameStatus)
        {
            var winners = gameStatus.PlayerStats.Where(s => s.Score == gameStatus.PlayerStats.Max(s => s.Score)).Select(s => s.Name);
            await Clients.All.SendStatus(gameStatus);
            await BroadCastMessage($"Game Over - {string.Join(',', winners)} win{(winners.Count() > 1 ? "" : "s")}!");
        }

        public async Task JoinFailed(string connectionId)
        {
            await Clients.Client(connectionId).SendMessage("Server", "Unable to join game.");
        }

        public async Task JoinConfirmation(string assignedName, string connectionId)
        {
            await Clients.Client(connectionId).JoinConfirmation(assignedName);
            await BroadCastMessage(assignedName + " has joined the game");
            await Clients.Client(connectionId).SendMessage("Server", "Welcome to the game " + assignedName);
        }

        public async Task ConfirmDeploy(string connectionId)
        {
            await Clients.Client(connectionId).SendMessage("Server", "Successfully Deployed");
        }

        public async Task AnnounceStartGame()
        {
            await BroadCastMessage("Game has started");
        }

        public Task RestartGame(string password, GameStartOptions startOptions)
        {
            IOActor.Tell(new BridgeRestartGameMessage(password, startOptions));
            return Task.CompletedTask;
        }
    }
}
