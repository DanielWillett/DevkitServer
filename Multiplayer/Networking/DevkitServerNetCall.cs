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
    SendPermissionLateRegistered = 18,
    SendPermissionGroupLateRegistered = 19,
    SendPermissionDeregistered = 20,
    SendPermissionGroupDeregistered = 21,
    SendPermissionGroupUpdate = 22,
    ReunRequest = 23,
    TileSyncAuthority = 24,
    EditorUIMessage = 25,
    RequestHierarchyInstantiation = 26,
    SendHierarchyInstantiation = 27,
    RequestLevelObjectInstantiation = 28,
    SendLevelObjectInstantiation = 29,
    RequestUpdateController = 30,
    RequestInitialState = 31,
    SendBuildableResponsibilities = 33,
    SendBindObject = 34,
    SendObjectSyncData = 35,
    SendBindHierarchyItem = 36,
    SendHierarchySyncData = 37,
    TranslatableEditorUIMessage = 38,
    AskSave = 39,
    SendBindSpawnpoint = 40,
    SendBakeNavRequest = 41,
    SendBindNavigation = 42
}
