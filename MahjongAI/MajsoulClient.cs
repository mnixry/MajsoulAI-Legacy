﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;
using System.Threading;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using MahjongAI.Models;

using Grpc.Core;
using pb = global::Google.Protobuf;
using Newtonsoft.Json;
using Google.Protobuf;

namespace MahjongAI
{
    class MajsoulClient : PlatformClient
    {
        private const string replaysFileName = "replays.txt";

        private string username;
        private string password;
        private pb.Collections.RepeatedField<Lq.OptionalOperation> operationList;
        private bool nextReach = false;
        private bool gameEnded = false;
        private Tile lastDiscardedTile;
        private int accountId = 0;
        private int playerSeat = 0;
        private bool continued = false;
        private bool syncing = false;
        private Queue<MajsoulMessage> pendingActions = new Queue<MajsoulMessage>();
        private bool inPrivateRoom = false;
        private bool continuedBetweenGames = false;
        private Stopwatch stopwatch = new Stopwatch();
        private Random random = new Random();
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();


        private Metadata md = null;
        private Channel channel = null;
        private Lq.Lobby.LobbyClient lobby = null;
        private Lq.FastTest.FastTestClient fast = null;
        private Lq.Notify.NotifyClient notify = null;
        private AsyncServerStreamingCall<Lq.ServerStream> call = null;

        private string connect_token = "";
        private string game_uuid = "";
        private string location = "";
        private bool disconnect = false;
        private uint level = 0;

        public void InitGrpc()
        {
            md = new Metadata { { "access_token", GetDeviceUUID() } };
            channel = new Channel(config.AuthServer, ChannelCredentials.Insecure);
            lobby = new Lq.Lobby.LobbyClient(channel);
        }

        public void HandleSyncGameMessage(string name, ByteString data)
        {
            IMessage msg = null;

            switch (name)
            {
                case "NotifyRoomGameStart":
                    msg = Lq.NotifyRoomGameStart.Parser.ParseFrom(data);
                    break;
                case "NotifyMatchGameStart":
                    msg = Lq.NotifyMatchGameStart.Parser.ParseFrom(data);
                    break;
                case "NotifyGameClientConnect":
                    msg = Lq.NotifyGameClientConnect.Parser.ParseFrom(data);
                    break;
                case "NotifyGameEndResult":
                    msg = Lq.NotifyGameEndResult.Parser.ParseFrom(data);
                    break;
                case "ActionNewRound":
                    msg = Lq.ActionNewRound.Parser.ParseFrom(data);
                    break;
                case "ActionDealTile":
                    msg = Lq.ActionDealTile.Parser.ParseFrom(data);
                    break;
                case "ActionDiscardTile":
                    msg = Lq.ActionDiscardTile.Parser.ParseFrom(data);
                    break;
                case "ActionChangeTile":
                    msg = Lq.ActionChangeTile.Parser.ParseFrom(data);
                    break;
                case "ActionNoTile":
                    msg = Lq.ActionNoTile.Parser.ParseFrom(data);
                    break;
                case "ActionHuleXueZhanEnd":
                    msg = Lq.ActionHuleXueZhanEnd.Parser.ParseFrom(data);
                    break;
                case "ActionHule":
                    msg = Lq.ActionHule.Parser.ParseFrom(data);
                    break;
                case "NotifyEndGameVote":
                    msg = Lq.NotifyEndGameVote.Parser.ParseFrom(data);
                    break;
                case "ActionLiuJu":
                    msg = Lq.ActionLiuJu.Parser.ParseFrom(data);
                    break;
                case "ActionChiPengGang":
                    msg = Lq.ActionChiPengGang.Parser.ParseFrom(data);
                    break;
                case "ActionAnGangAddGang":
                    msg = Lq.ActionAnGangAddGang.Parser.ParseFrom(data);
                    break;
                case "ActionMJStart":
                    msg = Lq.ActionMJStart.Parser.ParseFrom(data);
                    break;
                case "NotifyPlayerLoadGameReady":
                    msg = Lq.NotifyPlayerLoadGameReady.Parser.ParseFrom(data);
                    break;
            }

            HandleMessage(new MajsoulMessage
            {
                Success = true,
                Type = MajsoulMessageType.RESPONSE,
                MethodName = name,
                Message = msg
            });
        }

