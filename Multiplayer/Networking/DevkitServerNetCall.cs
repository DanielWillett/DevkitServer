﻿namespace DevkitServer.Multiplayer.Networking;

public enum DevkitServerNetCall : ushort
{
    RequestLevel = 1,
    Ping = 2,
    SendSteamVerificationToken = 3,
    OpenHighSpeedClient = 4,
    SendPending = 5,
    SendTransform = 6,
    SendClientInfo = 7,
    SendPermissionState = 8,
    SendClearPermissions = 9,
    SendUpdateController = 10,
    SendPermissionGroupState = 11,
    SendClearPermissionGroups = 12,
    SendPermissionGroupLateRegistered = 13,
    SendPermissionGroupDeregistered = 14,
    SendPermissionGroupUpdate = 15,
    ReunRequest = 16,
    TileSyncAuthority = 17,
    EditorUIMessage = 18,
    RequestHierarchyInstantiation = 19,
    SendHierarchyInstantiation = 20,
    RequestLevelObjectInstantiation = 21,
    SendLevelObjectInstantiation = 22,
    RequestUpdateController = 23,
    RequestInitialState = 24,
    SendBuildableResponsibilities = 25,
    SendBindObject = 26,
    SendObjectSyncData = 27,
    SendBindHierarchyItem = 28,
    SendHierarchySyncData = 29,
    TranslatableEditorUIMessage = 30,
    AskSave = 31,
    SendBindSpawnpoint = 32,
    SendBakeNavRequest = 33,
    SendBindNavigation = 34,
    SendNavBakeProgressUpdate = 35,
    SendBindRoadElement = 36,
    RequestRoadInstantiation = 37,
    RequestRoadVertexInstantiation = 38,
    SendRoadInstantiation = 39,
    SendRoadVertexInstantiation = 40,
    SendRoadSyncData = 41,
    RequestFlagInstantiation = 42,
    SendFlagInstantiation = 43,
    SendStartLargeTransmission = 44,
    SendLargeTransmissionPacket = 45,
    SendEndLargeTransmission = 46,
    SendMissedLargeTransmissionPackets = 47,
    SendLargeTransmissionCheckup = 48,
    RequestOpenHighSpeedConnection = 49,
    RequestReleaseOrTakeHighSpeedConnection = 50,
    SendRemoteNetMessageMappings = 51,
    SendBasicSpawnTableInstantiation = 52,
    SendZombieSpawnTableInstantiation = 53,
    RequestSpawnTableInstantiation = 54,
    SendSpawnTierInstantiation = 55,
    RequestSpawnTierInstantiation = 56,
    SendSpawnAssetInstantiation = 57,
    RequestSpawnAssetInstantiation = 58
}