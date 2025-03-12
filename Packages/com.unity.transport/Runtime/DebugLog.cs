using System.Runtime.CompilerServices;
using Unity.Baselib.LowLevel;
using Unity.Collections;
using Unity.Networking.Transport.Relay;

namespace Unity.Networking.Transport.Logging
{
    internal static class DebugLog
    {
        private static unsafe FixedString512Bytes GetBaselibErrorMessage(Binding.Baselib_ErrorState error)
        {
            FixedString512Bytes errorMessage = new FixedString512Bytes();
            errorMessage.Length = (int)Binding.Baselib_ErrorState_Explain(&error, errorMessage.GetUnsafePtr(), (uint)errorMessage.Capacity, Binding.Baselib_ErrorState_ExplainVerbosity.ErrorType_SourceLocation_Explanation);

            return errorMessage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ErrorResetNotEmptyEventQueue(int connCount, int connId, long listenState)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Resetting event queue with pending events (Count={ConnectionsCount}, ConnectionID={ConnectionId}) Listening: {ListeningState}", connCount, connId, listenState);
#else
            UnityEngine.Debug.LogError($"Resetting event queue with pending events (Count={connCount}, ConnectionID={connId}) Listening: {listenState}");
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Log(string message)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Info(message);
#else
            UnityEngine.Debug.Log(message);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Log(FixedString512Bytes message)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Info(message);
#else
            UnityEngine.Debug.Log(message);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogWarning(string message)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning(message);
#else
            UnityEngine.Debug.LogWarning(message);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogError(string message)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error(message);
#else
            UnityEngine.Debug.LogError(message);
#endif
        }

        public static void ErrorDTLSHandshakeFailed(uint handshakeStep)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("DTLS handshake failed at step {HandshakeStep}. Closing connection.", handshakeStep);
#else
            UnityEngine.Debug.LogError($"DTLS handshake failed at step {handshakeStep}. Closing connection.");
#endif
        }

        public static void ErrorDTLSEncryptFailed(uint result)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Failed to encrypt packet (error: {ResultCode}). Likely internal DTLS failure. Closing connection.", result);
#else
            UnityEngine.Debug.LogError($"Failed to encrypt packet (error: {result}). Likely internal DTLS failure. Closing connection.");
#endif
        }

        public static void ReceivedMessageWasNotProcessed(SimpleConnectionLayer.MessageType messageType)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("Received message with type {MessageType} was not processed", (byte)messageType);
#else
            UnityEngine.Debug.LogWarning(string.Format("Received message with type {0} was not processed", (byte)messageType));
#endif
        }

        public static void ProtocolMismatch(byte localVersion, byte remoteVersion)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("Simple Connection Protocol version mismatch. This could happen if remote connection UTP version is different. (local: {LocalVersion}, remote: {RemoteVersion})", localVersion, remoteVersion);
#else
            UnityEngine.Debug.LogWarning(
                string.Format("Simple Connection Protocol version mismatch. This could happen if remote connection UTP version is different. (local: {0}, remote: {1})",
                    localVersion, remoteVersion));
#endif
        }

        public static void ErrorTLSDecryptFailed(uint result)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Failed to decrypt packet (error: {ResultCode}). Likely internal TLS failure. Closing connection.", result);
#else
            UnityEngine.Debug.LogError($"Failed to decrypt packet (error: {result}). Likely internal TLS failure. Closing connection.");
#endif
        }

        public static void ErrorTLSEncryptFailed(uint result)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Failed to encrypt packet (error: {ResultCode}). Likely internal TLS failure. Closing connection.", result);
#else
            UnityEngine.Debug.LogError($"Failed to encrypt packet (error: {result}). Likely internal TLS failure. Closing connection.");
#endif
        }

        public static void ErrorTLSHandshakeFailed(uint handshakeStep)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("TLS handshake failed at step {HandshakeStep}. Closing connection.", handshakeStep);
#else
            UnityEngine.Debug.LogError($"TLS handshake failed at step {handshakeStep}. Closing connection.");
#endif
        }

        public static void ReceiveQueueIsFull(int receiveQueueCapacity)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("Receive queue is full, some packets could be dropped, consider increase its size ({Capacity}).", receiveQueueCapacity);
#else
            UnityEngine.Debug.LogWarning($"Receive queue is full, some packets could be dropped, consider increase its size ({receiveQueueCapacity}).");