        public Task CreateNotify()
        {
            new Thread(async () =>
            {
                try {
                    while (await call.ResponseStream.MoveNext())
                    {
                        var data = Lq.ServerStream.Parser.ParseFrom(call.ResponseStream.Current.ToByteArray());
                        var w = Lq.Wrapper.Parser.ParseFrom(data.Stream);

                        IMessage msg = null;

                        switch (w.Name)
                        {
                            case "NotifyLicence":
                                msg = Lq.NotifyLicence.Parser.ParseFrom(w.Data);
                                break;
                            case "NotifyGameFinishReward":
                                msg = Lq.NotifyGameFinishReward.Parser.ParseFrom(w.Data);
                                break;
                            case "NotifyRoomGameStart":
                                msg = Lq.NotifyRoomGameStart.Parser.ParseFrom(w.Data);
                                break;
                            case "NotifyMatchGameStart":
                                msg = Lq.NotifyMatchGameStart.Parser.ParseFrom(w.Data);
                                break;
                            case "NotifyGameClientConnect":
                                msg = Lq.NotifyGameClientConnect.Parser.ParseFrom(w.Data);
                                break;
                            case "NotifyDisconnect":
                                msg = Lq.NotifyDisconnect.Parser.ParseFrom(w.Data);
                                break;
                            case "NotifyGameEndResult":
                                msg = Lq.NotifyGameEndResult.Parser.ParseFrom(w.Data);
                                break;
                            case "NotifyAccountUpdate":
                                msg = Lq.NotifyAccountUpdate.Parser.ParseFrom(w.Data);
                                break;
                            case "ActionNewRound":
                                msg = Lq.ActionNewRound.Parser.ParseFrom(w.Data);
                                break;
                            case "ActionDealTile":
                                msg = Lq.ActionDealTile.Parser.ParseFrom(w.Data);
                                break;
                            case "ActionDiscardTile":
                                msg = Lq.ActionDiscardTile.Parser.ParseFrom(w.Data);
                                break;
                            case "ActionChangeTile":
                                msg = Lq.ActionChangeTile.Parser.ParseFrom(w.Data);
                                break;
                            case "ActionNoTile":
                                msg = Lq.ActionNoTile.Parser.ParseFrom(w.Data);
                                break;
                            case "ActionHuleXueZhanEnd":
                                msg = Lq.ActionHuleXueZhanEnd.Parser.ParseFrom(w.Data);
                                break;
                            case "ActionHule":
                                msg = Lq.ActionHule.Parser.ParseFrom(w.Data);
                                break;
                            case "NotifyEndGameVote":
                                msg = Lq.NotifyEndGameVote.Parser.ParseFrom(w.Data);
                                break;
                            case "ActionLiuJu":
                                msg = Lq.ActionLiuJu.Parser.ParseFrom(w.Data);
                                break;
                            case "ActionChiPengGang":
                                msg = Lq.ActionChiPengGang.Parser.ParseFrom(w.Data);
                                break;
                            case "ActionAnGangAddGang":
                                msg = Lq.ActionAnGangAddGang.Parser.ParseFrom(w.Data);
                                break;
                            case "ActionMJStart":
                                msg = Lq.ActionMJStart.Parser.ParseFrom(w.Data);
                                break;
                            case "NotifyPlayerLoadGameReady":
                                msg = Lq.NotifyPlayerLoadGameReady.Parser.ParseFrom(w.Data);
                                break;
                        }

                        HandleMessage(new MajsoulMessage
                        {
                            Success = true,
                            Type = MajsoulMessageType.RESPONSE,
                            MethodName = w.Name,
                            Message = msg
                        });
                    }
                } catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }).Start();
            return Task.CompletedTask;
        }

        public MajsoulClient(Models.Config config) : base(config)
        {
            username = config.MajsoulUsername;
            password = config.MajsoulPassword;
            InitGrpc();
        }

