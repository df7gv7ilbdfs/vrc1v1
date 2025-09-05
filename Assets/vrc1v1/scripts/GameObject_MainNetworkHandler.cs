
using System;
using System.Linq;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

public class GameObject_MainNetworkHandler : UdonSharpBehaviour
{
    [SerializeField] public GameObject RoomContainer;
    private GameObject[] RoomList;
    private TextMeshProUGUI[] timerList;
    private Button[] extendButtonList;
    private Button[] exitButtonList;
    private NetworkSyncObject syncObject = null;
    private DataList metPlayerList = new DataList();
    private DataList roomMatchList = new DataList();
    private DataList lastRoomMatchList = new DataList();
    private DataDictionary roomMatchDict = new DataDictionary();
    private DataDictionary playerMatchDict = new DataDictionary();
    private DataDictionary playerInfo = new DataDictionary();
    private double curTurnStartTime = 0;
    private double elapsedTime = 0;
    private long curTurn = -1;
    private bool receivedTurnInfo = false;
    [SerializeField] private int TurnTime = 20;
    private bool needReassignRoom = false;
    private int lastOldPairId = -1;
    private int lastOldRoom = -1;
    private int lastMinute = -1;
    private int lastSecond = -1;
    private bool ready_to_exit = false;
    private bool noplayer_to_assign = false;

    private const string PlayerInfo_Player = "Player";
    private const string PlayerInfo_Status = "Status";
    private const string PlayerInfo_OnlineTime = "OnlineTime";
    private const string PlayerInfo_ChatTime = "ChatTime";
    private const int PlayerInfo_Status_NotVerified = 0;
    private const int PlayerInfo_Status_Idle = 1;
    private const int PlayerInfo_Status_WaitAssign = 2;
    private const int PlayerInfo_Status_Assigned = 3;
    private const int PlayerInfo_Status_LockAssign = 4;

    private const string RPC_PlayerVerify = "pv";
    private const string RPC_PlayerExit = "pe";
    private const string RPC_PlayerWaitAssign = "pw";
    private const string RPC_PlayerAssign = "pa";
    private const string RPC_PlayerLockAssign = "pl";
    private const string RPC_AssignRoom = "ar";
    private const string RPC_AssignRoomWithAppend = "ara";
    private const string RPC_AppendPlayer = "ap";
    private const string RPC_ReassignRoom = "rr";