#endif
        }

        public static void ErrorRelay(byte errorCode)
        {
            switch (errorCode)
            {
                case 0:
                    LogError("Received error message from Relay: invalid protocol version. Make sure your Unity Transport package is up to date.");
                    break;
                case 1:
                    LogError("Received error message from Relay: player timed out due to inactivity.");
                    break;
                case 2:
                    LogError("Received error message from Relay: unauthorized.");
                    break;
                case 3:
                    LogError("Received error message from Relay: allocation ID client mismatch.");
                    break;
                case 4:
                    LogError("Received error message from Relay: allocation ID not found.");
                    break;
                case 5:
                    LogError("Received error message from Relay: not connected.");
                    break;
                case 6:
                    LogError("Received error message from Relay: self-connect not allowed.");
                    break;
                default:
#if USE_UNITY_LOGGING
                    Unity.Logging.Log.Error("Received error message from Relay with unknown error code {ErrorCode}", errorCode);
#else
                    UnityEngine.Debug.LogError($"Received error message from Relay with unknown error code {errorCode}");
#endif
                    break;
            }

            if (errorCode == 1 || errorCode == 4)
            {
                LogError("Relay allocation is invalid. See NetworkDriver.GetRelayConnectionStatus and RelayConnectionStatus.AllocationInvalid for details on how to handle this situation.");
            }
        }

        public static void ErrorBaselib(FixedString128Bytes description, Binding.Baselib_ErrorState error)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Baselib operation failed. '{Description}' (error {ErrorCode}: {ErrorMessage})", description, (int)error.code, GetBaselibErrorMessage(error));
#else
            UnityEngine.Debug.LogError($"Baselib operation failed. '{description}' (error {error}: {GetBaselibErrorMessage(error)})");
#endif
        }

        public static void ErrorBaselibBind(Binding.Baselib_ErrorState error, ushort port)
        {
            FixedString128Bytes extraExplain = error.code == Binding.Baselib_ErrorCode.AddressInUse
                ? FixedString.Format(" This is likely due to another process listening on port {0}.", port) : "";

#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Socket creation failed (error {ErrorCode}: {ErrorMessage}).{ExtraExplain}", (int)error.code, GetBaselibErrorMessage(error), extraExplain);
#else
            UnityEngine.Debug.LogError($"Socket creation failed (error {error}: {GetBaselibErrorMessage(error)}).{extraExplain}");
#endif
        }

        public static void BaselibFailedToSendPackets(int failedCount)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("Baselib failed to send {FailedCount} packets", failedCount);
#else
            UnityEngine.Debug.LogWarning(string.Format("Baselib failed to send {0} packets", failedCount));
#endif
        }

        public static void ErrorCreateQueueWrongSendSize(int requiredSize, uint sendBufferSize)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("The required buffer size ({RequiredSize}) does not fit in the allocated send buffers ({ReceiveBufferSize})", requiredSize, sendBufferSize);
#else
            UnityEngine.Debug.LogError($"The required buffer size ({requiredSize}) does not fit in the allocated send buffers ({sendBufferSize})");
#endif
        }

        public static void ErrorCreateQueueWrongReceiveSize(int requiredSize, uint receiveBufferSize)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("The required buffer size ({RequiredSize}) does not fit in the allocated receive buffers ({ReceiveBufferSize})", requiredSize, receiveBufferSize);
#else
            UnityEngine.Debug.LogError($"The required buffer size ({requiredSize}) does not fit in the allocated receive buffers ({receiveBufferSize})");
#endif
        }

        public static void ErrorCopyPayloadFailure(int payloadSize, int size)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("The payload size ({PayloadSize}) does not fit in the provided pointer ({ProvidedSize})", payloadSize, size);
#else
            UnityEngine.Debug.LogError($"The payload size ({payloadSize}) does not fit in the provided pointer ({size})");
#endif
        }

        public static void ErrorPayloadWrongSize(int bufferSize, int payloadSize)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("The size of the buffer ({BufferSize}) is larger than the payload ({PayloadSize}).", bufferSize, payloadSize);
#else
            UnityEngine.Debug.LogError($"The size of the buffer ({bufferSize}) is larger than the payload ({payloadSize}).");
#endif
        }

        public static void ErrorPayloadNotFitSize(int bufferSize, int payloadSize)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("The size of the required type ({BufferSize}) does not fit in the payload ({PayloadSize}).", bufferSize, payloadSize);
#else
            UnityEngine.Debug.LogError($"The size of the required type ({bufferSize}) does not fit in the payload ({payloadSize}).");
#endif
        }

        public static void ErrorPayloadNotFitStartSize(int bufferSize, int payloadSize)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("The requested data size ({BufferSize}) does not fit at the start of the payload ({PayloadSize} Bytes available).", bufferSize, payloadSize);
#else
            UnityEngine.Debug.LogError($"The requested data size ({bufferSize}) does not fit at the start of the payload ({payloadSize} Bytes available).");
#endif
        }

        public static void ErrorPayloadNotFitEndSize(int bufferSize, int payloadSize)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("The requested data size ({BufferSize}) does not fit at the end of the payload ({PayloadSize} Bytes available).", bufferSize, payloadSize);
