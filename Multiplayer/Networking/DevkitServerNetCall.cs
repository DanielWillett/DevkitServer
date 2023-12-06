﻿namespace DevkitServer.Multiplayer.Networking;
public enum DevkitServerNetCall : ushort
{
    RequestLevel = 1,
    StartSendLevel = 2,
    SendLevel = 3,
    EndSendLevel = 4,
    RequestLevelPackets = 5,
    RequestLevelCheckup = 6,
    Ping = 7,
    SendSteamVerificationToken = 8,
    OpenHighSpeedClient = 9,
    SendPending = 10,
    SendTransform = 11,
    SendClientInfo = 12,
    SendPermissionState = 13,
    SendClearPermissions = 14,
    SendUpdateController = 15,
    SendPermissionGroupState = 16,
    SendClearPermissionGroups = 17,
    SendPermissionGroupLateRegistered = 18,
    SendPermissionGroupDeregistered = 19,
    SendPermissionGroupUpdate = 20,
    ReunRequest = 21,
    TileSyncAuthority = 22,
    EditorUIMessage = 23,
    RequestHierarchyInstantiation = 24,
    SendHierarchyInstantiation = 25,
    RequestLevelObjectInstantiation = 26,
    SendLevelObjectInstantiation = 27,
    RequestUpdateController = 28,
    RequestInitialState = 29,
    SendBuildableResponsibilities = 30,
    SendBindObject = 31,
    SendObjectSyncData = 32,
    SendBindHierarchyItem = 33,
    SendHierarchySyncData = 34,
    TranslatableEditorUIMessage = 35,
    AskSave = 36,
    SendBindSpawnpoint = 37,
    SendBakeNavRequest = 38,
    SendBindNavigation = 39,
    SendNavBakeProgressUpdate = 40,
    SendBindRoadElement = 41,
    RequestRoadInstantiation = 42,
    RequestRoadVertexInstantiation = 43,
    SendRoadInstantiation = 44,
    SendRoadVertexInstantiation = 45,
    SendRoadSyncData = 46,
    RequestFlagInstantiation = 47,
    SendFlagInstantiation = 48,
    SendStartLargeTransmission = 49,
    SendLargeTransmissionPacket = 50,
    SendEndLargeTransmission = 51,
    SendMissedLargeTransmissionPackets = 52,
    SendLargeTransmissionCheckup = 53,
    RequestOpenHighSpeedConnection = 54,
    RequestReleaseOrTakeHighSpeedConnection = 55
}