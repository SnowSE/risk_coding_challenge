using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Risk.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Risk.Signalr.ConsoleClient
{
    public abstract class HostedPlayerLogic : IHostedService, IPlayerLogic
    {
        private const string DefaultServerAddress = "http://localhost:5000";
        static HubConnection hubConnection;
        private readonly IConfiguration config;
        private readonly IHostApplicationLifetime applicationLifetime;
        private readonly ILogger<HostedPlayerLogic> logger;

        public HostedPlayerLogic(IConfiguration configuration, IHostApplicationLifetime applicationLifetime, ILogger<HostedPlayerLogic> logger)
        {
            this.config = configuration;
            this.applicationLifetime = applicationLifetime;
            this.logger = logger;
        }

        public abstract string MyPlayerName { get; set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var serverAddress = config["serverAddress"] ?? DefaultServerAddress;
            logger.LogInformation($"Talking to the server at {serverAddress}");

            hubConnection = new HubConnectionBuilder()
                .WithUrl($"{serverAddress}/riskhub")
                .Build();

            hubConnection.On<string, string>(MessageTypes.SendMessage, (from, message) => Console.WriteLine("From {0}: {1}", from, message));

            hubConnection.On<string>(MessageTypes.JoinConfirmation, validatedName =>
            {
                Console.Title = validatedName;
                MyPlayerName = validatedName;
                logger.LogInformation($"Successfully joined server. Assigned Name is {validatedName}");
            });

            hubConnection.On<IEnumerable<BoardTerritory>>(MessageTypes.YourTurnToDeploy, async (board) =>
            {
                try
                {
                    logger.LogInformation("My turn to deploy!");
                    var deployLocation = WhereDoYouWantToDeploy(board);
                    logger.LogInformation("Deploying to {0}", deployLocation);
                    await hubConnection.SendAsync(MessageTypes.DeployRequest, deployLocation);
                }
                catch(Exception ex)
                {
                    logger.LogError(ex, "Problem determining where to deploy!");
                }
            });

            hubConnection.On<IEnumerable<BoardTerritory>>(MessageTypes.YourTurnToAttack, async (board) =>
            {
                logger.LogInformation("My turn to attack!");
                try
                {
                    (var from, var to) = WhereDoYouWantToAttack(board);
                    logger.LogInformation("Attacking from {0} ({1}) to {2} ({3})", from, board.First(c => c.Location == from).OwnerName, to, board.First(c => c.Location == to).OwnerName);
                    await hubConnection.SendAsync(MessageTypes.AttackRequest, from, to);
                }
                catch
                {
                    logger.LogInformation("Yielding turn (nowhere left to attack)");
                    await hubConnection.SendAsync(MessageTypes.AttackComplete);
                }
            });

            hubConnection.Closed += (ex) =>
            {
                logger.LogInformation("Connection terminated.  Closing program.");
                applicationLifetime.StopApplication();
                return Task.CompletedTask;
            };

            await hubConnection.StartAsync();

            logger.LogInformation("My connection id is {0}.  Waiting for game to start...", hubConnection.ConnectionId);
            await hubConnection.SendAsync(MessageTypes.Signup, MyPlayerName);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await hubConnection.StopAsync();
        }
        
        public abstract (Location from, Location to) WhereDoYouWantToAttack(IEnumerable<BoardTerritory> board);
        public abstract Location WhereDoYouWantToDeploy(IEnumerable<BoardTerritory> board);

        protected IEnumerable<BoardTerritory> GetNeighbors(BoardTerritory territory, IEnumerable<BoardTerritory> board)
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
