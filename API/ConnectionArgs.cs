namespace DevkitServer.API;

public delegate void ConnectionArgs(
#if SERVER
    ITransportConnection transportConnection
#elif CLIENT
        IClientTransport clientTransport
#endif
);