#else
            UnityEngine.Debug.LogError($"The requested data size ({bufferSize}) does not fit at the end of the payload ({payloadSize} Bytes available).");
#endif
        }

        public static void ErrorOperation(ref FixedString64Bytes label, int errorCode)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Error on {Label}, errorCode = {ErrorCode}", label, errorCode);
#else
            UnityEngine.Debug.LogError(FixedString.Format("Error on {0}, errorCode = {1}", label, errorCode));
#endif
        }

        public static void ErrorFragmentationMaxPayloadTooLarge(int payloadCapacity, int maxCapacity)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("PayloadCapacity value ({PayloadCapacity}) can't be greater than {MaxCapacity}", payloadCapacity, maxCapacity);
#else
            UnityEngine.Debug.LogError($"PayloadCapacity value ({payloadCapacity}) can't be greater than {maxCapacity}");
#endif
        }

        public static void SimulatorIncomingTooLarge(int incomingSize, int maxPacketSize)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("Incoming packet too large for SimulatorPipeline internal storage buffer. Passing through. [buffer={Buffer} MaxPacketSize={MaxPacketSize}]", incomingSize, maxPacketSize);
#else
            UnityEngine.Debug.LogWarning($"Incoming packet too large for SimulatorPipeline internal storage buffer. Passing through. [buffer={incomingSize} MaxPacketSize={maxPacketSize}]");
#endif
        }

        public static void SimulatorNoSpace(int maxPacketSize)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("Simulator has no space left in the delayed packets queue ({maxPacketSize} packets already in queue). Letting packet go through. Increase MaxPacketCount during driver construction.", maxPacketSize);
#else
            UnityEngine.Debug.LogWarning($"Simulator has no space left in the delayed packets queue ({maxPacketSize} packets already in queue). Letting packet go through. Increase MaxPacketCount during driver construction.");
#endif
        }

        public static void ErrorRelayWrongBufferSize(int expectedSize, int actualSize)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Provided byte array length is invalid, must be {ExpectedSize} but got {ActualSize}.", expectedSize, actualSize);
#else
            UnityEngine.Debug.LogError($"Provided byte array length is invalid, must be {expectedSize} but got {actualSize}.");
#endif
        }

        public static void ErrorRelayWrongBufferSizeLess(int expectedSize, int actualSize)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Provided byte array length is invalid, must be less or equal to {ExpectedSize} but got {ActualSize}.", expectedSize, actualSize);
#else
            UnityEngine.Debug.LogError($"Provided byte array length is invalid, must be less or equal to {expectedSize} but got {actualSize}.");
#endif
        }

        public static void ErrorRelayMapHostFailure(string host)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Couldn't map hostname {Host} to an IP address.", host);
#else
            UnityEngine.Debug.LogError($"Couldn't map hostname {host} to an IP address.");
#endif
        }

        public static void DriverTooManyUpdates(int updateCount)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("A lot of pipeline updates have been queued, possibly too many being scheduled in pipeline logic, queue count: {UpdateCount}", updateCount);
#else
            UnityEngine.Debug.LogWarning(FixedString.Format("A lot of pipeline updates have been queued, possibly too many being scheduled in pipeline logic, queue count: {0}", updateCount));
#endif
        }

        public static void ErrorRelayServerDataEndpoint(NetworkEndpoint serverDataEndpoint)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("ServerData.Endpoint value ({ServerDataEndpoint}) must be a valid value", serverDataEndpoint);
#else
            UnityEngine.Debug.LogError($"ServerData.Endpoint value ({serverDataEndpoint}) must be a valid value");
#endif
        }

        public static void ErrorRelayServerDataAllocationId(RelayAllocationId serverDataAllocationId)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("ServerData.AllocationId must be a valid value");
#else
            UnityEngine.Debug.LogError($"ServerData.AllocationId value ({serverDataAllocationId}) must be a valid value");
#endif
        }

        public static void ErrorValueIsNegative(FixedString64Bytes name, int value)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("{ValueName} value ({Value}) must be greater or equal to 0", name, value);
#else
            UnityEngine.Debug.LogError($"{name} value ({value}) must be greater or equal to 0");
#endif
        }

        public static void ErrorValueIsZeroOrNegative(FixedString64Bytes name, int value)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("{ValueName} value ({Value}) must be greater than 0", name, value);
#else
            UnityEngine.Debug.LogError($"{name} value ({value}) must be greater than 0");
#endif
        }

        public static void ErrorValueIsNotInRange(FixedString64Bytes name, int value, int min, int max)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("{ValueName} value ({Value}) must be greater than {Min} and less than or equal to {Max}", name, value, min, max);
#else
            UnityEngine.Debug.LogError($"{name} value ({value}) must be greater than {min} and less than or equal to {max}");
