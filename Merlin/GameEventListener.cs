using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.Customization;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks; 
using System;

namespace Impostor.Plugins.Example.Handlers
{
    /// <summary>
    ///     A class that listens for two events.
    ///     It may be more but this is just an example.
    ///
    ///     Make sure your class implements <see cref="IEventListener"/>.
    /// </summary>


    public class GameEventListener : IEventListener
    {
        private readonly ILogger<ExamplePlugin> _logger;

        static System.Random rnd = new System.Random();

        struct MerlinGame
        {
            public int MerlinClientId;
            public bool MerlinOn;
            public bool Merlinwin;
            public bool GameEnded;
            public bool CountingDown;
            public bool MerlinInGame;
            public string Merlinname;
        }
    
        Dictionary<string, MerlinGame> MerlinGames = new Dictionary<string, MerlinGame>();

        public GameEventListener(ILogger<ExamplePlugin> logger)
        {
            _logger = logger;
        }

        /// <summary>
        ///     An example event listener.
        /// </summary>
        /// <param name="e">
        ///     The event you want to listen for.
        /// </param>

        private async Task ServerSendChatToPlayerAsync(string text, IInnerPlayerControl player)
        {
            string playername = player.PlayerInfo.PlayerName;
            await player.SetNameAsync($"PrivateMsg").ConfigureAwait(false);
            await player.SendChatToPlayerAsync($"{text}", player).ConfigureAwait(false);
            await player.SetNameAsync(playername);
        }

        private async Task ServerSendChatAsync(string text, IInnerPlayerControl player)
        {
            string playername = player.PlayerInfo.PlayerName;
            await player.SetNameAsync($"PublicMsg").ConfigureAwait(false);
            await player.SendChatAsync($"{text}").ConfigureAwait(false);
            await player.SetNameAsync(playername);
        }  

        [EventListener]
        public void OnGameCreated(IGameCreatedEvent e)
        {
            MerlinGame jgame = new MerlinGame();
            jgame.MerlinOn = true;
            jgame.Merlinwin = false;
            jgame.GameEnded = false;
            jgame.CountingDown = false;
            jgame.MerlinInGame = false;
            MerlinGames.Add(e.Game.Code, jgame);
        }

        [EventListener]
        public void OnSetStartCounter(IPlayerSetStartCounterEvent e)
        {
            if (e.SecondsLeft == 5)
            {
                MerlinGame jgame = MerlinGames[e.Game.Code];
                jgame.CountingDown = true;
                MerlinGames[e.Game.Code] = jgame;

                _logger.LogInformation($"Countdown started.");
                if (MerlinGames[e.Game.Code].MerlinOn)
                {
                    Task.Run(async () => await AssignMerlin(e).ConfigureAwait(false));
                    foreach (var player in e.Game.Players)
                    {
                        Task.Run(async () => await MakePlayerLookAtChat(player).ConfigureAwait(false));
                    }
                }
            }
        }      

        private async Task AssignMerlin(IPlayerSetStartCounterEvent e)
        {
            List<IClientPlayer> gameplayers = new List<IClientPlayer>();
            foreach (var player in e.Game.Players)
            {
                gameplayers.Add(player);
            }
            int r = rnd.Next(gameplayers.Count);

            MerlinGame jgame = MerlinGames[e.Game.Code];
            jgame.MerlinClientId = gameplayers[r].Client.Id;
            jgame.Merlinname = gameplayers[r].Character.PlayerInfo.PlayerName;
            MerlinGames[e.Game.Code] = jgame;

            await ServerSendChatToPlayerAsync($"You're the JESTER! (unless you'll be an imposter)", gameplayers[r].Character).ConfigureAwait(false);
            _logger.LogInformation($"- {MerlinGames[e.Game.Code].Merlinname} is probably the merlin.");    
        }

        private async Task MakePlayerLookAtChat(IClientPlayer player)
        {
            await Task.Delay(TimeSpan.FromSeconds(0.5)).ConfigureAwait(false);
            string playername = player.Character.PlayerInfo.PlayerName;
            await player.Character.SetNameAsync($"OPEN CHAT").ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            await player.Character.SetNameAsync(playername).ConfigureAwait(false);
        }

        [EventListener]
        public void OnGamePlayerJoined(IGamePlayerJoinedEvent e)
        {
            if (MerlinGames[e.Game.Code].CountingDown)
            {
                _logger.LogInformation($"Assigning Merlin interrupted. {MerlinGames[e.Game.Code].Merlinname} is NOT the merlin.");
                Task.Run(async () => await SorryNotMerlin(e.Game.GetClientPlayer(MerlinGames[e.Game.Code].MerlinClientId)).ConfigureAwait(false));

                MerlinGame jgame = MerlinGames[e.Game.Code];
                jgame.CountingDown = false;
                MerlinGames[e.Game.Code] = jgame;
            }
        }

