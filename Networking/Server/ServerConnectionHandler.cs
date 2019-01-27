﻿using Boo.Lang;
using Castle.Windsor;
using ENet;
using Facepunch.Steamworks;
using ImperialStudio.Core.Eventing;
using ImperialStudio.Core.Logging;
using ImperialStudio.Core.Map;
using ImperialStudio.Core.Networking.Packets.Handlers;
using ImperialStudio.Core.Networking.Packets.Serialization;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using ILogger = ImperialStudio.Core.Logging.ILogger;

namespace ImperialStudio.Core.Networking.Server
{
    public sealed class ServerConnectionHandler : BaseConnectionHandler
    {
        public const byte MaxPlayersUpperLimit = byte.MaxValue;
        public TimeSpan ClientTimeOut { get; set; }

        private const int PingInterval = 1000 * 5;

        public string Name { get; private set; }

        public byte MaxPlayers { get; set; }

        public bool UseCustomPingHandler { get; set; } = true;

        public ServerConnectionHandler(
            IPacketSerializer packetSerializer,
            INetworkEventHandler networkEventProcessor,
            ILogger logger,
            IEventBus eventBus,
            IWindsorContainer container,
            IMapManager mapManager) : base(packetSerializer, networkEventProcessor, logger, eventBus, container)
        {
            m_EventBus = eventBus;
            m_MapManager = mapManager;
            m_Logger = logger;

            ClientTimeOut = TimeSpan.FromSeconds(15);
        }

        private readonly IEventBus m_EventBus;
        private readonly IMapManager m_MapManager;
        private readonly ILogger m_Logger;
        private Thread m_PingThread;

        public void Host(ServerListenParameters listenParameters)
        {
            Name = listenParameters.Name;
            MaxPlayers = listenParameters.MaxPlayers;

            if (IsListening)
            {
                throw new Exception("Server is already listening.");
            }

            string hostName = listenParameters.Host;
            ushort port = listenParameters.Port;

            Address address = new Address { Port = port };

            if (!address.SetHost(hostName))
            {
                throw new Exception($"Failed to resolve host: \"{hostName}\"");
            }

            var host = GetOrCreateHost();

            try
            {
                host.Create(address, MaxPlayersUpperLimit, ChannelUpperLimit);
            }
            catch (Exception ex)
            {
                m_Logger.LogError($"Failed to host server. Is port {listenParameters.Port} already being used?", ex);
                Application.Quit(1);
                return;
            }

            StartListening();

            m_Logger.LogInformation($"Hosted server: {hostName}:{listenParameters.Port}");
            StartPingThread();

            m_MapManager.ChangeMap(listenParameters.Map);
            m_EventBus.Emit(this, new ServerInitializedEvent(listenParameters));
        }

        public void Disconnect(NetworkPeer peer, ServerAuth.Status reason)
        {
            if (peer.EnetPeer.State == PeerState.Disconnected || peer.EnetPeer.State == PeerState.Disconnecting || peer.EnetPeer.State == PeerState.DisconnectLater)
            {
                return;
            }

            m_Logger.LogWarning($"{peer.Name} was rejected because of failed Steam authentication: {reason}.");

            Send(peer, new TerminatePacket
            {
                AuthFailureReason = reason
            });
            peer.EnetPeer.Disconnect(0);
        }

        public void Disconnect(NetworkPeer peer, string reason)
        {
            if (peer.EnetPeer.State == PeerState.Disconnected || peer.EnetPeer.State == PeerState.Disconnecting || peer.EnetPeer.State == PeerState.DisconnectLater)
            {
                return;
            }

            m_Logger.LogInformation(peer.Name + " was disconnected: " + reason);

            Send(peer, new TerminatePacket
            {
                Reason = reason
            });
            peer.EnetPeer.Disconnect(0);
        }

        private void StartPingThread()
        {
            m_PendingPings = new List<PendingPing>();

            m_PingThread = new Thread(PingClients);
            m_PingThread.Start();
        }

        private List<PendingPing> m_PendingPings = new List<PendingPing>();
        private ulong m_NextPingId = 1;

        private void PingClients()
        {
            // Only keep subscription as long as this object is alive:
            object referenceObject = new object();

            m_EventBus.Subscribe<PongEvent>(referenceObject, OnPong);

            while (IsListening)
            {
                if (UseCustomPingHandler)
                {
                    var toDisconnect = new List<PendingPing>();

                    foreach (var pendingPing in m_PendingPings)
                    {
                        TimeSpan ping = DateTime.UtcNow - pendingPing.SendTime;

                        if (ping > ClientTimeOut)
                            toDisconnect.Add(pendingPing);

                        bool isConnected = pendingPing.NetworkPeer.EnetPeer.State == PeerState.Connected;

                        if (isConnected)
                        {
                            Disconnect(pendingPing.NetworkPeer, "Timeout.");
                        }
                    }

                    m_PendingPings.RemoveAll(d => toDisconnect.Contains(d));

                    foreach (var client in GetPeers())
                    {
                        ulong currentPingId = m_NextPingId;

                        PendingPing pendingPing = new PendingPing
                        {
                            SendTime = DateTime.UtcNow,
                            NetworkPeer = client,
                            PingId = currentPingId
                        };

                        m_PendingPings.Add(pendingPing);

                        Send(client, new PingPacket
                        {
                            PingId = currentPingId
                        });

                        m_NextPingId++;
                    }

                }
                else
                {
#if LOG_NETWORK
                    foreach (var peer in GetPeers())
                    {
                        m_Logger.LogDebug($"{peer.Name} ping: {peer.EnetPeer.RoundTripTime}ms");
                    }
#endif
                }

                Thread.Sleep(PingInterval);
            }
        }

        private void OnPong(object sender, PongEvent @event)
        {
            var pendingPing = m_PendingPings.FirstOrDefault(d => d.PingId == @event.PongPacket.PingId && d.NetworkPeer.EnetPeer.ID == @event.Peer.EnetPeer.ID);
            if (pendingPing.PingId == 0)
                return;

            m_PendingPings.Remove(pendingPing);

            TimeSpan ping = DateTime.UtcNow - pendingPing.SendTime;
            @event.Peer.Ping = ping;

#if LOG_NETWORK
            m_Logger.LogDebug($"{@event.Peer.Name} ping: {ping.TotalMilliseconds}ms");
#endif
        }

        private struct PendingPing
        {
            public NetworkPeer NetworkPeer { get; set; }
            public DateTime SendTime { get; set; }
            public ulong PingId { get; set; }
        }
    }
}
