#if !UNITY_WEBGL || UNITY_EDITOR

using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport
{
    internal static class WebSocket
    {
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void Warn(string msg) => DebugLog.LogWarning(msg);

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void WarnIf(bool condition, string msg) { if (condition) DebugLog.LogWarning(msg); }

        public enum Opcode
        {
            Continuation = 0,
            TextData = 1,
            BinaryData = 2,
            Close = 8,
            Ping = 9,
            Pong = 10
        }

        public enum StatusCode
        {
            Normal = 1000,
            ProtocolError = 1002,
            UnsupportedDataType = 1003,
            MessageTooBig = 1009,
            InternalError = 1011,
        }

        public enum State
        {
            None,
            ClosedAndFlushed,
            Closed,
            Closing,
            Opening,
            Open,
        }

        public enum Role
        {
            Server = 0, // incoming (passive) connection
            Client = 1, // outgoing (active) connection
        }

        public static unsafe void Connect(ref Buffer buffer, ref NetworkEndpoint remoteEndpoint, ref Keys keys)
        {
            FixedString32Bytes end = "\r\n";
            FixedString512Bytes handshake = "GET / HTTP/1.1\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\n";
            FixedString32Bytes key = "Sec-WebSocket-Key: ";
            handshake.Append(key);
            GenerateBase64Key(out key, ref keys);
            handshake.Append(key);
            handshake.Append(end);
            FixedString128Bytes host = "Host: ";
            handshake.Append(host);
            handshake.Append(remoteEndpoint.ToFixedString());
            handshake.Append(end);
            handshake.Append(end);

            fixed (byte* data = buffer.Data)
            { 
                UnsafeUtility.MemCpy(data, handshake.GetUnsafePtr(), (uint)handshake.Length);
            }
            buffer.Length = handshake.Length;
        }

        public static unsafe State Handshake(ref Buffer recvbuffer, ref Buffer sendbuffer, bool isClient, ref Keys keys)
        {
            if (recvbuffer.Length == 0)
                return State.Opening;

            // If we can't find an ETX the message is not complete yet.
            var complete = false;
            int length;
            for (length = 0; length < recvbuffer.Length - 3 && !complete; ++length)
                complete = recvbuffer.Data[length] == '\r'
                        && recvbuffer.Data[length + 1] == '\n'
                        && recvbuffer.Data[length + 2] == '\r'
                        && recvbuffer.Data[length + 3] == '\n';

            if (!complete)
                return State.Opening;

            length += 3;
            fixed (byte* data = recvbuffer.Data)
            {
                // If you need to inspect this while debugging open the Immediate Window in Visual Studio and enter
                // "System.Text.Encoding.UTF8.GetString(recvHandshake)"
                var recvHandshake = data;
                var recvHandshakeLength = length;

                int lineStart = 0;
                int lineEnd = 0;
                while (recvHandshake[lineEnd] != '\r' || recvHandshake[lineEnd + 1] != '\n')
                    ++lineEnd;

                int firstLineEnd = lineEnd;
                var headerLookup = new NativeParallelHashMap<FixedString512Bytes, FixedString512Bytes>(16, Allocator.Temp);
                while (true)
                {
                    lineEnd += 2;
                    lineStart = lineEnd;
                    while (recvHandshake[lineEnd] != '\r' || recvHandshake[lineEnd + 1] != '\n')
                        ++lineEnd;

                    if (lineStart == lineEnd)
                        break;

                    // Found a line - analyze it
                    int keyStart = lineStart;
                    while (recvHandshake[keyStart] == ' ' || recvHandshake[keyStart] == '\t')
                        ++keyStart;

                    int keyEnd = keyStart;
                    FixedString512Bytes key = default;
                    
                    // Not allowing whitespace in keys
                    while (recvHandshake[keyEnd] != ':' && recvHandshake[keyEnd] != ' ' 
                        && recvHandshake[keyEnd] != '\t' && recvHandshake[keyEnd] != '\r' 
                        && recvHandshake[keyEnd] != '\n')
                    {
                        byte ch = recvHandshake[keyEnd];
                        if (ch >= (byte)'A' && ch <= (byte)'Z')
                            ch = (byte)(ch + 'a' - 'A');
                        key.Add(ch);
                        ++keyEnd;
                    }

                    int valueStart = keyEnd;
                    while (recvHandshake[valueStart] != ':')
                    {
                        if (recvHandshake[valueStart] != ' ' && recvHandshake[valueStart] != '\t' 
                            && recvHandshake[valueStart] != '\r' && recvHandshake[valueStart] != '\n')
                            break;

                        ++valueStart;
                    }
                    if (recvHandshake[valueStart] != ':')
                        continue;

                    ++valueStart;
                    while (recvHandshake[valueStart] == ' ' || recvHandshake[valueStart] == '\t')
                        ++valueStart;

                    FixedString512Bytes value = default;
                    int valueEnd = valueStart;
                    while (valueEnd < recvHandshakeLength && recvHandshake[valueEnd] != '\r')
                    {
                        if (value.Length == value.Capacity)
                        {
                            Warn("Received invalid http message for handshake: too large for string buffer.");
                            return State.Closed;
                        }

                        value.Add(recvHandshake[valueEnd]);
                        ++valueEnd;
                    }

                    // Trim trailing whitespace
                    while (value.Length > 0 && (value[value.Length - 1] == ' ' || value[value.Length - 1] == '\t'))
                        value.Length = value.Length - 1;

                    headerLookup.TryAdd(key, value);
                }

                FixedString128Bytes keyMagic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                FixedString128Bytes connectionHeader = "connection";
                FixedString128Bytes upgradeHeader = "upgrade";
                FixedString512Bytes headerValue;
                var invalidConnection = !headerLookup.TryGetValue(connectionHeader, out headerValue) || headerValue.Length < 7;
                // Scan for "upgrade" in a coma separated list
                if (!invalidConnection)
                {
                    invalidConnection = true;
                    int upPos = 0;
                    int len = 0;
                    while ((len = headerValue.Length - upPos) >= 7)
                    {
                        invalidConnection = ((headerValue[upPos + 0] | 32) != 'u' || (headerValue[upPos + 1] | 32) != 'p' || (headerValue[upPos + 2] | 32) != 'g' ||
                            (headerValue[upPos + 3] | 32) != 'r' || (headerValue[upPos + 4] | 32) != 'a' || (headerValue[upPos + 5] | 32) != 'd' || (headerValue[upPos + 6] | 32) != 'e');
                        if (!invalidConnection)
                        {
                            if (len == 7 || headerValue[upPos + 7] == ',' || headerValue[upPos + 7] == ' ' || headerValue[upPos + 7] == '\t')
                                break;
                            invalidConnection = true;
                        }
                        while (upPos < headerValue.Length && headerValue[upPos] != ',')
                            ++upPos;
                        // Skip ,
                        ++upPos;
                        // skip whitespace
                        while (upPos < headerValue.Length && (headerValue[upPos] == ' ' || headerValue[upPos] == '\t'))
                            ++upPos;
                    }
                }
                var invalidUpgrade = (!headerLookup.TryGetValue(upgradeHeader, out headerValue) || headerValue.Length != 9 ||
                    (headerValue[0] | 32) != 'w' || (headerValue[1] | 32) != 'e' || (headerValue[2] | 32) != 'b' || (headerValue[3] | 32) != 's' || (headerValue[4] | 32) != 'o' || (headerValue[5] | 32) != 'c' || (headerValue[6] | 32) != 'k' || (headerValue[7] | 32) != 'e' || (headerValue[8] | 32) != 't');
                
                // Try to parse the xpecetd handshake message: server expects HTTP UPGRADE; client expects
                // HTTP SWITCHING PROTOCOL.
                if (isClient)
                {
                    var invalidStatusLine = (firstLineEnd < 14 ||
                        recvHandshake[0]  != 'H' || recvHandshake[1] != 'T' || recvHandshake[2]  != 'T' || recvHandshake[3]  != 'P' ||
                        recvHandshake[4]  != '/' || recvHandshake[5] != '1' || recvHandshake[6]  != '.' || recvHandshake[7]  != '1' ||
                        recvHandshake[8]  != ' ' || recvHandshake[9] != '1' || recvHandshake[10] != '0' || recvHandshake[11] != '1' ||
                        recvHandshake[12] != ' ');

                    if (invalidStatusLine)
                    {
                        Warn("Unexpected server response. Could not parse http status line.");
                        return State.Closed;
                    }

                    FixedString128Bytes protocolHeader = "sec-websocket-protocol";
                    FixedString128Bytes extensionHeader = "sec-websocket-extensions";
                    FixedString128Bytes acceptHeader = "sec-websocket-accept";
                    FixedString512Bytes wsKey;

                    if (invalidConnection || invalidUpgrade || headerLookup.ContainsKey(protocolHeader) ||
                        headerLookup.ContainsKey(extensionHeader) || !headerLookup.TryGetValue(acceptHeader, out wsKey))
                    {
                        WarnIf(invalidConnection, "Received handshake with invalid or missing Connection key.");
                        WarnIf(invalidUpgrade, "Received handshake with invalid or missing Upgrade key.");
                        WarnIf(headerLookup.ContainsKey(protocolHeader), "Received handshake with a subprotocol != null.");
                        WarnIf(headerLookup.ContainsKey(extensionHeader), "Received handshake with an extension.");
                        WarnIf (!headerLookup.ContainsKey(acceptHeader), "Received handshake with a missing sec-websocket-accept key.");

                        return State.Closed;
                    }

                    // validate the accept header
                    FixedString512Bytes refWsKey = default;
                    GenerateBase64Key(out var clientKey, ref keys);
                    refWsKey.Append(clientKey);
                    refWsKey.Append(keyMagic);
                    var hash = new SHA1(refWsKey);
                    clientKey = hash.ToBase64();
                    if (wsKey != clientKey)
                    {
                        Warn("Received handshake with incorrect sec-websocket-accept.");
                        return State.Closed;
                    }                 
                }
                else
                {
                    FixedString512Bytes handshake;
                    FixedString128Bytes hostHeader = "host";
                    FixedString128Bytes keyHeader = "sec-websocket-key";
                    FixedString128Bytes versionHeader = "sec-websocket-version";
                    var invalidRequestLine = (firstLineEnd < 14 || recvHandshake[0] != 'G' || recvHandshake[1] != 'E' || recvHandshake[2] != 'T' || recvHandshake[3] != ' ' ||
                        recvHandshake[firstLineEnd - 9] != ' ' ||
                        recvHandshake[firstLineEnd - 8] != 'H' || recvHandshake[firstLineEnd - 7] != 'T' || recvHandshake[firstLineEnd - 6] != 'T' || recvHandshake[firstLineEnd - 5] != 'P' ||
                        recvHandshake[firstLineEnd - 4] != '/' || recvHandshake[firstLineEnd - 3] != '1' || recvHandshake[firstLineEnd - 2] != '.' || recvHandshake[firstLineEnd - 1] != '1');
                    FixedString512Bytes wsKey;
                    var invalidVersion = (!headerLookup.TryGetValue(versionHeader, out headerValue) || headerValue.Length != 2 ||
                        headerValue[0] != '1' || headerValue[1] != '3');
                    var invalidKey = (!headerLookup.TryGetValue(keyHeader, out wsKey) || wsKey.Length != 24);
                    if (invalidRequestLine || !headerLookup.ContainsKey(hostHeader) || invalidKey ||
                        invalidVersion || invalidConnection || invalidUpgrade)
                    {
                        WarnIf(invalidRequestLine, "Received handshake with invalid http request line.");
                        WarnIf (invalidVersion, "Received handshake with invalid or missing sec-websocket-version key.");
                        WarnIf(invalidConnection, "Received handshake with invalid or missing Connection key.");
                        WarnIf(invalidUpgrade, "Received handshake with invalid or missing Upgrade key.");
                        WarnIf(!headerLookup.ContainsKey(hostHeader), "Received handshake with a missing host key.");
                        WarnIf(invalidKey, "Received handshake with a missing or invalid sec-websocket-key key.");

                        // Not a valid get request
                        handshake = "HTTP/1.1 400 Bad Request\r\nSec-WebSocket-Version: 13\r\n\r\n";
                        if (sendbuffer.Available >= handshake.Length)
                        {
                            fixed(byte* destination = sendbuffer.Data)
                                UnsafeUtility.MemCpy(destination + sendbuffer.Length, handshake.GetUnsafePtr(), handshake.Length);
                            sendbuffer.Length += handshake.Length;
                        }
                        else
                        {
                            Warn("Insufficient send buffer.");
                        }
                           
                        return State.Closed;
                    }
                    // Only / is available
                    if (firstLineEnd != 14 || recvHandshake[4] != '/')
                    {
                        Warn("Received handshake with an incorrect resource name.");

                        handshake = "HTTP/1.1 404 Not Found\r\n\r\n";
                        if (sendbuffer.Available >= handshake.Length)
                        {
                            fixed(byte* destination = sendbuffer.Data)
                                UnsafeUtility.MemCpy(destination + sendbuffer.Length, handshake.GetUnsafePtr(), handshake.Length);
                            sendbuffer.Length += handshake.Length;
                        }
                        else
                        {
                            Warn("Insufficient send buffer.");
                        }

                        return State.Closed;
                    }

                    handshake = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n";
                    wsKey.Append(keyMagic);
                    var hash = new SHA1(new FixedString128Bytes(wsKey));
                    FixedString128Bytes accept = "Sec-WebSocket-Accept: ";
                    handshake.Append(accept);
                    handshake.Append(hash.ToBase64());
                    FixedString32Bytes end = "\r\n";
                    handshake.Append(end);
                    handshake.Append(end);
                    if (sendbuffer.Available >= handshake.Length)
                    {
                        fixed(byte* destination = sendbuffer.Data)
                            UnsafeUtility.MemCpy(destination + sendbuffer.Length, handshake.GetUnsafePtr(), handshake.Length);
                        sendbuffer.Length += handshake.Length;
                    }
                    else
                    {
                        Warn("Insufficient send buffer.");
                        return State.Closed;
                    }
                }

                // At this point tha handshake message has been parsed and can be discarded. Usually the handshake
                // message will be the only thing in the buffer but in theory, it's possible for a server to reply
                // with an HTTP SWITCHING PROTOCOL immediately followed by one or more WebSocket FRAMEs (PING, DATA
                // or CLOSE) which might have been received all together in a single chunk by the underlying layer
                // and would all sit in the recv buffer.
                var pending = recvbuffer.Length - length;
                if (pending > 0)
                    UnsafeUtility.MemMove(data, data + length, pending);
                recvbuffer.Length = pending;
            }
                               
            return State.Open;
        }

        public static unsafe bool Close(ref Buffer buffer, StatusCode status, bool useMask, uint mask)
        {
            var start = buffer.Length;
            var code = (ushort)(BitConverter.IsLittleEndian ? (((ushort)status & 0xFF) << 8) | (((ushort)status >> 8) & 0xFF) : (ushort)status);
            if (Binary(ref buffer, &code, sizeof(ushort), useMask, mask))
            {
                buffer.Data[start] = 0x88;
                return true;
            }
            return false;
        }

        public static unsafe bool Ping(ref Buffer buffer, bool useMask, uint mask)
        {
            var start = buffer.Length;
            if (Binary(ref buffer, (void*)0, 0, useMask, mask))
            {
                buffer.Data[start] = 0x89;
                return true;
            }
            return false;
        }

        public static unsafe bool Pong(ref Buffer buffer, byte* payload, int payloadSize, bool useMask, uint mask)
        {
            var start = buffer.Length;
            if (Binary(ref buffer, payload, payloadSize, useMask, mask))
            {
                buffer.Data[start] = 0x8a;
                return true;
            }
            return false;
        }

        public static unsafe bool Binary(ref PacketProcessor packet, bool useMask, uint mask)
        {
            var destination = (byte*)packet.GetUnsafePayloadPtr();
            var offset = packet.Offset;
            var capacity = packet.Capacity;
            var length = packet.Length;
            int headerLen = Header(destination, offset, capacity, length, useMask, mask);
            if (headerLen > 0)
            {
                destination += offset;
                offset -= headerLen;
                length += headerLen;
                if (useMask)
                {                           
                    var maskBytes = destination - 4;
                    for (int i = 0; i < length; i++)
                        destination[i] ^= maskBytes[i & 3];
                }
                packet.SetUnsafeMetadata(length, offset);
                return true;
            }

            return false;
        }          

        private static unsafe bool Binary(ref Buffer buffer, void* payload, int payloadLen, bool useMask, uint mask)
        {
            var padding = HeaderLen(payloadLen, useMask);
            fixed(byte* ptr = buffer.Data)
            {
                var destination = ptr + buffer.Length;
                var capacity = buffer.Available;
                var headerLen = Header(destination, padding, capacity, payloadLen, useMask, mask);
                if (headerLen <= 0)
                    return false;

                destination += headerLen;
                if (useMask)
                {
                    var maskBytes = destination - 4;
                    for (int i = 0; i < payloadLen; i++)
                        destination[i] = (byte)(((byte*)payload)[i] ^ maskBytes[i & 3]);
                }
                else
                {
                    if (payloadLen > 0)
                        UnsafeUtility.MemCpy(destination, payload, payloadLen);
                }

                buffer.Length += headerLen + payloadLen;
                return true;
            }
        }

        private static int HeaderLen(int payloadLen, bool useMask)
        {
            int totalHeaderLen;
            if (payloadLen < 126)
                totalHeaderLen = 2;
            else if (payloadLen <= 0xffff)
                totalHeaderLen = 4;
            else
                totalHeaderLen = 10;

            if (useMask)
                totalHeaderLen += 4;

            return totalHeaderLen;
        }

        private static unsafe int Header(byte* destination, int padding, int capacity, int payloadLen, bool useMask, uint mask)
        {
            int totalHeaderLen = HeaderLen(payloadLen, useMask);                
            if (capacity < padding || capacity < totalHeaderLen + payloadLen || totalHeaderLen > padding)
                return 0;

            destination += padding - totalHeaderLen;

            // fin + binary
            *destination++ = 0x82;
            (int maskLen, byte maskFlag)  = useMask ? (4, (byte)0x80) : (0, (byte)0);
            if (payloadLen < 126)
            {
                *destination++ = (byte)(maskFlag | payloadLen);
            }
            else if (payloadLen <= 0xffff)
            {
                *destination++ = (byte)(maskFlag | 126);
                *destination++ = (byte)(payloadLen >> 8);
                *destination++ = (byte)(payloadLen & 0xff);
            }
            else
            {
                *destination++ = (byte)(maskFlag | 127);
                *destination++ = (byte)0;
                *destination++ = (byte)0;
                *destination++ = (byte)0;
                *destination++ = (byte)0;
                *destination++ = (byte)((payloadLen >> 24) & 0xff);
                *destination++ = (byte)((payloadLen >> 16) & 0xff);
                *destination++ = (byte)((payloadLen >> 8) & 0xff);
                *destination++ = (byte)(payloadLen & 0xff);
            }

            if (useMask)
            {
                *destination++ = (byte)(mask >> 24);
                *destination++ = (byte)((mask >> 16) & 0xff);
                *destination++ = (byte)((mask >> 8) & 0xff);
                *destination++ = (byte)(mask & 0xff);
            }

            return totalHeaderLen;
        }

        // Per RFC6455 a single WebSocket frame has a maximum size limit of 2^63 bytes and a WebSocket message,
        // made up of more than 1 frame has no limit imposed by the protocol. This layer, however, only supports
        // handshake packets up to the 512 bytes and user payloads up to MTU - MaxHeaderSize(14).

        public const int MaxHeaderSize = 14;

        public unsafe struct Keys
        {
            public fixed uint Key[4];
        }

        public unsafe struct Buffer
        {
            public const int Capacity = 2 * NetworkParameterConstants.AbsoluteMaxMessageSize;

            public fixed byte Data[Capacity];
            public int Length;
            public int Available => Capacity - Length;
        }

        public unsafe struct Payload
        {
            public const int Capacity = NetworkParameterConstants.AbsoluteMaxMessageSize - MaxHeaderSize;

            public fixed byte Data[Capacity];
            public int Length;
            public int Available => Capacity - Length;
        }
                    
        public struct Settings
        {
            public int ConnectTimeoutMS;
            public int DisconnectTimeoutMS;
            public int HeartbeatTimeoutMS;
        }

        unsafe struct SHA1
        {
            private void UpdateABCDE(int i, ref uint a, ref uint b, ref uint c, ref uint d, ref uint e, uint f, uint k)
            {
                var tmp = ((a << 5) | (a >> 27)) + e + f + k + words[i];
                e = d;
                d = c;
                c = (b << 30) | (b >> 2);
                b = a;
                a = tmp;
            }

            private void UpdateHash()
            {
                for (int i = 16; i < 80; ++i)
                {
                    words[i] = (words[i - 3] ^ words[i - 8] ^ words[i - 14] ^ words[i - 16]);
                    words[i] = (words[i] << 1) | (words[i] >> 31);
                }

                var a = h0;
                var b = h1;
                var c = h2;
                var d = h3;
                var e = h4;

                for (int i = 0; i < 20; ++i)
                {
                    var f = (b & c) | ((~b) & d);
                    var k = 0x5a827999u;
                    UpdateABCDE(i, ref a, ref b, ref c, ref d, ref e, f, k);
                }
                for (int i = 20; i < 40; ++i)
                {
                    var f = b ^ c ^ d;
                    var k = 0x6ed9eba1u;
                    UpdateABCDE(i, ref a, ref b, ref c, ref d, ref e, f, k);
                }
                for (int i = 40; i < 60; ++i)
                {
                    var f = (b & c) | (b & d) | (c & d);
                    var k = 0x8f1bbcdcu;
                    UpdateABCDE(i, ref a, ref b, ref c, ref d, ref e, f, k);
                }
                for (int i = 60; i < 80; ++i)
                {
                    var f = b ^ c ^ d;
                    var k = 0xca62c1d6u;
                    UpdateABCDE(i, ref a, ref b, ref c, ref d, ref e, f, k);
                }
                h0 += a;
                h1 += b;
                h2 += c;
                h3 += d;
                h4 += e;
            }

            public SHA1(in FixedString512Bytes str)
            {
                h0 = 0x67452301u;
                h1 = 0xefcdab89u;
                h2 = 0x98badcfeu;
                h3 = 0x10325476u;
                h4 = 0xc3d2e1f0u;
                var bitLen = str.Length << 3;
                var numFullChunks = bitLen >> 9;
                byte* ptr = str.GetUnsafePtr();
                for (int chunk = 0; chunk < numFullChunks; ++chunk)
                {
                    for (int i = 0; i < 16; ++i)
                    {
                        words[i] = (uint)((ptr[0] << 24) | (ptr[1] << 16) | (ptr[2] << 8) | ptr[3]);
                        ptr += 4;
                    }
                    UpdateHash();
                }
                var remainingBits = (bitLen & 0x1ff);
                var remainingBytes = (remainingBits >> 3);
                var fullWords = (remainingBytes >> 2);
                for (int i = 0; i < fullWords; ++i)
                {
                    words[i] = (uint)((ptr[0] << 24) | (ptr[1] << 16) | (ptr[2] << 8) | ptr[3]);
                    ptr += 4;
                }
                var fullBytes = remainingBytes & 3;
                switch (fullBytes)
                {
                    case 3:
                        words[fullWords] = (uint)((ptr[0] << 24) | (ptr[1] << 16) | (ptr[2] << 8) | 0x80u);
                        ptr += 3;
                        break;
                    case 2:
                        words[fullWords] = (uint)((ptr[0] << 24) | (ptr[1] << 16) | (0x80u << 8));
                        ptr += 2;
                        break;
                    case 1:
                        words[fullWords] = (uint)((ptr[0] << 24) | (0x80u << 16));
                        ptr += 1;
                        break;
                    case 0:
                        words[fullWords] = (uint)((0x80u << 24));
                        break;
                }
                ++fullWords;
                if (remainingBits >= 448)
                {
                    // Needs two chunks, one for the remaining bits and one for size
                    for (int i = fullWords; i < 16; ++i)
                        words[i] = 0;
                    UpdateHash();
                    for (int i = 0; i < 15; ++i)
                        words[i] = 0;
                    words[15] = (uint)bitLen;
                    UpdateHash();
                }
                else
                {
                    for (int i = fullWords; i < 15; ++i)
                        words[i] = 0;
                    words[15] = (uint)bitLen;
                    UpdateHash();
                }
            }

            public FixedString32Bytes ToBase64()
            {
                FixedString32Bytes base64 = default;
                AppendBase64(ref base64, (byte)(h0 >> 24), (byte)(h0 >> 16), (byte)(h0 >> 8));
                AppendBase64(ref base64, (byte)(h0), (byte)(h1 >> 24), (byte)(h1 >> 16));
                AppendBase64(ref base64, (byte)(h1 >> 8), (byte)(h1), (byte)(h2 >> 24));
                AppendBase64(ref base64, (byte)(h2 >> 16), (byte)(h2 >> 8), (byte)(h2));
                AppendBase64(ref base64, (byte)(h3 >> 24), (byte)(h3 >> 16), (byte)(h3 >> 8));
                AppendBase64(ref base64, (byte)(h3), (byte)(h4 >> 24), (byte)(h4 >> 16));
                AppendBase64(ref base64, (byte)(h4 >> 8), (byte)(h4));
                return base64;
            }

            private fixed uint words[80];
            private uint h0;
            private uint h1;
            private uint h2;
            private uint h3;
            private uint h4;
        }           

        static unsafe void GenerateBase64Key(out FixedString32Bytes key, ref Keys keys)
        {
            key = default;
            AppendBase64(ref key, (byte)(keys.Key[0] >> 24), (byte)(keys.Key[0] >> 16), (byte)(keys.Key[0] >> 8));
            AppendBase64(ref key, (byte)(keys.Key[0]),       (byte)(keys.Key[1] >> 24), (byte)(keys.Key[1] >> 16));
            AppendBase64(ref key, (byte)(keys.Key[1] >> 8),  (byte)(keys.Key[1]),       (byte)(keys.Key[2] >> 24));
            AppendBase64(ref key, (byte)(keys.Key[2] >> 16), (byte)(keys.Key[2] >> 8),  (byte)(keys.Key[2]));
            AppendBase64(ref key, (byte)(keys.Key[3] >> 24), (byte)(keys.Key[3] >> 16), (byte)(keys.Key[3] >> 8));
            AppendBase64(ref key, (byte)(keys.Key[3]));
        }

        static void AppendBase64(ref FixedString32Bytes base64, byte b0, byte b1, byte b2)
        {
            var c1 = ApplyTable((byte)(b0 >> 2));
            var c2 = ApplyTable((byte)(((b0 & 3) << 4) | (b1 >> 4)));
            var c3 = ApplyTable((byte)(((b1 & 0xf) << 2) | (b2 >> 6)));
            var c4 = ApplyTable((byte)(b2 & 0x3f));
            base64.Add(c1);
            base64.Add(c2);
            base64.Add(c3);
            base64.Add(c4);
        }

        static void AppendBase64(ref FixedString32Bytes base64, byte b0, byte b1)
        {
            var c1 = ApplyTable((byte)(b0 >> 2));
            var c2 = ApplyTable((byte)(((b0 & 3) << 4) | (b1 >> 4)));
            var c3 = ApplyTable((byte)((b1 & 0xf) << 2));

            base64.Add(c1);
            base64.Add(c2);
            base64.Add(c3);
            base64.Add((byte)'=');
        }

        static void AppendBase64(ref FixedString32Bytes base64, byte b0)
        {
            var c1 = ApplyTable((byte)(b0 >> 2));
            var c2 = ApplyTable((byte)((b0 & 3) << 4));

            base64.Add(c1);
            base64.Add(c2);
            base64.Add((byte)'=');
            base64.Add((byte)'=');
        }

        static byte ApplyTable(byte val)
        {
            if (val < 26)
                return (byte)(val + 'A');
            else if (val < 52)
                return (byte)(val + 'a' - 26);
            else if (val < 62)
                return (byte)(val + '0' - 52);
            else if (val == 62)
                return (byte)'+';
            return (byte)'/';
        }            
    }
}
#endif