#endif
        }

        public static void WarningMaxMessageSizeTooSmall(FixedString64Bytes name, int value)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("{ValueName} value ({Value}) is unnecessarily low. 548 should be safe in all circumstances.", name, value);
#else
            UnityEngine.Debug.LogWarning($"{name} value ({value}) is unnecessarily low. 548 should be safe in all circumstances.");
#endif
        }

        public static void ErrorTLSInvalidOffset(int packetOffset, int packetPadding)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Invalid offset in packet processor ({Offset}, should be >={Padding}).", packetOffset, packetPadding);
#else
            UnityEngine.Debug.LogError($"Invalid offset in packet processor ({packetOffset}, should be >={packetPadding}).");
#endif
        }

        public static void ConnectionCompletingWrongState(NetworkConnection.State connectionDataState)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("Attempting to complete a connection with state '{State}'", (byte)connectionDataState);
#else
            UnityEngine.Debug.LogWarning(string.Format("Attempting to complete a connection with state '{0}'", connectionDataState));
#endif
        }

        public static void ConnectionAcceptWrongState(ConnectionId connectionId, NetworkConnection.State connectionState)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("Attempting to accept a connection ({ConnectionId}) with state '{State}'", connectionId, (byte)connectionState);
#else
            UnityEngine.Debug.LogWarning(string.Format("Attempting to accept a connection ({0}) with state '{1}'", connectionId, connectionState));
#endif
        }

        public static void ConnectionFinishWrongState(NetworkConnection.State connectionDataState)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("Attempting to complete a disconnection with state different to Disconnecting ({State})", (byte)connectionDataState);
#else
            UnityEngine.Debug.LogWarning(string.Format("Attempting to complete a disconnection with state different to Disconnecting ({0})", connectionDataState));
#endif
        }

        public static void PipelineCompleteSendFailed(int retval)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("CompleteSend failed with the following error code: {ErrorCode}", retval);
#else
            UnityEngine.Debug.LogWarning(FixedString.Format("CompleteSend failed with the following error code: {0}", retval));
#endif
        }

        public static void PipelineEndSendFailed(int retval)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("An error occurred during EndSend. ErrorCode: {ErrorCode}", retval);
#else
            UnityEngine.Debug.LogWarning(FixedString.Format("An error occurred during EndSend. ErrorCode: {0}", retval));
#endif
        }

        public static void PipelineProcessSendFailed(int result)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Warning("ProcessPipelineSend failed with the following error code {ErrorCode}.", result);
#else
            UnityEngine.Debug.LogWarning(FixedString.Format("ProcessPipelineSend failed with the following error code {0}.", result));
#endif
        }

        public static void ErrorPipelineReceiveInvalid(byte pipelineId, int pipelinesLength)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Received a packet with an invalid pipeline ({PipelineId}, should be between 1 and {PipelinesCount}). Possible mismatch between pipeline definitions on each end of the connection.", pipelineId, pipelinesLength);
#else
            UnityEngine.Debug.LogError($"Received a packet with an invalid pipeline ({pipelineId}, should be between 1 and {pipelinesLength}). Possible mismatch between pipeline definitions on each end of the connection.");
#endif
        }

        public static void ErrorParameterIsNotValid(string name)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("The provided network parameter ({ParamName}) is not valid", name);
#else
            UnityEngine.Debug.LogError($"The provided network parameter ({name}) is not valid");
#endif
        }

        public static void ErrorStackInitFailure(string layerTypeName, int errorCode)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("Failed to initialize the NetworkStack. Layer {LayerTypeName} with error Code: {ErrorCode}.", layerTypeName, errorCode);
#else
            UnityEngine.Debug.LogError($"Failed to initialize the NetworkStack. Layer {layerTypeName} with error Code: {errorCode}.");
#endif
        }

        public static void ErrorStackSendCreateWrongBufferCount(int providedCapacity, int expectedCapacity)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("The provided buffers count ({ProvidedCapacity}) must be equal to the sendQueueCapacity ({SendQueueCapacity})", providedCapacity, expectedCapacity);
#else
            UnityEngine.Debug.LogError(string.Format(
                "The provided buffers count ({0}) must be equal to the sendQueueCapacity ({1})",
                providedCapacity,
                expectedCapacity));
#endif
        }

        public static void ErrorStackReceiveCreateWrongBufferCount(int providedCapacity, int expectedCapacity)
        {
#if USE_UNITY_LOGGING
            Unity.Logging.Log.Error("The provided buffers count ({ProvidedCapacity}) must be equal to the receiveQueueCapacity ({ReceiveQueueCapacity})", providedCapacity, expectedCapacity);
#else
            UnityEngine.Debug.LogError(string.Format(
                "The provided buffers count ({0}) must be equal to the receiveQueueCapacity ({1})",
                providedCapacity,
                expectedCapacity));
#endif
        }
    }
}
