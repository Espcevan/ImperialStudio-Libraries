﻿using System;

namespace ImperialStudio.Core.Networking.Packets
{
    public class IncomingPacket
    {
        public NetworkPeer Peer { get; set; }
        public PacketType PacketType { get; set; }
        public byte[] Data { get; set; }
        public bool IsVerified { get; set; }
        public NetworkChannel Channel { get; set; }
    }
}