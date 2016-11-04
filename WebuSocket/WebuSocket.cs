using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WebuSocketCore {
    public enum SocketState {
		CONNECTING,
		TLS_HANDSHAKING,
		WS_HANDSHAKING,
		OPENED,
		CLOSING,
		CLOSED
	}

	public enum WebuSocketCloseEnum {
		CLOSED_FORCELY,
		CLOSED_GRACEFULLY,
		CLOSED_WHILE_RECEIVING,
		CLOSED_BY_SERVER
	}

	public enum WebuSocketErrorEnum {
		UNKNOWN_ERROR,
		CONNECTION_FAILED,
		TLS_HANDSHAKE_FAILED,
		TLS_ERROR,
		WS_HANDSHAKE_KEY_UNMATCHED,
		SEND_FAILED,
		RECEIVE_FAILED,
        CONNECTING,
        ALREADY_DISCONNECTED,
    }
	
	public class WebuSocket {
		private readonly EndPoint endPoint;
		
		private SocketToken socketToken;
		
		public string webSocketConnectionId;
		
		public class SocketToken {
			public SocketState socketState;
			public readonly Socket socket;
			
			public byte[] receiveBuffer;// このバッファを切りはりしたくない = array コピーを発生させたくないが為に、もう一個バッファが生まれる。
			
			public readonly SocketAsyncEventArgs connectArgs;
			public readonly SocketAsyncEventArgs sendArgs;
			public readonly SocketAsyncEventArgs receiveArgs;
			
			public SocketToken (Socket socket, int bufferLen, SocketAsyncEventArgs connectArgs, SocketAsyncEventArgs sendArgs, SocketAsyncEventArgs receiveArgs) {
				this.socket = socket;
				
				this.receiveBuffer = new byte[bufferLen];
				
				this.connectArgs = connectArgs;
				this.sendArgs = sendArgs;
				this.receiveArgs = receiveArgs;
				
				this.connectArgs.UserToken = this;
				this.sendArgs.UserToken = this;
				this.receiveArgs.UserToken = this;
				
				this.receiveArgs.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
			}
		}
		
		private readonly int baseReceiveBufferSize;
		
		private readonly Action OnConnected;
		private readonly Action OnPinged;
		private readonly Action<Queue<ArraySegment<byte>>> OnMessage;
		private readonly Action<WebuSocketCloseEnum> OnClosed;
		private readonly Action<WebuSocketErrorEnum, Exception> OnError;
		
		private readonly string base64Key;
		private readonly byte[] websocketHandshakeRequestBytes;

		public WebuSocket (
			string url,
			int baseReceiveBufferSize,
			Action OnConnected=null,
			Action<Queue<ArraySegment<byte>>> OnMessage=null,
			Action OnPinged=null,
			Action<WebuSocketCloseEnum> OnClosed=null,
			Action<WebuSocketErrorEnum, Exception> OnError=null,
			Dictionary<string, string> additionalHeaderParams=null
		) {
			this.webSocketConnectionId = Guid.NewGuid().ToString();
			this.baseReceiveBufferSize = baseReceiveBufferSize;
			
			this.base64Key = WebSocketByteGenerator.GeneratePrivateBase64Key();
			
			var requstBytesAndHostAndPort = GenerateRequestData(url, additionalHeaderParams, base64Key);
			websocketHandshakeRequestBytes = requstBytesAndHostAndPort.requestDataBytes;

			this.endPoint = new IPEndPoint(IPAddress.Parse(requstBytesAndHostAndPort.host), requstBytesAndHostAndPort.port);
			
			this.OnConnected = OnConnected;
			this.OnMessage = OnMessage;
			this.OnPinged = OnPinged;
			this.OnClosed = OnClosed;
			this.OnError = OnError;
			
			StartConnectAsync();
		}
		
		
		private const string CRLF = "\r\n";
		private const string WEBSOCKET_VERSION = "13"; 
		private bool isWss;
		private Encryption.WebuSocketTlsClientProtocol tlsClientProtocol;

		private void TLSHandshakeDone () {
			switch (socketToken.socketState) {
				case SocketState.TLS_HANDSHAKING: {
					SendWSHandshake();
					break;
				}
				default: {
					if (OnError != null) {
						var error = new Exception("tls handshake failed in unexpected state.");
						OnError(WebuSocketErrorEnum.TLS_HANDSHAKE_FAILED, error);
					}
					Disconnect();
					break;
				}
			}
		}

		private void TLSHandleError (Exception e, string errorMessage) {
			if (OnError != null) {
				if (e == null) e = new Exception("tls error:" + errorMessage);
				OnError(WebuSocketErrorEnum.TLS_ERROR, e);
			}
			Disconnect();
		}

		private RequestDataBytesAndHostAndPort GenerateRequestData (string urlSource, Dictionary<string, string> additionalHeaderParams, string base64Key) {
			var uri = new Uri(urlSource);
			
			var method = "GET";
			var host = uri.Host;
			var schm = uri.Scheme;
			var port = uri.Port;
			
			if (schm == "wss") {
				isWss = true;
				tlsClientProtocol = new Encryption.WebuSocketTlsClientProtocol();
				tlsClientProtocol.Connect(new Encryption.WebuSocketTlsClient(TLSHandshakeDone, TLSHandleError));
			}

			var ip = string.Empty;
			
			var ipPartsCandidate = host.Split('.');
			var isIp = true;
			foreach (var part in ipPartsCandidate) {
				double retNum;
				isIp = double.TryParse(part, out retNum);
			}
			if (!isIp) {
				var ipCandidates = Dns.GetHostAddresses(host);
				if (ipCandidates.Length == 0) {
					throw new Exception("unavailable url:" + urlSource);
				}

				ip = ipCandidates[0].ToString();
				// Debug.LogError("ip:" + ip);
				// Debug.LogError("まだ途中。ipなのかdomainなのか見極めて云々したい。まーipだと思ってぶちこんでエラーで云々でもいいんだけど、、");
			} else {
				ip = host;
			}

			var hostParam = uri.Authority;
			if (
				(port == 80 && schm == "ws") || 
				(port == 443 && schm == "wss")
			) {
				hostParam = uri.DnsSafeHost; 
			}
			
			
			var requestHeaderParams = new Dictionary<string, string>{
				{"Host", hostParam},
				{"Upgrade", "websocket"},
				{"Connection", "Upgrade"},
				{"Sec-WebSocket-Key", base64Key},
				{"Sec-WebSocket-Version", WEBSOCKET_VERSION}
			};

			if (additionalHeaderParams != null) { 
				foreach (var key in additionalHeaderParams.Keys) requestHeaderParams[key] = additionalHeaderParams[key];
			}
			
			/*
				construct request bytes data.
			*/
			var requestData = new StringBuilder();
			
			requestData.AppendFormat("{0} {1} HTTP/{2}{3}", method, uri, "1.1", CRLF);

			foreach (var key in requestHeaderParams.Keys) requestData.AppendFormat("{0}: {1}{2}", key, requestHeaderParams[key], CRLF);

			requestData.Append(CRLF);

			var entity = string.Empty;
			requestData.Append(entity);
			
			var reqBytes = Encoding.UTF8.GetBytes(requestData.ToString().ToCharArray()); 
			return new RequestDataBytesAndHostAndPort(ip, port, reqBytes);
		}
		
		
		public struct RequestDataBytesAndHostAndPort {
			public string host;
			public int port;
			public byte[] requestDataBytes;
			
			public RequestDataBytesAndHostAndPort (string host, int port, byte[] requestDataBytes) {
				this.host = host;
				this.port = port;
				this.requestDataBytes = requestDataBytes;
			}
		}
		
		
		private void StartConnectAsync () {
			var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			clientSocket.NoDelay = true;
			
			var connectArgs = new SocketAsyncEventArgs();
			connectArgs.AcceptSocket = clientSocket;
			connectArgs.RemoteEndPoint = endPoint;
			connectArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnConnect);
			
			var sendArgs = new SocketAsyncEventArgs();
			sendArgs.AcceptSocket = clientSocket;
			sendArgs.RemoteEndPoint = endPoint;
			sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSend);
			
			var receiveArgs = new SocketAsyncEventArgs();
			receiveArgs.AcceptSocket = clientSocket;
			receiveArgs.RemoteEndPoint = endPoint;
			receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnReceived);
						
			socketToken = new SocketToken(clientSocket, baseReceiveBufferSize, connectArgs, sendArgs, receiveArgs); 
			socketToken.socketState = SocketState.CONNECTING;
			
			// start connect.
			if (!clientSocket.ConnectAsync(socketToken.connectArgs)) OnConnect(clientSocket, connectArgs);
		}
		private byte[] webSocketHandshakeResult;
		private void OnConnect (object unused, SocketAsyncEventArgs args) {
			var token = (SocketToken)args.UserToken;

			{
				switch (token.socketState) {
					case SocketState.CONNECTING: {
						if (args.SocketError != SocketError.Success) {
							token.socketState = SocketState.CLOSED;
							
							if (OnError != null) {
								var error = new Exception("connect error:" + args.SocketError.ToString());
								OnError(WebuSocketErrorEnum.CONNECTION_FAILED, error);
							}
							return;
						}

						// ready receive.
						ReadyReceivingNewData(token);

						if (isWss) {
							token.socketState = SocketState.TLS_HANDSHAKING;
							
							// first, send clientHello to server.
							// get ClientHello byte data from tlsClientProtocol instance and send it to server.
							var buffer = new byte[tlsClientProtocol.GetAvailableOutputBytes()];
							tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);
							
							token.sendArgs.SetBuffer(buffer, 0, buffer.Length);
							if (!token.socket.SendAsync(token.sendArgs)) OnSend(token.socket, token.sendArgs);
							return;
						}
						
						SendWSHandshake();
						return;
					}
					default: {
						// unexpected error, should fall this connection.
						if (OnError != null) {
							var error = new Exception("unexpcted connection state error.");
							OnError(WebuSocketErrorEnum.CONNECTION_FAILED, error);
						}
						Disconnect();
						return;
					}
				}
			}
		}

		private void SendWSHandshake () {
			socketToken.socketState = SocketState.WS_HANDSHAKING;
			
			if (isWss) {
				tlsClientProtocol.OfferOutput(websocketHandshakeRequestBytes, 0, websocketHandshakeRequestBytes.Length);
				
				var count = tlsClientProtocol.GetAvailableOutputBytes();
				var buffer = new byte[count];
				tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);

				socketToken.sendArgs.SetBuffer(buffer, 0, buffer.Length);
			} else {
				socketToken.sendArgs.SetBuffer(websocketHandshakeRequestBytes, 0, websocketHandshakeRequestBytes.Length);
			}
			
			if (!socketToken.socket.SendAsync(socketToken.sendArgs)) OnSend(socketToken.socket, socketToken.sendArgs);
		}

		private void OnDisconnected (object unused, SocketAsyncEventArgs args) {
			var token = (SocketToken)args.UserToken;
			switch (token.socketState) {
				case SocketState.CLOSED: {
					// do nothing.
					break;
				}
				default: {
					lock (lockObj) {
						token.socketState = SocketState.CLOSED;

						try {
							token.socket.Close();
						} catch {
							// do nothing.
						}

						if (OnClosed != null) OnClosed(WebuSocketCloseEnum.CLOSED_GRACEFULLY);
					} 
					break;
				}
			}
		}
		
		private void OnSend (object unused, SocketAsyncEventArgs args) {
			var socketError = args.SocketError;
			switch (socketError) {
				case SocketError.Success: {
					// do nothing.
					break;
				}
				default: {
					if (OnError != null) {
						var error = new Exception("send error:" + socketError.ToString());
						OnError(WebuSocketErrorEnum.SEND_FAILED, error);
					}
					Disconnect();
					break;
				}
			}
		}

		private object lockObj = new object();
		private byte[] wsBuffer;
		private int wsBufIndex;
		private int wsBufLength;


		private void OnReceived (object unused, SocketAsyncEventArgs args) {
			var token = (SocketToken)args.UserToken;
			
			if (args.SocketError != SocketError.Success) {
				lock (lockObj) { 
					switch (token.socketState) {
						case SocketState.CLOSING:
						case SocketState.CLOSED: {
							// already closing, ignore.
							return;
						}
						default: {
							// show error, then close or continue receiving.
							if (OnError != null) {
								var error = new Exception("receive error:" + args.SocketError.ToString() + " size:" + args.BytesTransferred);
								OnError(WebuSocketErrorEnum.RECEIVE_FAILED, error);
							}
							Disconnect();
							return;
						}
					}
				}
			}
			
			if (args.BytesTransferred == 0) {
				if (OnError != null) {
					var error = new Exception("failed to receive. args.BytesTransferred = 0." + " args.SocketError:" + args.SocketError);
					OnError(WebuSocketErrorEnum.RECEIVE_FAILED, error);
				}
				Disconnect();
				return;
			}
			
			switch (token.socketState) {
				case SocketState.TLS_HANDSHAKING: {
					var responseFromServer = new byte[args.BytesTransferred];
					Buffer.BlockCopy(args.Buffer, 0, responseFromServer, 0, responseFromServer.Length);
					
					// set received data to tlsClientProtocol by "OfferInput" method.
					// tls handshake phase will progress.
					tlsClientProtocol.OfferInput(responseFromServer);

					// and next handshake data can be get from tlsClientProtocol.
					var buffer = new byte[tlsClientProtocol.GetAvailableOutputBytes()];
					tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);

					// ready receive next data.
					ReadyReceivingNewData(token);
						
					// send.
					token.sendArgs.SetBuffer(buffer, 0, buffer.Length);
					if (!token.socket.SendAsync(token.sendArgs)) OnSend(token.socket, token.sendArgs);
					return;
				}
				case SocketState.WS_HANDSHAKING: {
					var receivedData = new byte[args.BytesTransferred];
					Buffer.BlockCopy(args.Buffer, 0, receivedData, 0, receivedData.Length);
					
					if (isWss) {
						tlsClientProtocol.OfferInput(receivedData);
						if (0 < tlsClientProtocol.GetAvailableInputBytes()) {
							var index = 0;
							var length = tlsClientProtocol.GetAvailableInputBytes();
							if (webSocketHandshakeResult == null) {
								webSocketHandshakeResult = new byte[length];
							} else {
								index = webSocketHandshakeResult.Length;
								// already hold bytes, and should expand for holding more decrypted data.
								Array.Resize(ref webSocketHandshakeResult, webSocketHandshakeResult.Length + length);
							}

							// このReadInputの代わりに、内容のコピーを試しに取得できれば良い感じになる。この関数のデータは整合性が取れたら捨てる、みたいな処理ができればそれはそれで価値がありそう。
							// とりあえずこれで動かしてみる。
							tlsClientProtocol.ReadInput(webSocketHandshakeResult, index, length);
						}

						// failed to get tls decrypted data from current receiving data.
						// continue receiving next data at the end of this case.
					} else {
						var index = 0;
						var length = args.BytesTransferred;
						if (webSocketHandshakeResult == null) {
							webSocketHandshakeResult = new byte[args.BytesTransferred];
						} else {
							index = webSocketHandshakeResult.Length;
							// already hold bytes, and should expand for holding more decrypted data.
							Array.Resize(ref webSocketHandshakeResult, webSocketHandshakeResult.Length + length);
						}
						Buffer.BlockCopy(args.Buffer, 0, webSocketHandshakeResult, index, length);
					}

					if (0 < webSocketHandshakeResult.Length) {
						var lineEndCursor = ReadUpgradeLine(webSocketHandshakeResult, 0, webSocketHandshakeResult.Length);
						if (lineEndCursor != -1) {
							var protocolData = new SwitchingProtocolData(Encoding.UTF8.GetString(webSocketHandshakeResult, 0, lineEndCursor));
							var expectedKey = WebSocketByteGenerator.GenerateExpectedAcceptedKey(base64Key);
							if (protocolData.securityAccept != expectedKey) {
								if (OnError != null) {
									var error =  new Exception("WebSocket Key Unmatched.");
									OnError(WebuSocketErrorEnum.WS_HANDSHAKE_KEY_UNMATCHED, error);
								}
							}
							token.socketState = SocketState.OPENED;
							if (OnConnected != null) OnConnected();
							
							wsBuffer = new byte[baseReceiveBufferSize];
							wsBufIndex = 0;

							// ready for receiving websocket data.
							ReadyReceivingNewData(token);
							return;
						}
					}
					
					// continue receiveing websocket handshake data.
					ReadyReceivingNewData(token);
					return;
				}
				case SocketState.OPENED: {
					if (isWss) {
						// 切り出しが必ずbyte[]の形で必要なの地獄っぽい。なんとかできるかな。
						var buffer = new byte[args.BytesTransferred];
						Buffer.BlockCopy(args.Buffer, 0, buffer, 0, buffer.Length);
						
						tlsClientProtocol.OfferInput(buffer);


						if (0 < tlsClientProtocol.GetAvailableInputBytes()) {
							var additionalLen = tlsClientProtocol.GetAvailableInputBytes();
							
							if (wsBuffer.Length < wsBufIndex + additionalLen) {
								Array.Resize(ref wsBuffer, wsBufIndex + additionalLen);
								// Debug.LogError("resize, wsBuffer:" + wsBuffer.Length);
							}

							// transfer from tls buffer to wsBuffer.
							// 仮読み出しと破棄読み出しを実装できれば、こいつが1次バッファでいい感じがする
							tlsClientProtocol.ReadInput(wsBuffer, wsBufIndex, additionalLen);
							
							wsBufLength = wsBufLength + additionalLen; 
						} else {
							// Debug.LogError("incomlete tls bytes, continue.");
							ReadyReceivingNewData(token);
							return;
						}
					} else {
						var additionalLen = args.BytesTransferred;

						if (wsBuffer.Length < wsBufIndex + additionalLen) {
							Array.Resize(ref wsBuffer, wsBufIndex + additionalLen);
							// Debug.LogError("resize, wsBuffer:" + wsBuffer.Length);
						}

						Buffer.BlockCopy(args.Buffer, 0, wsBuffer, wsBufIndex, additionalLen);
						wsBufLength = wsBufLength + additionalLen;
					}

					var result = ScanBuffer(wsBuffer, wsBufLength);
					
					// read completed datas.
					if (0 < result.segments.Count) {
						OnMessage(result.segments);
					}
					
					// if the last result index is matched to whole length, receive finished.
					if (result.lastDataTail == wsBufLength) {
						wsBufIndex = 0;
						wsBufLength = 0;
						ReadyReceivingNewData(token);
						return;
					}

					// unreadable data still exists in wsBuffer.
					var unreadDataLength = wsBufLength - result.lastDataTail;

					if (result.lastDataTail == 0) {
						// no data is read as WS data. 
						// this means the all data in wsBuffer is not enough to read as WS data yet.
						// need more data to add the last of wsBuffer.

						// set wsBufferIndex and wsBufLength to the end of current buffer.
						wsBufIndex = unreadDataLength;
						wsBufLength = unreadDataLength;
					} else {
						// not all of wsBuffer data is read as WS data.
						// data which is located before alreadyReadDataTail is already read.
						
						// move rest "unreaded" data to head of wsBuffer.
						Array.Copy(wsBuffer, result.lastDataTail, wsBuffer, 0, unreadDataLength);

						// then set wsBufIndex to 
						wsBufIndex = unreadDataLength;
						wsBufLength = unreadDataLength;
					}

					// should read rest.
					ReadyReceivingNewData(token);
					return;
				}
				default: {
					var error = new Exception("fatal error, could not detect error, receive condition is strange, token.socketState:" + token.socketState);
					if (OnError != null) OnError(WebuSocketErrorEnum.RECEIVE_FAILED, error);
					Disconnect(true); 
					return;
				}
			}
		}
		
		private void ReadyReceivingNewData (SocketToken token) {
			token.receiveArgs.SetBuffer(token.receiveBuffer, 0, token.receiveBuffer.Length);
			if (!token.socket.ReceiveAsync(token.receiveArgs)) OnReceived(token.socket, token.receiveArgs);
		}
		
		public void Disconnect (bool force=false) {
			lock (lockObj) {
				switch (socketToken.socketState) {
					case SocketState.CLOSING:
					case SocketState.CLOSED: {
						// do nothing
						break;
					}
					default: {
						if (force) {
							try {
								socketToken.socket.Close();
							} catch {
								// do nothing.
							}
							socketToken.socketState = SocketState.CLOSED;
							if (OnClosed != null) OnClosed(WebuSocketCloseEnum.CLOSED_FORCELY); 
							return;
						}

						socketToken.socketState = SocketState.CLOSING;
						
						StartCloseAsync();
						break;
					}
				}
			}
		}
		
		private void StartCloseAsync () {
			var closeEventArgs = new SocketAsyncEventArgs();
			closeEventArgs.UserToken = socketToken;
			closeEventArgs.AcceptSocket = socketToken.socket;

			var closeData = WebSocketByteGenerator.CloseData();
			closeEventArgs.SetBuffer(closeData, 0, closeData.Length);

			closeEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnDisconnected);
			
			if (!socketToken.socket.SendAsync(closeEventArgs)) OnDisconnected(socketToken.socket, closeEventArgs);
		}
		
		private bool IsSocketConnected (Socket s) {
			bool part1 = s.Poll(10, SelectMode.SelectRead);
			bool part2 = (s.Available == 0);
			
			if (part1 && part2) return false;
			
			return true;
		}
		
		public static byte ByteCR = Convert.ToByte('\r');
		public static byte ByteLF = Convert.ToByte('\n');
		public static int ReadUpgradeLine (byte[] bytes, int cursor, long length) {
			while (cursor < length) {
				if (4 < cursor && 
					bytes[cursor - 3] == ByteCR && 
					bytes[cursor - 2] == ByteLF &&
					bytes[cursor - 1] == ByteCR && 
					bytes[cursor] == ByteLF
				) return cursor - 1;
				
				cursor++;
			}
			
			return -1;
		}
		
		
		private class SwitchingProtocolData {
			// HTTP/1.1 101 Switching Protocols
			// Server: nginx/1.7.10
			// Date: Sun, 22 May 2016 18:31:47 GMT
			// Connection: upgrade
			// Upgrade: websocket
			// Sec-WebSocket-Accept: C3HoL/ER1LOnEj8yVINdXluouHw=
			
			public string protocolDesc;
			public string httpResponseCode;
			public string httpMessage;
			public string serverInfo;
			public string date;
			public string connectionType;
			public string upgradeMethod;
			public string securityAccept;
			
			public SwitchingProtocolData (string source) {
				var acceptedResponseHeaderKeyValues = source.Split('\n');
				foreach (var line in acceptedResponseHeaderKeyValues) {
					if (line.StartsWith("HTTP")) {
						var httpResponseHeaderSplitted = line.Split(' ');
						this.protocolDesc = httpResponseHeaderSplitted[0];
						this.httpResponseCode = httpResponseHeaderSplitted[1];
						this.httpMessage = httpResponseHeaderSplitted[2] + httpResponseHeaderSplitted[3];
						continue;
					}
					
					if (!line.Contains(": ")) continue;
					
					var keyAndValue = line.Replace(": ", ":").Split(':');
					
					switch (keyAndValue[0]) {
						case "Server": {
							this.serverInfo = keyAndValue[1];
							break;
						}
						case "Date": {
							this.date = keyAndValue[1];
							break;
						}
						case "Connection": {
							this.connectionType = keyAndValue[1];
							break;
						}
						case "Upgrade": {
							this.upgradeMethod = keyAndValue[1];
							break;
						}
						case "Sec-WebSocket-Accept": {
							this.securityAccept = keyAndValue[1].TrimEnd();
							break;
						}
						default: {
							throw new Exception("invalid key value found. line:" + line);
						}
					}
				}
			}
		}
		
		private Queue<ArraySegment<byte>> receivedDataSegments = new Queue<ArraySegment<byte>>();
		private byte[] continuationBuffer;
		private int continuationBufferIndex;
		private WebuSocketResults ScanBuffer (byte[] buffer, long bufferLength) {
			receivedDataSegments.Clear();
			
			int messageHead = 0;
			int cursor = 0;
			int lastDataEnd = 0;
			while (cursor < bufferLength) {
				messageHead = cursor;
				
				// first byte = fin(1), rsv1(1), rsv2(1), rsv3(1), opCode(4)
				var opCode = (byte)(buffer[cursor++] & WebSocketByteGenerator.OPFilter);
				
				// second byte = mask(1), length(7)
				if (bufferLength < cursor) break;
				
				/*
					mask of data from server is definitely zero(0).
					ignore reading mask bit.
				*/
				int length = buffer[cursor++];
				switch (length) {
					case 126: {
						// next 2 byte is length data.
						if (bufferLength < cursor + 2) break;
						
						length = (
							(buffer[cursor++] << 8) +
							(buffer[cursor++])
						);
						break;
					}
					case 127: {
						// next 8 byte is length data.
						if (bufferLength < cursor + 8) break;
						
						length = (
							(buffer[cursor++] << (8*7)) +
							(buffer[cursor++] << (8*6)) +
							(buffer[cursor++] << (8*5)) +
							(buffer[cursor++] << (8*4)) +
							(buffer[cursor++] << (8*3)) +
							(buffer[cursor++] << (8*2)) +
							(buffer[cursor++] << 8) +
							(buffer[cursor++])
						);
						break;
					}
					default: {
						// other.
						break;
					}
				}
				
				// read payload data.
				if (bufferLength < cursor + length) break;
				
				// payload is fully contained!
				switch (opCode) {
					case WebSocketByteGenerator.OP_CONTINUATION: {
						if (continuationBuffer == null) continuationBuffer = new byte[baseReceiveBufferSize];
						if (continuationBuffer.Length <= continuationBufferIndex + length) Array.Resize(ref continuationBuffer, continuationBufferIndex + length);

						// pool data to continuation buffer.
						Buffer.BlockCopy(buffer, cursor, continuationBuffer, continuationBufferIndex, length);
						continuationBufferIndex += length;
						break;
					}
					case WebSocketByteGenerator.OP_TEXT:
					case WebSocketByteGenerator.OP_BINARY: {
						if (continuationBufferIndex == 0) receivedDataSegments.Enqueue(new ArraySegment<byte>(buffer, cursor, length));
						else {
							if (continuationBuffer.Length <= continuationBufferIndex + length) Array.Resize(ref continuationBuffer, continuationBufferIndex + length);
							Buffer.BlockCopy(buffer, cursor, continuationBuffer, continuationBufferIndex, length);
							continuationBufferIndex += length;
							
							receivedDataSegments.Enqueue(new ArraySegment<byte>(continuationBuffer, 0, continuationBufferIndex));
							
							// reset continuationBuffer index.
							continuationBufferIndex = 0;
						}
						break;
					}
					case WebSocketByteGenerator.OP_CLOSE: {
						CloseReceived();
						break;
					}
					case WebSocketByteGenerator.OP_PING: {
						PingReceived();
						break;
					}
					case WebSocketByteGenerator.OP_PONG: {
						PongReceived();
						break;
					}
					default: {
						break;
					}
				}
				
				cursor = cursor + length;
				
				// set end of data.
				lastDataEnd = cursor;
			}
			
			// finally return payload data indexies.
			return new WebuSocketResults(receivedDataSegments, lastDataEnd);
		}
		
		private struct WebuSocketResults {
			public Queue<ArraySegment<byte>> segments;
			public int lastDataTail;
			
			public WebuSocketResults (Queue<ArraySegment<byte>> segments, int lastDataTail) {
				this.segments = segments;
				this.lastDataTail = lastDataTail;
			}
		}
		
		private Action OnPonged;
		public void Ping (Action OnPonged) {
			this.OnPonged = OnPonged;
			var pingBytes = WebSocketByteGenerator.Ping();
			
			socketToken.sendArgs.SetBuffer(pingBytes, 0, pingBytes.Length);
			if (!socketToken.socket.SendAsync(socketToken.sendArgs)) OnSend(socketToken.socket, socketToken.sendArgs);	
		}
		
		public void Send (byte[] data) {
			if (socketToken.socketState != SocketState.OPENED) {
				WebuSocketErrorEnum ev = WebuSocketErrorEnum.UNKNOWN_ERROR;
				Exception error = null;
				switch (socketToken.socketState) {
					case SocketState.TLS_HANDSHAKING:
					case SocketState.WS_HANDSHAKING: {
						ev = WebuSocketErrorEnum.CONNECTING;
						error = new Exception("send error:" + "not yet connected.");
						break;
					}
					case SocketState.CLOSING:
					case SocketState.CLOSED: {
						ev = WebuSocketErrorEnum.ALREADY_DISCONNECTED;
						error = new Exception("send error:" + "connection was already closed. please create new connection by new WebuSocket().");
						break;
					}
					default: {
						ev = WebuSocketErrorEnum.CONNECTING;
						error = new Exception("send error:" + "not yet connected.");
						break;
					}
				}
				if (OnError != null) OnError(ev, error);
				return;
			}

			var payloadBytes = WebSocketByteGenerator.SendBinaryData(data);

			if (isWss) {
				tlsClientProtocol.OfferOutput(payloadBytes, 0, payloadBytes.Length);
				
				var buffer = new byte[tlsClientProtocol.GetAvailableOutputBytes()];
				tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);

				socketToken.sendArgs.SetBuffer(buffer, 0, buffer.Length);
			} else {
				socketToken.sendArgs.SetBuffer(payloadBytes, 0, payloadBytes.Length);
			}
			
			if (!socketToken.socket.SendAsync(socketToken.sendArgs)) OnSend(socketToken.socket, socketToken.sendArgs);
		}

		public void SendString (string data) {
			if (socketToken.socketState != SocketState.OPENED) {
				WebuSocketErrorEnum ev = WebuSocketErrorEnum.UNKNOWN_ERROR;
				Exception error = null;
				switch (socketToken.socketState) {
					case SocketState.TLS_HANDSHAKING:
					case SocketState.WS_HANDSHAKING: {
						ev = WebuSocketErrorEnum.CONNECTING;
						error = new Exception("send error:" + "not yet connected.");
						break;
					}
					case SocketState.CLOSING:
					case SocketState.CLOSED: {
						ev = WebuSocketErrorEnum.ALREADY_DISCONNECTED;
						error = new Exception("send error:" + "connection was already closed. please create new connection by new WebuSocket().");
						break;
					}
					default: {
						ev = WebuSocketErrorEnum.CONNECTING;
						error = new Exception("send error:" + "not yet connected.");
						break;
					}
				}
				if (OnError != null) OnError(ev, error);
				return;
			}
			
			var byteData = Encoding.UTF8.GetBytes(data);
			var payloadBytes = WebSocketByteGenerator.SendTextData(byteData);
			
			if (isWss) {
				tlsClientProtocol.OfferOutput(payloadBytes, 0, payloadBytes.Length);
				
				var buffer = new byte[tlsClientProtocol.GetAvailableOutputBytes()];
				tlsClientProtocol.ReadOutput(buffer, 0, buffer.Length);

				socketToken.sendArgs.SetBuffer(buffer, 0, buffer.Length);
			} else {
				socketToken.sendArgs.SetBuffer(payloadBytes, 0, payloadBytes.Length);
			}

			if (!socketToken.socket.SendAsync(socketToken.sendArgs)) OnSend(socketToken.socket, socketToken.sendArgs);
		}

		public bool IsConnected () {
			if (socketToken.socketState != SocketState.OPENED) return false;
			return true;
		}
		
		private void CloseReceived () {
			lock (lockObj) {
				switch (socketToken.socketState) {
					case SocketState.OPENED: {
						socketToken.socketState = SocketState.CLOSED;
						if (OnClosed != null) OnClosed(WebuSocketCloseEnum.CLOSED_BY_SERVER);
						Disconnect();
						break;
					}
					default: {
						
						break;
					}
				}
			}
		}
		
		private void PingReceived () {
			if (OnPinged != null) OnPinged();
			
			var pongBytes = WebSocketByteGenerator.Pong();
			socketToken.sendArgs.SetBuffer(pongBytes, 0, pongBytes.Length);
			if (!socketToken.socket.SendAsync(socketToken.sendArgs)) OnSend(socketToken.socket, socketToken.sendArgs);	
		}
		
		private void PongReceived () {
			if (OnPonged != null) OnPonged();
		}
	}
}
