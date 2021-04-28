using Akka.Actor;
using Risk.Shared;
using System;
using System.Collections.Generic;
using System.Text;
using Risk.Game;
using System.Linq;
using Akka.Event;

namespace Risk.Akka.Actors
{
    public class GameActor : ReceiveActor
    {
        public ILoggingAdapter Log { get; } = Context.GetLogger();
        private string secretCode { get; set; }
        private Risk.Game.Game game { get; set; }
        public GameActor(string secretCode)
        {
            this.secretCode = secretCode;
            game = new Game.Game();
            Become(Starting);
        }

        public void Starting()
        {
            Receive<JoinGameMessage>(msg =>
            {
                game.Players.Add(msg.Actor);
                game.AssignedNames.Add(msg.Actor, msg.AssignedName);
            });

            Receive((Action<StartGameMessage>)(msg =>
            {
                StartOrRestartGame(msg.SecretCode, msg.StartOptions, Sender);
            }));

            Receive<StartGameMessage>(msg =>
            {
                StartOrRestartGame(msg.SecretCode, msg.StartOptions, Sender);
            });

            Receive<RestartGameMessage>(msg =>
            {
                StartOrRestartGame(msg.SecretCode, msg.StartOptions, Sender);
            });

            Receive<TooManyInvalidRequestsMessage>(msg => {
                game.RemovePlayerFromGame(msg.Player);
                Context.ActorSelection(ActorNames.Path(Self.Path.Root.ToString(), ActorNames.IO)).Tell(new TooManyInvalidRequestsMessage(msg.Player));
            });

            Receive<UserDisconnectedMessage>(msg =>
            {
                Log.Info($"Removing player {msg.ActorRef.Path.Name} from game...they disconnected from the server.");
                game.RemovePlayerFromGame(msg.ActorRef);
            });
        }

        private void StartOrRestartGame(string secretCode, GameStartOptions startOptions, IActorRef Sender)
        {
            if (this.secretCode != secretCode)
            {
                Sender.Tell(new InvalidSecretCodeMessage());
                return;
            }

            if(game.Players.Count == 0)
            {
                Sender.Tell(new NotEnoughPlayersToStartGameMessage());
                return;
            }

            Become(Deploying);
            game.InitializeGame(startOptions);
            game.StartGame();
            Sender.Tell(new GameStartingMessage());
            yourTurnToDeploy(game.CurrentPlayer);            
        }

        public void Deploying()
        {
            Receive<JoinGameMessage>(msg =>
            {
                Sender.Tell(new UnableToJoinMessage(msg.AssignedName, msg.Actor));
            });

            Receive((Action<DeployMessage>)(msg =>
            {
                if (!game.Players.Contains(msg.Player))
                    return;//ignore messages from actors not in the game.

                Log.Info($"{game.AssignedNames[msg.Player]} wants to deploy to {msg.To}");
                
                if (isCurrentPlayer(msg.Player) && game.TryPlaceArmy(msg.Player, msg.To))
                {
                    game.LastAction = new GameAction { Type = ActionType.Deploy, Location = msg.To };
                    Sender.Tell(new ConfirmDeployMessage());
                    Log.Info($"{msg.Player} successfully deployed to {msg.To}");
                    var nextPlayer = game.NextPlayer();
                    if(game.GameState == GameState.Deploying)
                    {
                        yourTurnToDeploy(nextPlayer);
                    }
                    else
                    {
                        Become(Attacking);
                        yourTurnToAttack(nextPlayer);
                    }
                }
                else
                {
                    msg.Player.Tell(new InvalidPlayerRequestMessage());
                    Sender.Tell(new BadDeployRequest(msg.Player));
                    Log.Info($"{msg.Player} failed to deploy to {msg.To}");
                    yourTurnToDeploy(game.NextPlayer());
                }
                Sender.Tell(new GameStatusMessage(game.GetGameStatus()));
            }));

            Receive<TooManyInvalidRequestsMessage>(msg => {
                Log.Info($"Removing {msg.Player.Path.Name} from game.  Too many bad requests.");
                game.RemovePlayerFromGame(msg.Player);
                Context.ActorSelection(ActorNames.Path(Self.Path.Root.ToString(), ActorNames.IO)).Tell(new TooManyInvalidRequestsMessage(msg.Player));
                game.NextPlayer();
            });

            Receive<UserDisconnectedMessage>(msg =>
            {
                Log.Info($"Removing player {msg.ActorRef.Path.Name} from game...they disconnected from the server.");
                game.RemovePlayerFromGame(msg.ActorRef);
            });
        }

        private void yourTurnToDeploy(IActorRef nextPlayer)
        {
            Log.Info($"Asking {game.AssignedNames[nextPlayer]} where they want to deploy");
            Sender.Tell(new TellUserDeployMessage(nextPlayer, game.Board));
        }

        private void yourTurnToAttack(IActorRef nextPlayer)
        {
            Log.Info($"Asking {game.AssignedNames[nextPlayer]} where they want to attack");
            Sender.Tell(new TellUserAttackMessage(nextPlayer, game.Board));
        }