        [EventListener]
        public void OnGamePlayerLeft(IGamePlayerLeftEvent e)
        {
            if (MerlinGames[e.Game.Code].CountingDown)
            {
                _logger.LogInformation($"Assigning Merlin interrupted. {MerlinGames[e.Game.Code].Merlinname} is NOT the merlin.");
                Task.Run(async () => await SorryNotMerlin(e.Game.GetClientPlayer(MerlinGames[e.Game.Code].MerlinClientId)).ConfigureAwait(false));

                MerlinGame jgame = MerlinGames[e.Game.Code];
                jgame.CountingDown = false;
                MerlinGames[e.Game.Code] = jgame;
            }
        }

        private async Task SorryNotMerlin(IClientPlayer player)
        {
            await ServerSendChatToPlayerAsync($"Startup interrupted. You're NOT the Merlin.", player.Character).ConfigureAwait(false);
        }        

        [EventListener]
        public void OnGameStarted(IGameStartedEvent e)
        {
            MerlinGame jgame = MerlinGames[e.Game.Code];
            jgame.GameEnded = false;
            jgame.CountingDown = false;
            jgame.MerlinInGame = false;
            jgame.Merlinwin = false;
            MerlinGames[e.Game.Code] = jgame;

            _logger.LogInformation($"Game is starting.");
            if (MerlinGames[e.Game.Code].MerlinOn)
            {
                Task.Run(async () => await InformMerlin(e).ConfigureAwait(false));
            }            
            // This prints out for all players if they are impostor or crewmate.
            foreach (var player in e.Game.Players)
            {
                if (MerlinGames[e.Game.Code].MerlinOn && (player.Character.PlayerInfo.HatId == 27 || player.Character.PlayerInfo.HatId == 84))
                {
                    Task.Run(async () => await OffWithYourHat(player).ConfigureAwait(false));
                }                
                var info = player.Character.PlayerInfo;
                var isImpostor = info.IsImpostor;
                if (isImpostor)
                {
                    _logger.LogInformation($"- {info.PlayerName} is an impostor.");
                }
                else
                {
                    _logger.LogInformation($"- {info.PlayerName} is a crewmate.");
                }
            }
        }

        private async Task InformMerlin(IGameStartedEvent e)
        {
            if (e.Game.GetClientPlayer(MerlinGames[e.Game.Code].MerlinClientId).Character.PlayerInfo.IsImpostor)
            {
                _logger.LogInformation($"- {e.Game.GetClientPlayer(MerlinGames[e.Game.Code].MerlinClientId).Character.PlayerInfo.PlayerName} isn't merlin but impostor.");
                await ServerSendChatToPlayerAsync($"You happen to be IMPOSTER! No Merlin this game.", e.Game.GetClientPlayer(MerlinGames[e.Game.Code].MerlinClientId).Character).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation($"- {e.Game.GetClientPlayer(MerlinGames[e.Game.Code].MerlinClientId).Character.PlayerInfo.PlayerName} is indeed merlin.");
                await ServerSendChatToPlayerAsync($"You're indeed the JESTER!", e.Game.GetClientPlayer(MerlinGames[e.Game.Code].MerlinClientId).Character).ConfigureAwait(false);

                MerlinGame jgame = MerlinGames[e.Game.Code];
                jgame.MerlinInGame = true;
                MerlinGames[e.Game.Code] = jgame;
            }
        }

        private async Task OffWithYourHat(IClientPlayer player)
        {
            await player.Character.SetHatAsync(HatType.NoHat).ConfigureAwait(false);
        }

        [EventListener]
        public void OnPlayerExiled(IPlayerExileEvent e)
        {
            if (MerlinGames[e.Game.Code].MerlinInGame && e.PlayerControl == e.Game.GetClientPlayer(MerlinGames[e.Game.Code].MerlinClientId).Character)
            {                                
                MerlinGame jgame = MerlinGames[e.Game.Code];
                jgame.Merlinwin = true;
                MerlinGames[e.Game.Code] = jgame;

                _logger.LogInformation($"Merlin has won!");
                Task.Run(async () => await TurnTheTables(e).ConfigureAwait(false));
            }
        }

