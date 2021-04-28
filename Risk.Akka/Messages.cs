using Akka.Actor;
using Risk.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Risk.Akka
{
    public record CannotStartGameMessage;
    public record ConfirmPlayerSignup(string AssignedName);
    public record GameStartingMessage;
    public record InvalidSecretCodeMessage;
    public record NotEnoughPlayersToStartGameMessage;
    public record JoinGameMessage(string AssignedName, IActorRef Actor);
    public record JoinGameResponse(string AssignedName);
    public record TellUserJoinedMessage(string AssignedName, string ConnectionId);
    public record NoGameResponse;
    public record SignupMessage(string RequestedName, string ConnectionId);
    public record StartGameMessage(string SecretCode, GameStartOptions StartOptions);
    public record UnableToJoinMessage(string AssignedName, IActorRef Actor);
    public record BridgeDeployMessage(Location To, string ConnectionId);
    public record BridgeAttackMessage(Location Defending, Location Attacking, string ConnectionId);
    public record BridgeCeaseAttackingMessage(string ConnectionId);
    public record CeaseAttackingMessage(IActorRef Player);
    public record DeployMessage(Location To, IActorRef Player);
    public record AttackMessage(Location Defending, Location Attacking, IActorRef Player);
    public record ConfirmDeployMessage();
    public record BadDeployRequest(IActorRef Player);
    public record BadAttackRequest(IActorRef Player);
    public record InvalidPlayerRequestMessage;
    public record TellUserDeployMessage(IActorRef Player, Board Board);
    public record TellUserAttackMessage(IActorRef Player, Board Board);
    public record GameStatusMessage(GameStatus Status);
    public record GameOverMessage(GameStatus gameStatus);
    public record ChatMessage(IActorRef Player, string MessageText);
    public record TooManyInvalidRequestsMessage(IActorRef Player);
    public record BridgeRestartGameMessage(string Password, GameStartOptions StartOptions);
    public record RestartGameMessage(string SecretCode, GameStartOptions StartOptions);
    public record ResetInvalidRequestMessage();
    public record UserDisconnectedMessage(string ConnectionId)
    {
        public IActorRef ActorRef { get; internal set; }
    }
}
