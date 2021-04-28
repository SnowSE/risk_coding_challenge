using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Risk.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Risk.Signalr.ConsoleClient
{
    public class DefaultHostedPlayerLogic : HostedPlayerLogic
    {
        private readonly ILogger<HostedPlayerLogic> logger;

        public DefaultHostedPlayerLogic(IConfiguration configuration, IHostApplicationLifetime applicationLifetime, ILogger<HostedPlayerLogic> logger)
            : base(configuration, applicationLifetime, logger)
        {
            this.logger = logger;
        }

        public override Location WhereDoYouWantToDeploy(IEnumerable<BoardTerritory> board)
        {
            logger.LogInformation($"Pondering where to deploy...{board.Count()} choices...");
            
            var myTerritory = board.FirstOrDefault(t => t.OwnerName == MyPlayerName);
            logger.LogInformation($"My territory: {myTerritory?.ToString() ?? "[null]"}");

            var nextFreeTerritory = board.First(t => t.OwnerName == null);
            logger.LogInformation($"Next free territory: {nextFreeTerritory?.ToString() ?? "[null]"}");

            var desiredDeployLocation = myTerritory ?? nextFreeTerritory;
            logger.LogInformation($"I'm thinking of deploying to {desiredDeployLocation.Location}...");
            return desiredDeployLocation.Location;
        }

        public override string MyPlayerName { get; set; } = "Default Player Logic";

        public override (Location from, Location to) WhereDoYouWantToAttack(IEnumerable<BoardTerritory> board)
        {
            foreach (var myTerritory in board.Where(t => t.OwnerName == MyPlayerName).OrderByDescending(t => t.Armies))
            {
                var myNeighbors = GetNeighbors(myTerritory, board);
                var destination = myNeighbors.Where(t => t.OwnerName != MyPlayerName).OrderBy(t => t.Armies).FirstOrDefault();
                if (destination != null)
                {
                    return (myTerritory.Location, destination.Location);
                }
            }
            throw new Exception("Unable to find place to attack");
        }
    }
}