        private async Task TurnTheTables(IPlayerExileEvent e)
        {
            await e.Game.GetClientPlayer(MerlinGames[e.Game.Code].MerlinClientId).Character.SetHatAsync(HatType.ElfHat).ConfigureAwait(false);
            foreach (var player in e.Game.Players)
            {
                if (player.Client.Id != MerlinGames[e.Game.Code].MerlinClientId)
                {
                    await player.Character.SetHatAsync(HatType.DumSticker).ConfigureAwait(false);
                }
            }
            foreach (var player in e.Game.Players)
            {
                if (!player.Character.PlayerInfo.IsDead && player.Character.PlayerInfo.IsImpostor)
                {
                    _logger.LogInformation($"- {player.Character.PlayerInfo.PlayerName} is murdered by plugin.");
                    await player.Character.SetMurderedByAsync(player);                          
                }
            }
                      
        }

        [EventListener]
        public void OnGameEnded(IGameEndedEvent e)
        {
            _logger.LogInformation($"Game has ended.");

            MerlinGame jgame = MerlinGames[e.Game.Code];
            jgame.GameEnded = true;
            MerlinGames[e.Game.Code] = jgame;
        }

        [EventListener]
        public void OnPlayerSpawned(IPlayerSpawnedEvent e)
        {
            if (MerlinGames[e.Game.Code].GameEnded && MerlinGames[e.Game.Code].MerlinInGame)
            {               
                Task.Run(async () => await MerlinAnnouncement(e).ConfigureAwait(false));
            }
        }

        private async Task MerlinAnnouncement(IPlayerSpawnedEvent e)
        {
            if (MerlinGames[e.Game.Code].Merlinwin)
            {
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await ServerSendChatToPlayerAsync($"{MerlinGames[e.Game.Code].Merlinname} was Merlin and won by getting ejected!", e.PlayerControl).ConfigureAwait(false);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await ServerSendChatToPlayerAsync($"{MerlinGames[e.Game.Code].Merlinname} was Merlin, but didn't get voted out.", e.PlayerControl).ConfigureAwait(false);                
            }
        }

        [EventListener]
        public void OnGameDestroyed(IGameDestroyedEvent e)
        {
            MerlinGames.Remove(e.Game.Code);
        }

        [EventListener]
        public void OnPlayerChat(IPlayerChatEvent e)
        {
            _logger.LogInformation($"{e.PlayerControl.PlayerInfo.PlayerName} said {e.Message}");
            if (e.Game.GameState == GameStates.NotStarted && !MerlinGames[e.Game.Code].CountingDown && e.Message.StartsWith("/"))
            {
                Task.Run(async () => await RunCommands(e).ConfigureAwait(false));
            }
        }

        private async Task RunCommands(IPlayerChatEvent e)
        {
            switch (e.Message.ToLowerInvariant())
            {
                case "/j on":
                case "/merlin on":
                    if (e.ClientPlayer.IsHost)
                    {
                        MerlinGame jgame = MerlinGames[e.Game.Code];
                        jgame.MerlinOn = true;
                        MerlinGames[e.Game.Code] = jgame;

                        await ServerSendChatAsync("The Merlin role is now on!", e.PlayerControl).ConfigureAwait(false);
                    }
                    else
                    {
                        await ServerSendChatAsync("You need to be host to change roles.", e.PlayerControl).ConfigureAwait(false);
                    }
                    break;
                case "/j off":
                case "/merlin off":
                    if (e.ClientPlayer.IsHost)
                    {
                        MerlinGame jgame = MerlinGames[e.Game.Code];
                        jgame.MerlinOn = false;
                        MerlinGames[e.Game.Code] = jgame;

                        await ServerSendChatAsync("The Merlin role is now off!", e.PlayerControl).ConfigureAwait(false);
                    }
                    else
                    {
                        await ServerSendChatAsync("You need to be host to change roles.", e.PlayerControl).ConfigureAwait(false);
                    }
                    break;
                case "/j help":
                case "/merlin help":
                    await ServerSendChatAsync("When the special Merlin role is on, one crewmate is Merlin.", e.PlayerControl).ConfigureAwait(false);  
                    await ServerSendChatAsync("In addition to the ways in which a normal crewmate can win, the Merlin can win by getting voted out.", e.PlayerControl).ConfigureAwait(false);
                    await ServerSendChatAsync("If this happens all the other players lose, so be careful who you vote during meetings!", e.PlayerControl).ConfigureAwait(false);
                    await ServerSendChatAsync("The host can turn the Merlin role on and off by typing '/merlin on' or '/merlin off'.", e.PlayerControl).ConfigureAwait(false);
                    break;
                default:
                    await ServerSendChatAsync("Error. Possible commands are '/merlin help', '/merlin on', '/merlin off'.", e.PlayerControl).ConfigureAwait(false);  
                    break;                                     
            }
        }
        
    }
}