        public void Attacking()
        {
            Receive<CeaseAttackingMessage>(msg =>
            {
                if (isCurrentPlayer(msg.Player))
                {
                    if(game.Players.Count <= 1 || game.Players.Any(p => game.PlayerCanAttack(p)) is false)
                    {
                        game.SetGameOver();
                        Log.Info("Ending Game. Player count = " + game.Players.Count + ";");
                        Sender.Tell(new GameOverMessage(game.GetGameStatus()));
                        Become(GameOver);
                        return;
                    }

                    Log.Info($"{game.AssignedNames[msg.Player]} ceases attacking.");
                    yourTurnToAttack(game.NextPlayer());
                }
                else
                {
                    Log.Info($"{game.AssignedNames[msg.Player]} wants to cease attacking...but it's not their turn.");
                    msg.Player.Tell(new InvalidPlayerRequestMessage());
                    Sender.Tell(new BadAttackRequest(msg.Player));
                }
            });

            Receive<AttackMessage>(msg =>
            {
                if (isCurrentPlayer(msg.Player))
                {
                    if(game.Players.Count <= 1 || game.Players.Any(p => game.PlayerCanAttack(p)) is false)
                    {
                        game.SetGameOver();
                        Log.Info("Ending Game. Player count = " + game.Players.Count + ";");
                        Sender.Tell(new GameOverMessage(game.GetGameStatus()));
                        Become(GameOver);
                        return;
                    }

                    if (game.PlayerCanAttack(msg.Player))
                    {
                        TryAttackResult attackResult = new TryAttackResult { AttackInvalid = false };
                        Territory attackingTerritory = null;
                        Territory defendingTerritory = null;
                        try
                        {
                            attackingTerritory = game.Board.GetTerritory(msg.Attacking);
                            defendingTerritory = game.Board.GetTerritory(msg.Defending);

                            Log.Info($"{game.AssignedNames[msg.Player]} wants to attack from {attackingTerritory} to {defendingTerritory}");

                            attackResult = game.TryAttack(msg.Player, attackingTerritory, defendingTerritory);
                            game.LastAction = new GameAction { Type = ActionType.Attack, Location = msg.Attacking, Destination = msg.Defending };
                            Sender.Tell(new GameStatusMessage(game.GetGameStatus()));
                        }
                        catch (Exception ex)
                        {
                            attackResult = new TryAttackResult { AttackInvalid = true, Message = ex.Message };
                        }
                        if (attackResult.AttackInvalid)
                        {
                            game.LastAction = null;
                            msg.Player.Tell(new InvalidPlayerRequestMessage());
                            Log.Error($"Invalid attack request! {msg.Player} from {attackingTerritory} to {defendingTerritory}.");
                            Sender.Tell(new ChatMessage(msg.Player, $"Invalid attack request: {attackResult.Message} :("));
                            yourTurnToAttack(msg.Player);
                        }
                        else
                        {
                            Sender.Tell(new ChatMessage(msg.Player, $"Successfully Attacked From ({msg.Attacking.Row}, {msg.Attacking.Column}) To ({msg.Defending.Row}, {msg.Defending.Column})"));
                            if (game.GameState == GameState.Attacking)
                            {
                                if (game.PlayerCanAttack(msg.Player))
                                {
                                    yourTurnToAttack(msg.Player);
                                }
                                else
                                    yourTurnToAttack(game.NextPlayer());
                            }
                            else
                            {
                                game.SetGameOver();
                                Sender.Tell(new GameOverMessage(game.GetGameStatus()));
                                Become(GameOver);
                            }
                        }
                    }
                    else
                    {
                        Log.Error($"Player {game.AssignedNames[msg.Player]} tried to attack when they couldn't attack");
                        msg.Player.Tell(new InvalidPlayerRequestMessage());
                        yourTurnToAttack(game.NextPlayer());
                    }
                }
                else
                {
                    msg.Player.Tell(new InvalidPlayerRequestMessage());
                    Sender.Tell(new BadAttackRequest(msg.Player));
                }
            });

            Receive<TooManyInvalidRequestsMessage>(msg => {
                Log.Error($"Player {game.AssignedNames[msg.Player]} has too many invalid requests.  Booting from game.");
                game.RemovePlayerFromGame(msg.Player);
                Context.ActorSelection(ActorNames.Path(Self.Path.Root.ToString(), ActorNames.IO)).Tell(new TooManyInvalidRequestsMessage(msg.Player));
            });

            Receive<UserDisconnectedMessage>(msg =>
            {
                Log.Info($"Removing player {msg.ActorRef.Path.Name} from game...they disconnected from the server.");
                game.RemovePlayerFromGame(msg.ActorRef);
            });
        }

        public void GameOver()
        {
            Receive<RestartGameMessage>(msg =>
            {
                if(msg.SecretCode == secretCode)
                {
                    Become(Starting);
                    Context.Self.Tell(new StartGameMessage(msg.SecretCode, msg.StartOptions), Context.Sender);
                }
            });

            Receive<JoinGameMessage>(msg =>
            {
                game.Players.Add(msg.Actor);
                game.AssignedNames.Add(msg.Actor, msg.AssignedName);
            });

            Receive<UserDisconnectedMessage>(msg =>
            {
                Log.Info($"Removing player {msg.ActorRef.Path.Name} from game...they disconnected from the server.");
                game.RemovePlayerFromGame(msg.ActorRef);
            });
        }

        private bool isCurrentPlayer(IActorRef CurrentPlayer) => game.CurrentPlayer == CurrentPlayer;


        


    }
}