    public void NetworkSync_OnRPCReceived()
    {
        var playerId = Networking.LocalPlayer.playerId;

        if(syncObject.NetworkSync_IsOwner())
        {
            Debug.Log("I'm master");
        }
        else
        {
            Debug.Log($"I'm not master, my playerId: {playerId}");
        }

        switch(syncObject.NetworkSync_Arg_GetCmd())
        {
            case RPC_PlayerVerify:
            case RPC_PlayerExit:
            {
                var srcPlayer = syncObject.NetworkSync_Arg_GetSrcPlayer().playerId;

                if(playerInfo.ContainsKey(srcPlayer))
                {
                    playerInfo[srcPlayer].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_Idle;

                    if(syncObject.NetworkSync_Arg_GetCmd() == RPC_PlayerExit)
                    {
                        if(roomMatchDict[srcPlayer].Int != -1)
                        {
                            if(roomMatchList[roomMatchDict[srcPlayer].Int].DataDictionary[0].Int == srcPlayer)
                            {
                                roomMatchList[roomMatchDict[srcPlayer].Int].DataDictionary[0] = -1;
                            }

                            if(roomMatchList[roomMatchDict[srcPlayer].Int].DataDictionary[1].Int == srcPlayer)
                            {
                                roomMatchList[roomMatchDict[srcPlayer].Int].DataDictionary[1] = -1;
                            }

                            roomMatchDict[srcPlayer] = -1;
                        }

                        if(playerMatchDict[srcPlayer].Int != -1)
                        {
                            if(playerMatchDict.ContainsKey(playerMatchDict[srcPlayer].Int))
                            {
                                playerMatchDict[playerMatchDict[srcPlayer].Int] = -1;
                            }

                            playerMatchDict[srcPlayer] = -1;
                        }
                    }
                }

                break;
            }

            case RPC_PlayerWaitAssign:
            {
                var srcPlayer = syncObject.NetworkSync_Arg_GetSrcPlayer().playerId;
                
                if(playerInfo.ContainsKey(srcPlayer))
                {
                    playerInfo[srcPlayer].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_WaitAssign;
                }

                break;
            }

            case RPC_PlayerAssign:
            {
                var srcPlayer = syncObject.NetworkSync_Arg_GetSrcPlayer().playerId;
                var args = syncObject.NetworkSync_Arg_GetArg();

                if(args != null)
                {
                    srcPlayer = (int)args;
                }
                
                if(playerInfo.ContainsKey(srcPlayer))
                {
                    playerInfo[srcPlayer].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_Assigned;
                }

                break;
            }

            case RPC_PlayerLockAssign:
            {
                var srcPlayer = syncObject.NetworkSync_Arg_GetSrcPlayer().playerId;

                if(playerInfo.ContainsKey(srcPlayer))
                {
                    playerInfo[srcPlayer].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_LockAssign;
                }

                break;
            }

            case RPC_AssignRoom:
            case RPC_AssignRoomWithAppend:
            {
                var args = (DataList)syncObject.NetworkSync_Arg_GetArg();
                var newRoomMatchList = args[0].DataList;
                var playerStatusList = args[1].DataDictionary;
                curTurn = args[2].Long;
                elapsedTime = args[3].Double;
                receivedTurnInfo = true;

                if(playerInfo.ContainsKey(playerId))
                {
                    if(syncObject.NetworkSync_Arg_GetCmd() == RPC_AssignRoom)
                    {
                        if(!playerStatusList.ContainsKey(playerId))
                        {
                            ReassignRoom();
                            break;
                        }

                        if(playerStatusList[playerId].Int != playerInfo[playerId].DataDictionary[PlayerInfo_Status].Int)
                        {
                            ReassignRoom();
                            break;
                        }
                    }

                    BackupPlayerAssignment();
                    needReassignRoom = false;
                    var oldPair = playerMatchDict[playerId].Int;
                    var oldRoom = roomMatchDict[playerId].Int;

                    //清空当前房间和配对信息
                    var playerIdList = playerInfo.GetKeys();

                    for(var i = 0;i < playerIdList.Count;i++)
                    {
                        var id = playerIdList[i].Int;
                        
                        if(roomMatchDict.ContainsKey(id))
                        {
                            roomMatchDict[id] = -1;
                        }

                        if(playerMatchDict.ContainsKey(id))
                        {
                            playerMatchDict[id] = -1;
                        }
                    }

                    for(var i = 0;i < newRoomMatchList.Count;i++)
                    {
                        var player1 = newRoomMatchList[i].DataDictionary[0].Int;
                        var player2 = newRoomMatchList[i].DataDictionary[1].Int;

                        if(!playerMatchDict.ContainsKey(player1))
                        {
                            player1 = -1;
                        }

                        if(!playerMatchDict.ContainsKey(player2))
                        {
                            player2 = -1;
                        }

                        roomMatchList[i].DataDictionary[0] = player1;
                        roomMatchList[i].DataDictionary[1] = player2;
                    
                        if(player1 >= 0)
                        {
                            roomMatchDict[player1] = i;
                            playerMatchDict[player1] = player2;   
                        }

                        if(player2 >= 0)
                        {
                            roomMatchDict[player2] = i;
                            playerMatchDict[player2] = player1;
                        }
                    }

                    if(playerInfo[playerId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_Assigned)
                    {
                        Debug.Log("ResetTurn-NetworkSync_OnRPCReceived");
                        ResetTurn(oldPair, playerMatchDict[playerId].Int, oldRoom, roomMatchDict[playerId].Int);
                    }

                    Debug.Log($"Received master assign player to room, curTurn: {curTurn}, elapsedTime: {elapsedTime}");
                    Debug.Log("roomMatchList: ");

                    for(var i = 0;i < roomMatchList.Count;i++)
                    {
                        var player1 = roomMatchList[i].DataDictionary[0].Int;
                        var player2 = roomMatchList[i].DataDictionary[1].Int;
                        Debug.Log($"Room {i}: Player1: {player1}, Player2: {player2}");
                    }
                }

                break;
            }

            case RPC_AppendPlayer:
            {
                var srcPlayer = syncObject.NetworkSync_Arg_GetSrcPlayer().playerId;

                if(playerInfo.ContainsKey(srcPlayer))
                {
                    AppendPlayer(srcPlayer);
                }

                break;
            }

            case RPC_ReassignRoom:
            {
                if(syncObject.NetworkSync_IsOwner())
                {
                    AssignPlayer(true);

                    //Master负责重置自己的轮次
                    if(playerInfo[playerId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_Assigned)
                    {
                        Debug.Log("ResetTurn-Update");
                        ResetTurn(-2, playerMatchDict[playerId].Int, -2, roomMatchDict[playerId].Int);
                    }
                }
                else
                {
                    needReassignRoom = true;
                }

                break;
            }
        }
    }

    public void NetworkSync_OnPongReceived()
    {
    
    }

    public void NetworkSync_OnGetValue()
    {
    
    }

    public void NetworkSync_OnSetValue()
    {
    
    }

    public void NetworkSync_OnGotOwner()
    {
    
    }

    private void ApplyOwnerSetting()
    {
        var isOwner = Networking.IsOwner(gameObject);

        if(isOwner)
        {
            bool has_waitassign = false;
            bool has_assigned = false;
            var playerIdList = playerInfo.GetKeys();

            for(var i = 0;i < playerIdList.Count;i++)
            {
                var id = playerIdList[i].Int;

                if(playerInfo[id].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_WaitAssign)
                {
                    has_waitassign = true;
                }

                if(playerInfo[id].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_LockAssign)
                {
                    var pairId = playerMatchDict[id].Int;

                    if(playerInfo.ContainsKey(pairId) && (playerInfo[pairId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_LockAssign))
                    {
                        //什么都不需要做
                    }
                    else
                    {
                        has_waitassign = true;//判定为双方有一个人未按下延长按钮，标记为需要重分配房间
                    }
                }

                if(playerInfo[id].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_Assigned)
                {
                    has_assigned = true;
                    break;
                }
            }

            if(has_waitassign && !has_assigned && playerInfo.ContainsKey(Networking.LocalPlayer.playerId))
            {
                //需要立即执行房间分配算法，之前的房主可能客户端在分配途中崩溃
                //先记录自己更新前的配对信息
                var playerId = Networking.LocalPlayer.playerId;
                int oldRoom = roomMatchDict[playerId].Int;
                int oldPair = playerMatchDict[playerId].Int;
                curTurn++;
                elapsedTime = 0;
                AssignPlayer(false);
                receivedTurnInfo = true;

                //Master负责重置自己的轮次
                if(playerInfo[playerId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_Assigned)
                {
                    Debug.Log("ResetTurn-ApplyOwnerSetting");
                    ResetTurn(oldPair, playerMatchDict[playerId].Int, oldRoom, roomMatchDict[playerId].Int);
                }
            }
            else if(needReassignRoom)
            {
                //需要重分配房间
                var playerId = Networking.LocalPlayer.playerId;
                AssignPlayer(true);

                //Master负责重置自己的轮次
                if(playerInfo[playerId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_Assigned)
                {
                    Debug.Log("ResetTurn-ApplyOwnerSetting");
                    ResetTurn(-2, playerMatchDict[playerId].Int, -2, roomMatchDict[playerId].Int);
                }

                needReassignRoom = false;
            }
        }
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        base.OnOwnershipTransferred(player);
        ApplyOwnerSetting();
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        base.OnPlayerJoined(player);

        var dict = new DataDictionary();
        dict[PlayerInfo_Player] = new DataToken(player);
        dict[PlayerInfo_Status] = PlayerInfo_Status_NotVerified;
        dict[PlayerInfo_OnlineTime] = 0.0;
        dict[PlayerInfo_ChatTime] = 0.0;
        playerInfo.Add(player.playerId, dict);
        roomMatchDict.Add(player.playerId, -1);
        playerMatchDict.Add(player.playerId, -1);

        if(player.playerId == Networking.LocalPlayer.playerId)
        {
            for(var i = 0;i < RoomList.Length;i++)
            {
                RoomList[i].SetActive(false);
            }
        }
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        base.OnPlayerLeft(player);

        if(playerInfo.ContainsKey(player.playerId))
        {
            playerInfo.Remove(player.playerId);

            var room = roomMatchDict[player.playerId].Int;
        
            if(room >= 0)
            {
                if(roomMatchList[room].DataDictionary[0].Int == player.playerId)
                {
                    roomMatchList[room].DataDictionary[0] = -1;
                }

                if(roomMatchList[room].DataDictionary[1].Int == player.playerId)
                {
                    roomMatchList[room].DataDictionary[1] = -1;
                }
            }

            if(playerMatchDict.ContainsKey(playerMatchDict[player.playerId].Int))
            {
                playerMatchDict[playerMatchDict[player.playerId].Int] = -1;
            }

            roomMatchDict.Remove(player.playerId);
            playerMatchDict.Remove(player.playerId);
        }
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        base.OnPlayerRespawn(player);
        _PlayerExit();
    }

    //该函数用于答题控件在完成答题后调用，从而将玩家状态调整到Idle状态，并广播该状态
    public void PlayerVerify()
    {
        var playerId = Networking.LocalPlayer.playerId;

        if(playerInfo.ContainsKey(playerId))
        {
            playerInfo[playerId].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_Idle;
            syncObject.NetworkSync_SendRPC(null, RPC_PlayerVerify, null);
        }
    }

    //该函数用于让玩家进入待分配状态
    public void PlayerWaitAssign()
    {
        var playerId = Networking.LocalPlayer.playerId;
        
        if(playerInfo.ContainsKey(playerId))
        {
            playerInfo[playerId].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_WaitAssign;
            syncObject.NetworkSync_SendRPC(null, RPC_PlayerWaitAssign, null);
        }
    }

    public void PlayerExit()
    {
        Networking.LocalPlayer.Respawn();
    }

    //该函数用于让玩家退出待分配状态
    private void _PlayerExit()
    {
        var playerId = Networking.LocalPlayer.playerId;

        if(playerInfo.ContainsKey(playerId))
        {
            if(roomMatchDict[playerId].Int != -1)
            {
                if(roomMatchList[roomMatchDict[playerId].Int].DataDictionary[0].Int == playerId)
                {
                    roomMatchList[roomMatchDict[playerId].Int].DataDictionary[0] = -1;
                }

                if(roomMatchList[roomMatchDict[playerId].Int].DataDictionary[1].Int == playerId)
                {
                    roomMatchList[roomMatchDict[playerId].Int].DataDictionary[1] = -1;
                }

                roomMatchDict[playerId] = -1;
            }

            if(playerMatchDict[playerId].Int != -1)
            {
                if(playerMatchDict.ContainsKey(playerMatchDict[playerId].Int))
                {
                    playerMatchDict[playerMatchDict[playerId].Int] = -1;
                }

                playerMatchDict[playerId] = -1;
            }

            for(var i = 0;i < RoomList.Length;i++)
            {
                RoomList[i].SetActive(false);
            }
            
            playerInfo[playerId].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_Idle;
            syncObject.NetworkSync_SendRPC(null, RPC_PlayerExit, null);
        }
    }

    private void PlayerAssigned()
    {
        var playerId = Networking.LocalPlayer.playerId;

        if(playerInfo.ContainsKey(playerId))
        {
            playerInfo[playerId].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_Assigned;
            syncObject.NetworkSync_SendRPC(null, RPC_PlayerAssign, null);
        }
    }

    public void PlayerLockAssign()
    {
        var playerId = Networking.LocalPlayer.playerId;

        if(playerInfo.ContainsKey(playerId))
        {
            playerInfo[playerId].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_LockAssign;
            syncObject.NetworkSync_SendRPC(null, RPC_PlayerLockAssign, null);
        }
    }

    public void PlayerUnlockAssign()
    {
        var playerId = Networking.LocalPlayer.playerId;

        if(playerInfo.ContainsKey(playerId))
        {
            playerInfo[playerId].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_Assigned;
            syncObject.NetworkSync_SendRPC(null, RPC_PlayerAssign, null);
        }
    }

    //该函数用于追加玩家
    public void AppendPlayer(int playerId)
    {
        //若当前是Master，直接执行追加操作，否则发送RPC给Master执行追加操作
        if(syncObject.NetworkSync_IsOwner())
        {
            if(curTurn == -1)
            {
                curTurn = 0;
                elapsedTime = 0;
                AssignPlayer(false);
                receivedTurnInfo = true;
            }
            else if(noplayer_to_assign)
            {
                curTurn++;
                elapsedTime = 0;
                AssignPlayer(false);
                receivedTurnInfo = true;
            }
            else
            {
                if(roomMatchDict.ContainsKey(playerId) && (roomMatchDict[playerId].Int == -1))
                {
                    //寻找是否有包含一个玩家的房间和空闲房间，优先选择包含一个玩家的房间进行玩家配对
                    var oneRoom = -1;
                    var idleRoom = -1;

                    for(var i = 0;i < RoomList.Length;i++)
                    {
                        if(oneRoom == -1)
                        {
                            if(((roomMatchList[i].DataDictionary[0].Int == -1) && (roomMatchList[i].DataDictionary[1].Int >= 0)) || ((roomMatchList[i].DataDictionary[1].Int == -1) && (roomMatchList[i].DataDictionary[0].Int >= 0)))
                            {
                                oneRoom = i;
                            }
                        }

                        if(idleRoom == -1)
                        {
                            if((roomMatchList[i].DataDictionary[0].Int == -1) && (roomMatchList[i].DataDictionary[1].Int == -1))
                            {
                                idleRoom = i;
                            }
                        }
                    }

                    var room = idleRoom;

                    if(oneRoom >= 0)
                    {
                        room = oneRoom;
                    }

                    if(roomMatchList[room].DataDictionary[0].Int == -1)
                    {
                        roomMatchList[room].DataDictionary[0] = playerId;
                        playerMatchDict[playerId] = roomMatchList[room].DataDictionary[1].Int;
                    }
                    else
                    {
                        roomMatchList[room].DataDictionary[1] = playerId;
                        playerMatchDict[playerId] = roomMatchList[room].DataDictionary[0].Int;
                    }

                    roomMatchDict[playerId] = room;
                    playerInfo[playerId].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_Assigned;
                    syncObject.NetworkSync_SendRPC(null, RPC_PlayerAssign, playerId);

                    var playerStatusList = new DataDictionary();
                    var playerIdList = playerInfo.GetKeys();

                    for(var i = 0;i < playerIdList.Count;i++)
                    {
                        var tplayerId = playerIdList[i].Int;
                        playerStatusList.Add(tplayerId, playerInfo[tplayerId].DataDictionary[PlayerInfo_Status].Int);
                    }

                    var args = new DataList();
                    args.Add(roomMatchList);
                    args.Add(playerStatusList);
                    args.Add(curTurn);
                    args.Add(elapsedTime);
                    syncObject.NetworkSync_SendRPC(null, RPC_AssignRoomWithAppend, args);

                    Debug.Log($"Master append player to room, curTurn: {curTurn}");
                    Debug.Log("roomMatchList: ");

                    for(var i = 0;i < roomMatchList.Count;i++)
                    {
                        var player1 = roomMatchList[i].DataDictionary[0].Int;
                        var player2 = roomMatchList[i].DataDictionary[1].Int;
                        Debug.Log($"Room {i}: Player1: {player1}, Player2: {player2}");
                    }
                }
            }

            //Master负责重置自己的轮次
            if((playerId == Networking.LocalPlayer.playerId) && roomMatchDict.ContainsKey(playerId))
            {
                var room = roomMatchDict[playerId].Int;
                Debug.Log($"Master reset turn to {curTurn} for player {playerId} in room {room}");
                ResetTurn(-1, playerMatchDict[playerId].Int, -1, roomMatchDict[playerId].Int);
            }
        }
        else
        {
            syncObject.NetworkSync_SendRPC(syncObject.NetworkSync_GetOwner(), RPC_AppendPlayer, null);
        }
    }

    //发起重分配房间请求，此操作是当玩家检测到分配房间请求携带的玩家状态信息与玩家当前状态不一致时调用，禁止Master调用
    private void ReassignRoom()
    {
        if(!syncObject.NetworkSync_IsOwner())
        {
            needReassignRoom = true;
            syncObject.NetworkSync_SendRPC(null, RPC_ReassignRoom, null);//该请求应当广播，从而防止在此时Master正好发生转移，导致请求不被正确处理
        }
    }

    private void BackupPlayerAssignment()
    {
        for(var i = 0;i < RoomList.Length;i++)
        {
            lastRoomMatchList[i].DataDictionary[0] = roomMatchList[i].DataDictionary[0].Int;
            lastRoomMatchList[i].DataDictionary[1] = roomMatchList[i].DataDictionary[1].Int;
        }
    }

    //该函数用于分配玩家，仅允许Master调用
    private void AssignPlayer(bool retry)
    {
        DataList waitAssignList = new DataList();
        DataList roomOccupied = new DataList();
        DataList idleRoomList = new DataList();

        //判断是否为重分配
        if(retry)
        {
            var playerList = roomMatchDict.GetKeys();

            for(var i = 0;i < playerList.Count;i++)
            {
                var playerId = playerList[i].Int;
                roomMatchDict[playerId] = -1;
                playerMatchDict[playerId] = -1;
            }

            for(var i = 0;i < RoomList.Length;i++)
            {
                roomMatchList[i].DataDictionary[0] = lastRoomMatchList[i].DataDictionary[0].Int;
                roomMatchList[i].DataDictionary[1] = lastRoomMatchList[i].DataDictionary[1].Int;

                if(roomMatchDict.ContainsKey(roomMatchList[i].DataDictionary[0].Int))
                {
                    roomMatchDict[roomMatchList[i].DataDictionary[0].Int] = i;
                }

                if(roomMatchDict.ContainsKey(roomMatchList[i].DataDictionary[1].Int))
                {
                    roomMatchDict[roomMatchList[i].DataDictionary[1].Int] = i;
                }

                if(playerMatchDict.ContainsKey(roomMatchList[i].DataDictionary[0].Int))
                {
                    if(playerMatchDict.ContainsKey(roomMatchList[i].DataDictionary[1].Int))
                    {
                        playerMatchDict[roomMatchList[i].DataDictionary[0].Int] = roomMatchList[i].DataDictionary[1].Int;
                    }
                    else
                    {
                        playerMatchDict[roomMatchList[i].DataDictionary[0].Int] = -1;
                    }
                }

                if(playerMatchDict.ContainsKey(roomMatchList[i].DataDictionary[1].Int))
                {
                    if(playerMatchDict.ContainsKey(roomMatchList[i].DataDictionary[0].Int))
                    {
                        playerMatchDict[roomMatchList[i].DataDictionary[1].Int] = roomMatchList[i].DataDictionary[0].Int;
                    }
                    else
                    {
                        playerMatchDict[roomMatchList[i].DataDictionary[1].Int] = -1;
                    }
                }
            }
        }
        else
        {
            BackupPlayerAssignment();
        }

        //首先初始化房间占用状态
        for(var i = 0;i < RoomList.Length;i++)
        {
            roomOccupied.Add(false);
        }

        //首先提取出所有待分配玩家列表
        var playerIdList = playerInfo.GetKeys();
        Debug.Log("CurPlayerStatus:");

        for(var i = 0;i < playerIdList.Count;i++)
        {
            var playerId = playerIdList[i].Int;

            Debug.Log($"Player {playerId} is {playerInfo[playerId].DataDictionary[PlayerInfo_Status]}");

            if(playerInfo[playerId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_WaitAssign)
            {
                waitAssignList.Add(playerId);
                noplayer_to_assign = false;
            }

            //检查配对玩家是否都处于延时状态，如果不处于延时状态，也加入待分配列表
            if((playerInfo[playerId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_LockAssign))
            {
                noplayer_to_assign = false;

                if(playerInfo.ContainsKey(playerMatchDict[playerId].Int) &&
                (playerInfo[playerMatchDict[playerId].Int].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_LockAssign))
                {
                    //处于延时状态，将对应房间标记为已占用状态，防止被再次分配
                    var room = roomMatchDict[playerId].Int;

                    if(room >= 0)
                    {
                        roomOccupied[room] = true;
                    }
                }
                else
                {
                    waitAssignList.Add(playerId);
                }
            }
        }

        //生成空闲房间列表，同时清空空闲房间的玩家
        for(var i = 0;i < RoomList.Length;i++)
        {
            if(!roomOccupied[i].Boolean)
            {
                idleRoomList.Add(i);

                if(roomMatchDict.ContainsKey(roomMatchList[i].DataDictionary[0].Int))
                {
                    roomMatchDict[roomMatchList[i].DataDictionary[0].Int] = -1;
                }

                if(roomMatchDict.ContainsKey(roomMatchList[i].DataDictionary[1].Int))
                {
                    roomMatchDict[roomMatchList[i].DataDictionary[1].Int] = -1;
                }

                roomMatchList[i].DataDictionary[0] = -1;
                roomMatchList[i].DataDictionary[1] = -1;
            }
        }

        //对玩家执行洗牌算法
        for(var i = 0;i < waitAssignList.Count;i++)
        {
            var rand = UnityEngine.Random.Range(i, waitAssignList.Count);
            var t = waitAssignList[i];
            waitAssignList[i] = waitAssignList[rand];
            waitAssignList[rand] = t;
        }

        //对空闲房间执行洗牌算法
        for(var i = 0;i < idleRoomList.Count;i++)
        {
            var rand = UnityEngine.Random.Range(i, idleRoomList.Count);
            var t = idleRoomList[i];
            idleRoomList[i] = idleRoomList[rand];
            idleRoomList[rand] = t;
        }

        //执行房间重分配，分配玩家时同时变更玩家状态为已分配，防止后续重复分配
        var roomIndex = 0;
        var playerIndex = 0;

        while((roomIndex < idleRoomList.Count) && (playerIndex < waitAssignList.Count))
        {
            var room = idleRoomList[roomIndex++].Int;
            var player1 = waitAssignList[playerIndex++].Int;
            var player2 = -1;

            if(playerIndex < waitAssignList.Count)
            {
                player2 = waitAssignList[playerIndex++].Int;
            }
            
            roomMatchList[room].DataDictionary[0] = player1;
            roomMatchList[room].DataDictionary[1] = player2;
            
            if(roomMatchDict.ContainsKey(player1))
            {
                roomMatchDict[player1] = room;
            }

            if(roomMatchDict.ContainsKey(player2))
            {
                roomMatchDict[player2] = room;
            }

            if(playerMatchDict.ContainsKey(player1))
            {
                playerMatchDict[player1] = player2;
                playerInfo[player1].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_Assigned;
                syncObject.NetworkSync_SendRPC(null, RPC_PlayerAssign, player1);
            }

            if(playerMatchDict.ContainsKey(player2))
            {
                playerMatchDict[player2] = player1;
                playerInfo[player2].DataDictionary[PlayerInfo_Status] = PlayerInfo_Status_Assigned;
                syncObject.NetworkSync_SendRPC(null, RPC_PlayerAssign, player2);
            }
        }

        var playerStatusList = new DataDictionary();

        for(var i = 0;i < playerIdList.Count;i++)
        {
            var playerId = playerIdList[i].Int;
            playerStatusList.Add(playerId, playerInfo[playerId].DataDictionary[PlayerInfo_Status].Int);
        }

        var args = new DataList();
        args.Add(roomMatchList);
        args.Add(playerStatusList);
        args.Add(curTurn);
        args.Add(elapsedTime);
        syncObject.NetworkSync_SendRPC(null, RPC_AssignRoom, args);
        //output roomMatchList with loop and curTurn for debug
        Debug.Log($"Master assign player to room, curTurn: {curTurn}");
        Debug.Log("roomMatchList: ");

        for(var i = 0;i < roomMatchList.Count;i++)
        {
            var player1 = roomMatchList[i].DataDictionary[0].Int;
            var player2 = roomMatchList[i].DataDictionary[1].Int;
            Debug.Log($"Room {i}: Player1: {player1}, Player2: {player2}");
        }
    }

    void ResetTurn(int oldPairId, int newPairId, int oldRoom, int newRoom)
    {
        var playerId = Networking.LocalPlayer.playerId;

        if(oldPairId == -2)
        {
            oldPairId = lastOldPairId;
        }
        else
        {
            lastOldPairId = oldPairId;
        }

        if(oldRoom == -2)
        {
            oldRoom = lastOldRoom;
        }
        else
        {
            lastOldRoom = oldRoom;
        }

        //如果前后轮对面玩家没有改变，房间也没有改变，且都按下了延长按钮，则不触发切换效果，也不改变状态，否则正常触发切换效果
        //这么多限制条件是处理在接近轮次结束时间时双方按下延长按钮的状态没有及时同步到聊天双方以及Master的情况
        if((oldPairId == newPairId) && 
           (oldRoom == newRoom) &&
           playerInfo.ContainsKey(playerId) && 
           playerInfo.ContainsKey(oldPairId) && 
           (playerInfo[oldPairId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_LockAssign) && 
           (playerInfo[playerId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_LockAssign))
        {
            //这里什么也不做
        }
        else if((newRoom >= 0) && (oldRoom != newRoom))
        {
            //改变为已分配状态
            PlayerAssigned();
            //玩家跳转到新房间
            Debug.Log($"Player {playerId} reset turn to {curTurn} in room {newRoom}, pair {newPairId}, oldroom {oldRoom}, oldpair {oldPairId}");

            for(var i = 0;i < RoomList.Length;i++)
            {
                RoomList[i].SetActive(false);
            }

            RoomList[newRoom].SetActive(true);
            ready_to_exit = false;
            extendButtonList[newRoom].gameObject.GetComponent<Image>().color = Color.white;
            exitButtonList[newRoom].gameObject.GetComponent<Image>().color = Color.white;
            GameObject spawnObj = RoomList[newRoom].GetComponentInChildrenByName("Match_Control_Menu");

            if(spawnObj != null)
            {
                Networking.LocalPlayer.TeleportTo(spawnObj.transform.position, spawnObj.transform.rotation);
            }
        }
    }

    public void OnExtend()
    {
        var playerId = Networking.LocalPlayer.playerId;

        if(playerInfo.ContainsKey(playerId))
        {
            var playerStatus = playerInfo[playerId].DataDictionary[PlayerInfo_Status];

            if(playerStatus == PlayerInfo_Status_Assigned)
            {
                PlayerLockAssign();
                extendButtonList[roomMatchDict[playerId].Int].gameObject.GetComponent<Image>().color = Color.green;
            }
            else if(playerStatus == PlayerInfo_Status_LockAssign)
            {
                PlayerAssigned();
                extendButtonList[roomMatchDict[playerId].Int].gameObject.GetComponent<Image>().color = Color.white;
            }
        }
    }

    public void OnExit()
    {
        var playerId = Networking.LocalPlayer.playerId;

        if(playerInfo.ContainsKey(playerId))
        {
            var playerStatus = playerInfo[playerId].DataDictionary[PlayerInfo_Status];

            if((playerStatus == PlayerInfo_Status_WaitAssign) || (playerStatus == PlayerInfo_Status_Assigned) || (playerStatus == PlayerInfo_Status_LockAssign))
            {
                if(!ready_to_exit)
                {
                    if(playerStatus == PlayerInfo_Status_LockAssign)
                    {
                        PlayerAssigned();
                        extendButtonList[roomMatchDict[playerId].Int].gameObject.GetComponent<Image>().color = Color.white;
                    }

                    exitButtonList[roomMatchDict[playerId].Int].gameObject.GetComponent<Image>().color = Color.green;
                    ready_to_exit = true;
                }
                else
                {
                    exitButtonList[roomMatchDict[playerId].Int].gameObject.GetComponent<Image>().color = Color.white;
                    ready_to_exit = false;
                }
            }
        }
    }

    void Update()
    {
        if(syncObject.NetworkSync_IsNetworkRegistered() && playerInfo.ContainsKey(Networking.LocalPlayer.playerId))
        {
            var playerId = Networking.LocalPlayer.playerId;

            //判断当前是否处于有效轮次
            if(curTurn >= 0)
            {
                //判断新轮次是否已经开始，如果已经开始，重置轮次开始时间
                if(receivedTurnInfo)
                {
                    receivedTurnInfo = false;
                    curTurnStartTime = Time.unscaledTimeAsDouble - elapsedTime;
                    lastMinute = -1;
                    lastSecond = -1;
                }
                else
                {
                    elapsedTime = Time.unscaledTimeAsDouble - curTurnStartTime;
                }

                //检查玩家是否处于已分配状态
                if((playerInfo[playerId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_Assigned) || (playerInfo[playerId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_LockAssign))
                {
                    var room = roomMatchDict[playerId].Int;

                    if(room >= 0)
                    {
                        var remainingTime = (int)(TurnTime - ((long)(Time.unscaledTimeAsDouble - curTurnStartTime)));

                        if(remainingTime < 0)
                        {
                            remainingTime = 0;
                        }

                        var minute = remainingTime / 60;
                        var second = remainingTime % 60;

                        if((minute != lastMinute) || (second != lastSecond))
                        {
                            timerList[room].text = $"{minute:D2}:{second:D2}";
                            lastMinute = minute;
                            lastSecond = second;
                        }
                    }

                    //若处于已分配状态且预定时间到达
                    if((Time.unscaledTimeAsDouble - curTurnStartTime) >= TurnTime)
                    {
                        //判断一对玩家是否互相都选择了延长
                        var pairId = playerMatchDict[playerId].Int;

                        if((playerInfo[playerId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_LockAssign) && (pairId >= 0) && playerInfo.ContainsKey(pairId) && (playerInfo[pairId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_LockAssign))
                        {
                            //等待Master下发指令，通知新的轮次开始
                        }
                        else if(ready_to_exit)
                        {
                            PlayerExit();
                            ready_to_exit = false;
                        }
                        else
                        {
                            //玩家恢复为待分配状态，且广播通知自己的状态改变
                            Debug.Log($"Player {playerId} turn time out, reset to WaitAssign, curTurnStartTime: {curTurnStartTime}");
                            PlayerWaitAssign();
                        }
                    }
                }
                
                //Master负责玩家重分配
                if(Networking.IsOwner(gameObject) && ((Time.unscaledTimeAsDouble - curTurnStartTime) >= TurnTime))
                {
                    //考虑到各个玩家的网络延迟和本地时钟波动，可能有玩家仍处于已分配状态，等待所有玩家全部离开已分配状态
                    bool has_assigned = false;
                    bool has_wait_assign = false;
                    bool has_lock_assign = false;
                    var playerIdList = playerInfo.GetKeys();

                    for(var i = 0;i < playerIdList.Count;i++)
                    {
                        var id = playerIdList[i].Int;

                        if(playerInfo[id].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_Assigned)
                        {
                            has_assigned = true;
                            break;
                        }

                        if(playerInfo[id].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_WaitAssign)
                        {
                            has_wait_assign = true;
                        }

                        if(playerInfo[id].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_LockAssign)
                        {
                            var pairId = playerMatchDict[id].Int;

                            if((pairId >= 0) && playerInfo.ContainsKey(pairId) && (playerInfo[pairId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_LockAssign))
                            {
                                has_lock_assign = true;
                            }
                        }
                    }

                    //先记录自己更新前的配对信息
                    int oldRoom = roomMatchDict[playerId].Int;
                    int oldPair = playerMatchDict[playerId].Int;

                    //若没有玩家进入已分配状态，且没有玩家处于待分配状态和锁定状态，设置无玩家可分配标志
                    if(!has_assigned && !has_wait_assign && !has_lock_assign)
                    {
                        noplayer_to_assign = true;
                    }
                    else
                    {
                        noplayer_to_assign = false;
                    }

                    //若没有玩家进入已分配状态，且有玩家处于待分配状态，执行玩家重分配操作
                    if(!has_assigned && (has_wait_assign || has_lock_assign))
                    {
                        curTurn++;
                        elapsedTime = 0;
                        curTurnStartTime = Time.unscaledTimeAsDouble;
                        AssignPlayer(false);

                        //Master负责重置自己的轮次
                        if(playerInfo[playerId].DataDictionary[PlayerInfo_Status] == PlayerInfo_Status_Assigned)
                        {
                            Debug.Log("ResetTurn-Update");
                            ResetTurn(oldPair, playerMatchDict[playerId].Int, oldRoom, roomMatchDict[playerId].Int);
                        }
                    }
                }
            }
        }
    }

    void Start()
    {
        syncObject = gameObject.GetComponent<NetworkSyncObject>();
        syncObject.EventObject = this;
        RoomList = new GameObject[RoomContainer.transform.childCount];

        for(var i = 0;i < RoomList.Length;i++)
        {
            RoomList[i] = RoomContainer.transform.GetChild(i).gameObject;
        }

        Debug.Log($"RoomList length: {RoomList.Length}");

        roomMatchList = new DataList();
        timerList = new TextMeshProUGUI[RoomList.Length];
        extendButtonList = new Button[RoomList.Length];
        exitButtonList = new Button[RoomList.Length];

        for(var i = 0;i < RoomList.Length;i++)
        {
            var dict = new DataDictionary();
            dict[0] = -1;
            dict[1] = -1;
            roomMatchList.Add(dict);
            dict = new DataDictionary();
            dict[0] = -1;
            dict[1] = -1;
            lastRoomMatchList.Add(dict);
            timerList[i] = RoomList[i].gameObject.GetComponentInChildrenByName("UI_Timer").GetComponentInChildren<Canvas>().gameObject.GetComponentInChildrenByName("t_timer").GetComponent<TextMeshProUGUI>();
            extendButtonList[i] = RoomList[i].gameObject.GetComponentInChildrenByName("Match_Control_Menu").GetComponentInChildren<Canvas>().gameObject.GetComponentInChildrenByName("Extend").GetComponent<Button>();
            exitButtonList[i] = RoomList[i].gameObject.GetComponentInChildrenByName("Match_Control_Menu").GetComponentInChildren<Canvas>().gameObject.GetComponentInChildrenByName("Exit").GetComponent<Button>();
        }

        roomMatchDict = new DataDictionary();
        playerMatchDict = new DataDictionary();
    }
}
