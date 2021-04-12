﻿using Akka.Actor;
using Risk.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Risk.Akka.Actors
{
    public class IOActor : ReceiveActor
    {
        private readonly IRiskIOBridge riskIOBridge;
        Dictionary<IActorRef, string> players;
        private ActorSelection gameActor;
        private List<string> names { get; set; }

        public IOActor(IRiskIOBridge riskIOBridge)
        {
            this.riskIOBridge = riskIOBridge;
            names = new();
            gameActor = Context.ActorSelection(ActorNames.Path(ActorNames.Game));
            players = new Dictionary<IActorRef, string>();
            Become(Active);
        }

        public void Active()
        {
            Receive<SignupMessage>(msg =>
            {

                if (players.ContainsValue(msg.ConnectionId))
                {
                    riskIOBridge.JoinFailed(msg.ConnectionId);
                    return;
                }
                var assignedName = AssignName(msg.RequestedName);
                names.Add(assignedName);
                var newPlayer = Context.ActorOf(Props.Create(() => new PlayerActor(assignedName, msg.ConnectionId)), msg.ConnectionId);
                gameActor.Tell(new JoinGameMessage(assignedName, newPlayer));
                players.Add(newPlayer, msg.ConnectionId);
                riskIOBridge.JoinConfirmation(assignedName, msg.ConnectionId);
            });

            Receive<JoinGameResponse>(msg =>
            {
                riskIOBridge.JoinConfirmation(msg.AssignedName, players[Sender]);
            });

            Receive<UnableToJoinMessage>(msg =>
            {
                riskIOBridge.JoinFailed(players[Sender]);
                players.Remove(Sender);
            });

            Receive<DeployMessage>(msg =>
            {
                var deployedPlayer = players.FirstOrDefault(x => x.Value == msg.ConnectionId).Key;
                deployedPlayer.Tell(msg);
            });

            Receive<ConfirmDeployMessage>(msg =>
            {
                //riskIOBridge.
            });

            Receive<StartGameMessage>(msg =>
            {
                gameActor.Tell(new PlayerStartingGameMessage(msg.SecretCode, players.FirstOrDefault(x => x.Value == msg.ConnectionId).Key));
            });

            Receive<GameStartingMessage>(msg =>
            {
                riskIOBridge.GameStarting();
            });
        }

        private string AssignName(string requestedName)
        {
            int sameNames = 2;
            var assignedPlayerName = requestedName;
            while (names.Contains(assignedPlayerName))
            {
                assignedPlayerName = string.Concat(requestedName, sameNames.ToString());
                sameNames++;
            }
            return assignedPlayerName;
        }


    }

}
