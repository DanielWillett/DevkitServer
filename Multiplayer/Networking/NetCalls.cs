﻿namespace DevkitServer.Multiplayer.Networking;
internal enum NetCalls : ushort
{
    SendLog = 1,
    RequestLevel = 2,
    StartSendLevel = 3,
    SendLevel = 4,
    EndSendLevel = 5,
    RequestLevelPackets = 6,
    SendTileData = 7,
    RequestLevelCheckup = 8,
    Ping = 9,
    SendSteamVerificationToken = 10,
    OpenHighSpeedClient = 11,
    SendPending = 12,
    SendInitialPosition = 13,
    SendClientInfo = 14,
    SendPermissionState = 15,
    SendClearPermissions = 16,
    SendUpdateController = 17,
    SendPermissionGroupState = 18,
    SendClearPermissionGroups = 19,
    SendPermissionLateRegistered = 20,
    SendPermissionGroupLateRegistered = 21,
    SendPermissionDeregistered = 22,
    SendPermissionGroupDeregistered = 23,
    SendPermissionGroupUpdate = 24
}