        public override void Close(bool unexpected = false)
        {
            try
            {
                var res = lobby.softLogout(new Lq.ReqLogout { }, md);
                Environment.Exit(0);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Login()
        {
            if (lobby == null)
            {
                throw new Exception("can't get lobby client");
            }
            try
            {
                expectMessage("Login", timeout: 60000, timeoutMessage: "Login timed out.");

                IMessage res = null;

                if (config.AccessToken == "")
                {
                    res = lobby.login(new Lq.ReqLogin
                    {
                        CurrencyPlatforms = new pb.Collections.RepeatedField<uint> { 2, 6, 8, 10, 11 },
                        Account = username,
                        Password = EncodePassword(password),
                        Reconnect = false,
                        RandomKey = GetDeviceUUID(),
                        GenAccessToken = true,
                        Type = 0
                    }, md);
                    config.AccessToken = ((Lq.ResLogin)res).AccessToken;
                    File.WriteAllText("config.json", JsonConvert.SerializeObject(config));
                }
                else
                {
                    try
                    {
                        var vres = lobby.oauth2Check(new Lq.ReqOauth2Check
                        {
                            AccessToken = config.AccessToken,
                            Type = 0
                        }, md);
                        if (!vres.HasAccount) {
                            config.AccessToken = "";
                            File.WriteAllText("config.json", JsonConvert.SerializeObject(config));
                            Console.WriteLine("Please Restart Application...");
                            Console.WriteLine("Press any key to exit...");
                            Console.ReadKey();
                            Environment.Exit(0);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        Console.WriteLine("Please Restart Application...");
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }

                    res = lobby.oauth2Login(new Lq.ReqOauth2Login
                    {
                        AccessToken = config.AccessToken,
                        Reconnect = false,
                        RandomKey = GetDeviceUUID(),
                        GenAccessToken = true,
                        Type = 0
                    }, md);
                }

                connected = true;

                var msg = new MajsoulMessage
                {
                    Success = true,
                    Type = MajsoulMessageType.RESPONSE,
                    MethodName = "Login",
                    Message = res
                };

                HandleMessage(msg);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public override void Join(GameType type)
        {
            if (lobby == null)
            {
                throw new Exception("can't get lobby client");
            }
            if (!inPrivateRoom && config.PrivateRoom == 0)
            {
                int typeNum = 2;

                if (type.HasFlag(GameType.Match_EastSouth))
                {
                    typeNum += 1;
                }

                if (type.HasFlag(GameType.Level_Throne))
                {
                    typeNum += 12;
                }
                else if (type.HasFlag(GameType.Level_Jade))
                {
                    typeNum += 9;
                }
                else if (type.HasFlag(GameType.Level_Gold))
                {
                    typeNum += 6;
                }
                else if (type.HasFlag(GameType.Level_Silver))
                {
                    typeNum += 3;
                }

                if (config.AutoLevel && level == 0)
                {
                    try
                    {
                        var res = lobby.fetchAccountInfo(new Lq.ReqAccountInfo { AccountId = (uint)accountId }, md);
                        level = res.Account.Level.Id;
                        Console.WriteLine("Get Level: {0}", level);
                    } catch
                    {
                        Console.WriteLine("Get Level Fail, Use Config Level...");
                    }
                }

                if (config.AutoLevel && level > 0)
                {
                    typeNum = 2;
                    if (type.HasFlag(GameType.Match_EastSouth))
                    {
                        typeNum += 1;
                    }

                    if (level == 10101 || level == 10102 || level == 10103)
                    {
                        
                    } else if (level == 10201 || level == 10202 || level == 10203)
                    {
                        typeNum += 3;
                    } else if (level == 10301 || level == 10302 || level == 10303)
                    {
                        typeNum += 6;
                    } else if (level == 10401 || level == 10402 || level == 10403)
                    {
                        typeNum += 9;
                    } else if (level == 10501 || level == 10502 || level == 10503)
                    {
                        typeNum += 12;
                    } else
                    {
                        Console.WriteLine("top level please play by yourself");
                        Close();
                    }
                }

                try
                {
                    expectMessage("MatchGame", timeout: 60000, timeoutMessage: "Game matching timed out.");
                    var res = lobby.matchGame(new Lq.ReqJoinMatchQueue
                    {
                        MatchMode = (uint)typeNum
                    }, md);
                    Console.WriteLine("Match Game");
                    HandleMessage(new MajsoulMessage
                    {
                        Success = true,
                        Type = MajsoulMessageType.RESPONSE,
                        MethodName = "MatchGame",
                        Message = res
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            else
            {
                try
                {
                    Thread.Sleep(1000);
                    var res = lobby.readyPlay(new Lq.ReqRoomReady { Ready = true }, md);
                    Console.WriteLine("Ready Play");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        public override void EnterPrivateRoom(int roomNumber)
        {
            if (roomNumber != 0)
            {
                try
                {
                    var res = lobby.joinRoom(new Lq.ReqJoinRoom
                    {
                        RoomId = (uint)roomNumber
                    }, md);
                    Console.WriteLine("Join Room");
                    inPrivateRoom = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    inPrivateRoom = false;
                }
            }
            else
            {
                inPrivateRoom = false;
            }
        }

        public override void NextReady()
        {
            try
            {
                var res = fast.confirmNewRound(new Lq.ReqCommon { }, md);
                Console.WriteLine("ConfirmNewRound");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Bye()
        {

        }

        public override void Pass()
        {
            doRandomDelay();
            try
            {
                var res = fast.inputChiPengGang(new Lq.ReqChiPengGang
                {
                    CancelOperation = true,
                    Timeuse = (uint)stopwatch.Elapsed.Seconds
                }, md);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Discard(Tile tile)
        {
            doRandomDelay();
            try
            {
                var res = fast.inputOperation(new Lq.ReqSelfOperation
                {
                    Type = nextReach ? (uint)7 : (uint)1,
                    Tile = tile.OfficialName,
                    Moqie = gameData.lastTile == tile,
                    Timeuse = (uint)stopwatch.Elapsed.Seconds
                }, md);

                nextReach = false;
                lastDiscardedTile = tile;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Pon(Tile tile0, Tile tile1)
        {
            doRandomDelay();
            var combination = operationList.First(item => (int)item.Type == 3).Combination;
            int index = combination.ToList().FindIndex(comb => comb.Contains(tile0.GeneralName));
            try
            {
                var res = fast.inputChiPengGang(new Lq.ReqChiPengGang
                {
                    Type = 3,
                    Index = (uint)index,
                    Timeuse = (uint)stopwatch.Elapsed.Seconds
                }, md);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Minkan()
        {
            doRandomDelay();
            try
            {
                var res = fast.inputChiPengGang(new Lq.ReqChiPengGang
                {
                    Type = 5,
                    Index = (uint)0,
                    Timeuse = (uint)stopwatch.Elapsed.Seconds
                }, md);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Chii(Tile tile0, Tile tile1)
        {
            doRandomDelay();
            var combination = operationList.First(item => (int)item.Type == 2).Combination;
            int index = combination.ToList().FindIndex(comb => comb.Split('|').OrderBy(t => t).SequenceEqual(new[] { tile0.OfficialName, tile1.OfficialName }.OrderBy(t => t)));
            try
            {
                var res = fast.inputChiPengGang(new Lq.ReqChiPengGang
                {
                    Type = 2,
                    Index = (uint)index,
                    Timeuse = (uint)stopwatch.Elapsed.Seconds
                }, md);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Ankan(Tile tile)
        {
            doRandomDelay();
            var combination = operationList.First(item => (int)item.Type == 4).Combination;
            int index = combination.ToList().FindIndex(comb => comb.Contains(tile.GeneralName));
            try
            {
                var res = fast.inputChiPengGang(new Lq.ReqChiPengGang
                {
                    Type = 4,
                    Index = (uint)index,
                    Timeuse = (uint)stopwatch.Elapsed.Seconds
                }, md);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Kakan(Tile tile)
        {
            doRandomDelay();
            var combination = operationList.First(item => (int)item.Type == 6).Combination;
            int index = combination.ToList().FindIndex(comb => comb.Contains(tile.GeneralName) || comb.Contains(tile.OfficialName));
            try
            {
                var res = fast.inputChiPengGang(new Lq.ReqChiPengGang
                {
                    Type = 6,
                    Index = (uint)index,
                    Timeuse = (uint)stopwatch.Elapsed.Seconds
                }, md);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Ron()
        {
            try
            {
                var res = fast.inputChiPengGang(new Lq.ReqChiPengGang
                {
                    Type = 9,
                    Index = 0
                }, md);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Tsumo()
        {
            try
            {
                var res = fast.inputChiPengGang(new Lq.ReqChiPengGang
                {
                    Type = 8,
                    Index = 0
                }, md);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Ryuukyoku()
        {
            doRandomDelay();
            try
            {
                var res = fast.inputChiPengGang(new Lq.ReqChiPengGang
                {
                    Type = 10,
                    Index = 0,
                    Timeuse = (uint)stopwatch.Elapsed.Seconds
                }, md);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void Nuku()
        {
            throw new NotSupportedException();
        }

        public override void Reach(Tile tile)
        {
            nextReach = true;
            player.reached = true;
        }

        private void DelayedNextReady()
        {
            new Thread(() =>
            {
                Thread.Sleep(5000);
                if (!gameEnded)
                {
                    NextReady();
                }
            }).Start();
        }

        private void StartGame(bool continued)
        {
            try
            {
                InvokeOnUnknownEvent("Game found. Connecting...");
                var res = fast.authGame(new Lq.ReqAuthGame
                {
                    AccountId = (uint)accountId,
                    Token = connect_token,
                    GameUuid = game_uuid
                }, md);
                HandleMessage(new MajsoulMessage
                {
                    Success = true,
                    Type = MajsoulMessageType.RESPONSE,
                    MethodName = "AuthGame",
                    Message = res
                });

                SaveReplay(game_uuid);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private int NormalizedPlayerId(int seat)
        {
            return (seat - playerSeat + 4) % 4;
        }

        private void HandleMessage(MajsoulMessage message, bool forSync = false)
        {
            //Console.WriteLine("{0}\n{1}\n{2} {3} {4}", message.MethodName, message.Message, syncing, forSync, syncing && !forSync && message.MethodName != "FetchGamePlayerState");
            if (syncing && !forSync && message.MethodName != "FetchGamePlayerState")
            {
                pendingActions.Enqueue(message);
                return;
            }

            if (message.MethodName != null && timers.ContainsKey(message.MethodName))
            {
                timers[message.MethodName].Dispose();
            }

            if (message.MethodName == "NotifyGameFinishReward")
            {
                var msg = (Lq.NotifyGameFinishReward)message.Message;
                try
                {
                    level = msg.LevelChange.Final.Id;
                    Console.WriteLine("Level Change: origin {0}: {1} {2}: {3}", msg.LevelChange.Origin.Id, msg.LevelChange.Origin.Score, msg.LevelChange.Final.Id, msg.LevelChange.Final.Score);
                } catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            if (message.MethodName == "NotifyLicence")
            {
                var msg = (Lq.NotifyLicence)message.Message;
                Console.WriteLine("ExpireTime: {0} LeftMatchCount: {1}", msg.Time, msg.MatchCount);
            }

            if (!message.Success && message.MethodName != "AuthGame")
            {
                return;
            }
            if (message.MethodName == "Login" || message.MethodName == "Oauth2Login")
            {
                var msg = (Lq.ResLogin)message.Message;
                accountId = (int)msg.AccountId;

                if (msg.Error != null && msg.Error.Code != (uint)0)
                {
                    InvokeOnLogin(resume: false, succeeded: false);
                }
                else if (msg.GameInfo != null)
                {
                    continued = true;
                    fast = new Lq.FastTest.FastTestClient(channel);
                    notify = new Lq.Notify.NotifyClient(channel);
                    call = notify.Notify(new Lq.ClientStream { }, md);
                    _ = CreateNotify();
                    connect_token = msg.GameInfo.ConnectToken;
                    game_uuid = msg.GameInfo.GameUuid;
                    location = msg.GameInfo.Location;
                    InvokeOnLogin(resume: true, succeeded: true);
                }
                else
                {
                    fast = new Lq.FastTest.FastTestClient(channel);
                    notify = new Lq.Notify.NotifyClient(channel);
                    call = notify.Notify(new Lq.ClientStream { }, md);
                    _ = CreateNotify();
                    InvokeOnLogin(resume: false, succeeded: true);
                }
                disconnect = false;
            }
            if (message.MethodName == "NotifyRoomGameStart")
            {
                var msg = (Lq.NotifyRoomGameStart)message.Message;
                connect_token = msg.ConnectToken;
                game_uuid = msg.GameUuid;
                location = msg.Location;
            } else if (message.MethodName == "NotifyMatchGameStart")
            {
                var msg = (Lq.NotifyMatchGameStart)message.Message;
                connect_token = msg.ConnectToken;
                game_uuid = msg.GameUuid;
                location = msg.Location;
            }
            else if (message.MethodName == "NotifyGameClientConnect")
            {
                StartGame(false);
            }
            else if (message.MethodName == "NotifyDisconnect")
            {
                disconnect = true;
                Login();
            }
            else if (message.MethodName == "NotifyGameSync")
            {
                StartGame(true);
            }
            else if (message.MethodName == "AuthGame")
            {
                var msg = (Lq.ResAuthGame)message.Message;

                InvokeOnGameStart(continued);

                if (!continued)
                {
                    try
                    {
                        fast.enterGame(new Lq.ReqCommon { }, md);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
                else
                {
                    try
                    {
                        var res = fast.syncGame(new Lq.ReqSyncGame
                        {
                            RoundId = "-1",
                            Step = 1000000
                        }, md);
                        HandleMessage(new MajsoulMessage
                        {
                            Success = true,
                            Type = MajsoulMessageType.RESPONSE,
                            MethodName = "SyncGame",
                            Message = res
                        });
                        continued = false;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }
            else if (message.MethodName == "NotifyPlayerLoadGameReady")
            {
                var msg = (Lq.NotifyPlayerLoadGameReady)message.Message;
                playerSeat = msg.ReadyIdList.Select(t => (int)t).ToList().IndexOf(accountId);
            }
            else if (message.MethodName == "ActionMJStart")
            {
                gameEnded = false;
            }
            else if (message.MethodName == "NotifyGameEndResult")
            {
                Bye();
                gameEnded = true;
                InvokeOnGameEnd();
            }
            else if (message.MethodName == "NotifyEndGameVote")
            {
                new Thread(() =>
                {
                    try
                    {
                        fast.voteGameEnd(new Lq.ReqVoteGameEnd { Yes = true }, md);
                    }
                    catch { }
                }).Start();
                Bye();
                gameEnded = true;
                InvokeOnGameEnd();
            }
            else if (message.MethodName == "ActionHule")
            {
                var msg = (Lq.ActionHule)message.Message;

                int[] points = msg.Scores.Select(t => (int)t).ToArray();
                int[] rawPointDeltas = msg.DeltaScores.Select(t => (int)t).ToArray();
                int[] pointDeltas = new int[4];

                for (var i = 0; i < 4; i++)
                {
                    gameData.players[NormalizedPlayerId(i)].point = points[i];
                    pointDeltas[NormalizedPlayerId(i)] = rawPointDeltas[i];
                }

                foreach (var agari in msg.Hules)
                {
                    Player who = gameData.players[NormalizedPlayerId((int)agari.Seat)];
                    Player fromWho = pointDeltas.Count(s => s < 0) == 1 ? gameData.players[Array.FindIndex(pointDeltas, s => s < 0)] : who;
                    int point = !agari.Zimo ? (int)agari.PointRong : agari.Qinjia ? (int)agari.PointZimoXian * 3 : (int)agari.PointZimoXian * 2 + (int)agari.PointZimoQin;
                    if (gameData.lastTile != null)
                    {
                        gameData.lastTile.IsTakenAway = true;
                    }
                    if (agari.Yiman)
                    {
                        SaveReplayTag("Yakuman");
                    }
                    InvokeOnAgari(who, fromWho, point, pointDeltas, gameData.players);
                }

                DelayedNextReady();
            }
            else if (message.MethodName == "ActionLiuJu")
            {
                InvokeOnAgari(null, null, 0, new[] { 0, 0, 0, 0 }, gameData.players);
                DelayedNextReady();
            }
            else if (message.MethodName == "ActionNoTile")
            {
                var msg = (Lq.ActionNoTile)message.Message;

                var scoreObj = msg.Scores[0];
                int[] rawPointDeltas = scoreObj.DeltaScores != null ? scoreObj.DeltaScores.Select(t => (int)t).ToArray() : new[] { 0, 0, 0, 0 };
                if (rawPointDeltas.Length != 4)
                {
                    rawPointDeltas = new[] { 0, 0, 0, 0 };
                }
                int[] pointDeltas = new int[4];
                for (var i = 0; i < 4; i++)
                {
                    try
                    {
                        gameData.players[NormalizedPlayerId(i)].point = (int)scoreObj.OldScores[i] + rawPointDeltas[i];
                        pointDeltas[NormalizedPlayerId(i)] = rawPointDeltas[i];
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        Console.WriteLine("Index Out Of Range:" + gameData.players.Length + ":" + i + ":" + NormalizedPlayerId(i) + ":" + scoreObj.OldScores + ":" + rawPointDeltas.Length);
                    }
                }
                InvokeOnAgari(null, null, 0, pointDeltas, gameData.players);
                DelayedNextReady();
            }
            else if (message.MethodName == "ActionNewRound")
            {
                var msg = (Lq.ActionNewRound)message.Message;

                Tile.Reset();
                gameData = new GameData();
                HandleInit(msg);

                if (!syncing)
                {
                    InvokeOnInit(/* continued */ false, gameData.direction, gameData.seq, gameData.seq2, gameData.players);
                }

                if (player.hand.Count > 13)
                {
                    operationList = msg.Operation.OperationList;
                    if (!syncing)
                    {
                        Thread.Sleep(2000); // 等待发牌动画结束
                        stopwatch.Restart();
                        InvokeOnDraw(player.hand.Last());
                    }
                }
            }
            else if (message.MethodName == "SyncGame")
            {
                var msg = (Lq.ResSyncGame)message.Message;

                syncing = true;
                continuedBetweenGames = (int)msg.Step == 0;
                try
                {
                    if (msg.GameRestore != null && msg.GameRestore.Actions != null)
                    {
                        foreach (var action in msg.GameRestore.Actions)
                        {
                            HandleSyncGameMessage(action.Name, action.Data);
                        }
                    }
                    var res = fast.fetchGamePlayerState(new Lq.ReqCommon { }, md);
                    HandleMessage(new MajsoulMessage
                    {
                        Success = true,
                        Type = MajsoulMessageType.RESPONSE,
                        MethodName = "FetchGamePlayerState",
                        Message = res
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            else if (message.MethodName == "FetchGamePlayerState")
            {
                var msg = (Lq.ResGamePlayerState)message.Message;

                bool inited = false;

                playerSeat = msg.StateList.ToList().IndexOf(Lq.GamePlayerState.Syncing); // - 2;

                while (pendingActions.Count > 1)
                {
                    var actionMessage = pendingActions.Dequeue();
                    if (actionMessage.MethodName == "ActionNewRound")
                    {
                        inited = true;
                    }
                    HandleMessage(actionMessage, forSync: true);
                }

                try
                {
                    var res = fast.finishSyncGame(new Lq.ReqCommon { }, md);
                    HandleMessage(new MajsoulMessage
                    {
                        Success = true,
                        Type = MajsoulMessageType.RESPONSE,
                        MethodName = "FinishSyncGame",
                        Message = res
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                syncing = false;

                if (inited)
                {
                    InvokeOnInit(/* continued */ true, gameData.direction, gameData.seq, gameData.seq2, gameData.players);
                }

                // Queue里的最后一个action需要响应
                if (pendingActions.Count > 0)
                {
                    HandleMessage(pendingActions.Dequeue());
                }

                if (continuedBetweenGames)
                {
                    NextReady();
                }
            }
            else if (message.MethodName == "ActionDealTile")
            {
                var msg = (Lq.ActionDealTile)message.Message;

                gameData.remainingTile = (int)msg.LeftTileCount;
                if (msg.Doras != null && msg.Doras.Count > 0)
                {
                    try {
                        foreach (var dora in msg.Doras.Skip(gameData.dora.Count))
                        {
                            gameData.dora.Add(new Tile(dora));
                        }
                    } catch (Exception e) {
                        Console.WriteLine("ActionDealTile Doras Out of Range:\n" + msg);
                    }
                }
                if (NormalizedPlayerId((int)msg.Seat) == 0)
                {
                    try
                    {
                        Tile tile = new Tile(msg.Tile);
                        player.hand.Add(tile);
                        gameData.lastTile = tile;

                                                        
                        operationList = msg.Operation.OperationList;

                        if (!syncing && operationList.Count > 0)
                        {
                            stopwatch.Restart();
                            InvokeOnDraw(tile);
                        }

                    } catch (Exception e)
                    {
                        Console.WriteLine("Deal error:");
                        Console.WriteLine(playerSeat);
                        Console.WriteLine(msg.Seat);
                        Console.WriteLine(NormalizedPlayerId((int)msg.Seat));
                        Console.WriteLine(msg);
                    }
                }
            }
            else if (message.MethodName == "ActionDiscardTile")
            {
                var msg = (Lq.ActionDiscardTile)message.Message;

                Player currentPlayer = gameData.players[NormalizedPlayerId((int)msg.Seat)];
                if (!msg.Moqie)
                {
                    currentPlayer.safeTiles.Clear();
                }
                var tileName = msg.Tile;
                if (currentPlayer != null && player != null && currentPlayer == player)
                {
                    if (lastDiscardedTile == null || lastDiscardedTile.OfficialName != tileName)
                    {
                        lastDiscardedTile = player.hand.First(t => t.OfficialName == tileName);
                    }
                    player.hand.Remove(lastDiscardedTile);
                }
                Tile tile = currentPlayer == player ? lastDiscardedTile : new Tile(tileName);
                lastDiscardedTile = null;
                if (tile != null)
                {
                    currentPlayer.graveyard.Add(tile);
                } else
                {
                    Console.WriteLine("Discard error:");
                    Console.WriteLine(playerSeat);
                    Console.WriteLine(msg.Seat);
                    Console.WriteLine(NormalizedPlayerId((int)msg.Seat));
                    Console.WriteLine(msg);
                }
                gameData.lastTile = tile;
                foreach (var p in gameData.players)
                {
                    p.safeTiles.Add(tile);
                }
                if (msg.IsLiqi|| msg.IsWliqi)
                {
                    currentPlayer.reached = true;
                    currentPlayer.safeTiles.Clear();
                    if (!syncing) InvokeOnReach(currentPlayer);
                }
                if (!syncing) InvokeOnDiscard(currentPlayer, tile);
                if (msg.Doras != null && msg.Doras.Count > 0)
                {
                    foreach (var dora in msg.Doras.Skip(gameData.dora.Count))
                    {
                        gameData.dora.Add(new Tile(dora));
                    }
                }
                if (msg.Operation != null && msg.Operation.OperationList.Count > 0)
                {
                    operationList = msg.Operation.OperationList;
                    if (!syncing)
                    {
                        stopwatch.Restart();
                        InvokeOnWait(tile, currentPlayer);
                    }
                }
            }
            else if (message.MethodName == "ActionChiPengGang")
            {
                var msg = (Lq.ActionChiPengGang)message.Message;

                Player currentPlayer = gameData.players[NormalizedPlayerId((int)msg.Seat)];
                var fuuro = HandleFuuro(currentPlayer, (int)msg.Type, msg.Tiles, msg.Froms.Select(t => (int)t));

                if (!syncing) InvokeOnNaki(currentPlayer, fuuro);
            }
            else if (message.MethodName == "ActionAnGangAddGang")
            {
                var msg = (Lq.ActionAnGangAddGang)message.Message;

                Player currentPlayer = gameData.players[NormalizedPlayerId((int)msg.Seat)];
                FuuroGroup fuuro = null;
                if ((int)msg.Type == 2)
                {
                    fuuro = HandleKakan(currentPlayer, msg.Tiles);
                }
                else if ((int)msg.Type == 3)
                {
                    fuuro = HandleAnkan(currentPlayer, msg.Tiles);
                }

                if (!syncing) InvokeOnNaki(currentPlayer, fuuro);
            }
        }

        private void HandleInit(Lq.ActionNewRound data)
        {

            switch ((int)data.Chang)
            {
                case 0:
                    gameData.direction = Direction.E;
                    break;
                case 1:
                    gameData.direction = Direction.S;
                    break;
                case 2:
                    gameData.direction = Direction.W;
                    break;
            }

            gameData.seq = (int)data.Ju + 1;
            gameData.seq2 = (int)data.Ben;
            gameData.reachStickCount = (int)data.Liqibang;

            gameData.remainingTile = GameData.initialRemainingTile;

            gameData.dora.Clear();
            gameData.dora.Add(new Tile((string)data.Doras[0]));

            for (int i = 0; i < 4; i++)
            {
                gameData.players[NormalizedPlayerId(i)].point = (int)data.Scores[i];
                gameData.players[NormalizedPlayerId(i)].reached = false;
                gameData.players[NormalizedPlayerId(i)].graveyard = new Graveyard();
                gameData.players[NormalizedPlayerId(i)].fuuro = new Fuuro();
                gameData.players[NormalizedPlayerId(i)].hand = new Hand();
            }

            int oyaNum = (4 - playerSeat + (int)data.Ju) % 4;
            gameData.players[oyaNum].direction = Direction.E;
            gameData.players[(oyaNum + 1) % 4].direction = Direction.S;
            gameData.players[(oyaNum + 2) % 4].direction = Direction.W;
            gameData.players[(oyaNum + 3) % 4].direction = Direction.N;

            foreach (var tileName in data.Tiles.Select(t => (string)t))
            {
                player.hand.Add(new Tile(tileName));
            }
        }

        private FuuroGroup HandleFuuro(Player currentPlayer, int type, IEnumerable<string> tiles, IEnumerable<int> froms)
        {
            FuuroGroup fuuroGroup = new FuuroGroup();
            switch (type)
            {
                case 0:
                    fuuroGroup.type = FuuroType.chii;
                    break;
                case 1:
                    fuuroGroup.type = FuuroType.pon;
                    break;
                case 2:
                    fuuroGroup.type = FuuroType.minkan;
                    break;
            }

            foreach (var (tileName, from) in tiles.Zip(froms, Tuple.Create))
            {
                if (NormalizedPlayerId(from) != currentPlayer.id) // 从别人那里拿来的牌
                {
                    fuuroGroup.Add(gameData.lastTile);
                    if (gameData.lastTile != null)
                    {
                        gameData.lastTile.IsTakenAway = true;
                    }
                }
                else if (currentPlayer == player) // 自己的手牌
                {
                    Tile tile = player.hand.First(t => t.OfficialName == tileName);
                    player.hand.Remove(tile);
                    fuuroGroup.Add(tile);
                }
                else
                {
                    fuuroGroup.Add(new Tile(tileName));
                }
            }

            currentPlayer.fuuro.Add(fuuroGroup);
            return fuuroGroup;
        }

        private FuuroGroup HandleAnkan(Player currentPlayer, string tileName)
        {
            tileName = tileName.Replace('0', '5');

            FuuroGroup fuuroGroup = new FuuroGroup();
            fuuroGroup.type = FuuroType.ankan;

            if (currentPlayer == player)
            {
                IEnumerable<Tile> tiles = player.hand.Where(t => t.GeneralName == tileName).ToList();
                fuuroGroup.AddRange(tiles);
                player.hand.RemoveRange(tiles);
            }
            else
            {
                if (tileName[0] == '5' && tileName[1] != 'z') // 暗杠中有红牌
                {
                    fuuroGroup.Add(new Tile(tileName));
                    fuuroGroup.Add(new Tile(tileName));
                    fuuroGroup.Add(new Tile(tileName));
                    fuuroGroup.Add(new Tile("0" + tileName[1]));
                }
                else
                {
                    for (var i = 0; i < 4; i++)
                    {
                        fuuroGroup.Add(new Tile(tileName));
                    }
                }
            }

            currentPlayer.fuuro.Add(fuuroGroup);
            return fuuroGroup;
        }

        private FuuroGroup HandleKakan(Player currentPlayer, string tileName)
        {
            FuuroGroup fuuroGroup = currentPlayer.fuuro.First(g => g.type == FuuroType.pon && g.All(t => t.GeneralName == tileName.Replace('0', '5')));
            fuuroGroup.type = FuuroType.kakan;

            if (currentPlayer == player)
            {
                Tile tile = player.hand.First(t => t.GeneralName == tileName.Replace('0', '5'));
                player.hand.Remove(tile);
                fuuroGroup.Add(tile);
            }
            else
            {
                fuuroGroup.Add(new Tile(tileName));
            }

            return fuuroGroup;
        }

        private void SaveReplay(string gameID)
        {
            StreamWriter writer = new StreamWriter(replaysFileName, true);
            writer.WriteLine("https://game.maj-soul.com/1/?paipu={0}", gameID);
            writer.Close();
        }

        private void SaveReplayTag(string tag)
        {
            StreamWriter writer = new StreamWriter(replaysFileName, true);
            writer.WriteLine("tag: {0}", tag);
            writer.Close();
        }

        private static string EncodePassword(string password)
        {
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes("lailai")))
            {
                return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(password))).Replace("-", "").ToLower();
            }
        }

        private string GetDeviceUUID()
        {
            string uuid = config.DeviceUuid; //(string)Properties.Settings.Default["DeviceUUID"];
            if (string.IsNullOrEmpty(uuid))
            {
                uuid = Guid.NewGuid().ToString();
                config.DeviceUuid = uuid;
                File.WriteAllText("config.json", JsonConvert.SerializeObject(config));
            }
            return uuid;
        }

        private void doRandomDelay()
        {
            if (stopwatch.Elapsed < TimeSpan.FromSeconds(2))
            {
                Thread.Sleep(random.Next(1, 4) * 1000);
            }
        }

        private void expectMessage(string methodName, int timeout, string timeoutMessage)
        {
            timers[methodName] = new Timer((state) =>
            {
                InvokeOnUnknownEvent(timeoutMessage);
                Close(true);
            }, state: null, dueTime: timeout, period: Timeout.Infinite);
        }
    }
}
