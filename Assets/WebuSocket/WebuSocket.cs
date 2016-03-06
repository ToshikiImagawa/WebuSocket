using UnityEngine;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

namespace WebuSocket {
	
	public class WebuSocketClient {
		private static RNGCryptoServiceProvider randomGen = new RNGCryptoServiceProvider();
		
		private Socket socket;
		private readonly Thread updater;
		
		private const string CRLF = "\r\n";
		private const int BUF_SIZE = 1024;
		private const string WEBSOCKET_VERSION = "13"; 
		
			
		public enum ReadyState : int {
			Connecting,
			Open,
			Closing,
			Closed
		}
		
		private enum WSOrder : int {
			CloseGracefully,
		}
		
		private Queue<WSOrder> stackedOrders = new Queue<WSOrder>();
		
		private Queue<byte[]> stackedSendingDatas = new Queue<byte[]>();
		private Queue<byte[]> stackedReceivedDatas = new Queue<byte[]>();	
		
		
		private ReadyState state;
		
		
	
		
		public WebuSocketClient (string protocol, string host, int port) {
			var url = protocol + "://" + host + ":" + port + "/calivers_disque_client";
			
			// wsかwss、まずはws protocolだけサポートする。いきなり非同期でいいんじゃねーかな。
			Debug.LogError("url:" + url);
			
			Debug.LogError("非同期前提にする。なので、ここでconnectingにして返す。");
			state = ReadyState.Connecting;
			int i = 0;

			// thread for websocket.
			updater = Updater(
				"WebuSocket",
				() => {
					switch (state) {
						case ReadyState.Connecting: {
							var socketComponent = WebSocketHandshakeRequest(url);
							if (socketComponent != null) {
								state = ReadyState.Open;
								this.socket = socketComponent.socket;
								break;
							}
							
							// connection failed.
							ForceClose();
							
							break;
						}
						case ReadyState.Open: {
							lock (socket) {
								while (0 < socket.Available) {
									Debug.LogError("socket.Available:" + socket.Available);
									// ここでデータを読んじゃえばいいよね
									// 全力でデキューしちゃって放置
									var buff = new byte[socket.Available];
									socket.Receive(buff);
									stackedReceivedDatas.Enqueue(buff);
								}
							}
							
							lock (stackedOrders) {
								while (0 < stackedOrders.Count) {
									var order = stackedOrders.Dequeue();
									ExecuteOrder(order);
								}
							}
							
							lock (stackedSendingDatas) {
								while (state == ReadyState.Open && 0 < stackedSendingDatas.Count) {
									// queueに入ってるもの全部繋げて送信ってやっていいような気がするんだが、まあちゃんとやるか、、
									var data = stackedSendingDatas.Dequeue();
									
									// websocketのフォーマッティングを行う。さて。
									
									// socket.Send
								}
							} 
							
							if (i == 100) {// 試しにgraceful close.
								StackOrder(WSOrder.CloseGracefully);
							}
							
							break;
						}
						case ReadyState.Closing: {
							lock (socket) {
								while (0 < socket.Available) {
									var buff = new byte[socket.Available];
									socket.Receive(buff);
									stackedReceivedDatas.Enqueue(buff);
								}
							}
							
							lock (stackedOrders) {
								while (0 < stackedOrders.Count) {
									var order = stackedOrders.Dequeue();
									ExecuteOrder(order);
								}
							}
							break;
						}
						case ReadyState.Closed: {
							// break this thread.
							return false;
						}
					}
					
					i++;
					
					return true;
				}
			);
		}
		
		
		public void CloseAsync () {
			switch (state) {
				case ReadyState.Open: {
					StackOrder(WSOrder.CloseGracefully);
					break;
				}
				default: {
					Debug.LogError("current state is:" + state + ", close operation request is ignored.");
					break;
				}
			}
		}
		
		/*
			forcely close socket on this time.
		*/
		public void Close () {
			ForceClose();
		}
		
		
		
		
		private void StackOrder (WSOrder order) {
			lock (stackedOrders) stackedOrders.Enqueue(order);
		}
		
		
		private void ExecuteOrder (WSOrder order) {
			switch (order) {
				case WSOrder.CloseGracefully: {
					state = ReadyState.Closing;
					
					// this order is final one of this thread. ignore all other orders.
					stackedOrders.Clear();
					
					var data = WebSocketByteGenerator.CloseData();
					TrySend(data);
					ForceClose();
					break;
				}
				default: {
					Debug.LogError("unhandled order:" + order);
					break;
				}
			}
		}
		
		
		private void TrySend (byte[] data) {
			try {
				socket.Send(data);
			} catch (Exception e0) {
				Debug.LogError("TrySend failed:" + e0 + ". attempt to close forcely.");
				ForceClose();
			}
		}
		
		private void ForceClose () {
			if (state == ReadyState.Closed) {
				Debug.LogError("already closed.");
				return;
			}
			
			state = ReadyState.Closed;
			
			try {
				if (socket.Connected) {
					socket.Close();
				}
			} catch (Exception e) {
				Debug.LogError("socket closing error:" + e);
			} finally {
				socket = null;
			}
		}
		// 別スレッドでqueueにする、とか？うーん、、どうやって渡すのを前提にしようかな、、
		// mainThreadから実行される関数、っていうのでくるんでしまっていいような気がするんですがどうすかね。
		
		// 受け取り/送信threadの分解能はまあ可変かつorderedってとこで、無限でもいいわけで。
		// 受け取ったものがqueueに入る->それを吸い取る、でいいわけで。であればpull型か、、OnMainThreadでメソッドが実行される、とかのほうがいいんだよな。
		
		// Threadを受け取って、そこで実行されるハンドラを自分たちで置いてね、でいいか。Threadを受け取れれば、そのThreadのタイミングで処理できる。
		// それがMainThreadかどうかはあんまり関係ない。（Thread出した側で処理すればいい。
		
		
		private SocketComponent WebSocketHandshakeRequest (string urlSource) {
			var uri = new Uri(urlSource);
			
			var method = "GET";
			var host = uri.Host;
			var schm = uri.Scheme;
			var port = uri.Port;
			
			var agent = "testing_webuSocket_client";
			var base64Key = GeneratePrivateBase64Key();
			
			Debug.LogError("wss is not supported yet.");
			/*
				unsupporteds:
					wss,
					redirect,
					proxy,
					
			*/
			
			var requestHeaderParams = new Dictionary<string, string>{
				{"Host", (port == 80 && schm == "ws") || (port == 443 && schm == "wss") ? uri.DnsSafeHost : uri.Authority},
				{"Upgrade", "websocket"},
				{"Connection", "Upgrade"},
				{"Sec-WebSocket-Key", base64Key},
				{"Sec-WebSocket-Version", WEBSOCKET_VERSION},
				{"User-Agent", agent}
			};
			
			/*
				construct request bytes data.
			*/
			var requestData = new StringBuilder();
			
			requestData.AppendFormat("{0} {1} HTTP/{2}{3}", method, uri, "1.1", CRLF);

			foreach (var key in requestHeaderParams.Keys) requestData.AppendFormat("{0}: {1}{2}", key, requestHeaderParams[key], CRLF);

			requestData.Append (CRLF);

			var entity = string.Empty;
			requestData.Append(entity);
			
			var requestDataBytes = Encoding.UTF8.GetBytes(requestData.ToString().ToCharArray());
			
			/*
				ready connection sockets.
			*/
			var timeout = 1000;
			
			var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			sock.NoDelay = true;
			sock.SendTimeout = timeout;
			
			try {
				sock.Connect(host, port);
			} catch (Exception e) {
				Debug.LogError("failed to connect to host:" + host + " error:" + e);
				return null;
			}
			
			if (!sock.Connected) {
				Debug.LogError("failed to connect.");
				sock.Close();
				sock = null;
				return null;
			}
			
			try {
				var result = sock.Send(requestDataBytes);
				
				if (0 < result) {}// succeeded to send.
				else {
					Debug.LogError("failed to send connection request data, send size is 0.");
					sock.Close();
					sock = null;
					return null;
				}
			} catch (Exception e) {
				Debug.LogError("failed to send connection request data. error:" + e);
				sock.Close();
				sock = null;
			}
			
			
			
			/*
				read connection response from socket.
			*/
			var responceHeaderDict = WaitResponceFromServer(sock);
			
			// validate.
			if (!responceHeaderDict.ContainsKey("Server".ToLower())) {
				Debug.LogError("failed to receive 'Server' key.");
				return null;
			}
			
			if (!responceHeaderDict.ContainsKey("Date".ToLower())) {
				Debug.LogError("failed to receive 'Date' key.");
				return null;
			}
			
			if (!responceHeaderDict.ContainsKey("Connection".ToLower())) {
				Debug.LogError("failed to receive 'Connection' key.");
				return null;
			}
			
			if (!responceHeaderDict.ContainsKey("Upgrade".ToLower())) {
				Debug.LogError("failed to receive 'Upgrade' key.");
				return null;
			}
			
			if (!responceHeaderDict.ContainsKey("Sec-WebSocket-Accept".ToLower())) {
				Debug.LogError("failed to receive 'Sec-WebSocket-Accept' key.");
				return null;
			}
			var serverAcceptedWebSocketKey = responceHeaderDict["Sec-WebSocket-Accept".ToLower()];
			
			
			if (!sock.Connected) {
				Debug.LogError("disconnected,");
				return null;
			}
			
			return new SocketComponent(sock, base64Key, serverAcceptedWebSocketKey);
		}
		
		public class SocketComponent {
			public readonly Socket socket;
			public readonly string base64Key;
			public readonly string acceptedKey;
			public SocketComponent (Socket socket, string base64Key, string acceptedKey) {
				this.socket = socket;
				this.base64Key = base64Key;
				this.acceptedKey = acceptedKey;
			}
		}
		
		private Dictionary<string, string> WaitResponceFromServer (Socket sock) {
			var responseHeaderDict = new Dictionary<string, string>();
			
			/*
				protocol should be switched.
			*/
			var protocolResponse = ReadLineBytes(sock);
			if (Encoding.UTF8.GetString(protocolResponse).ToLower() != "HTTP/1.1 101 Switching Protocols".ToLower()) {
				return new Dictionary<string, string>();
			}
			
			if (sock.Available == 0) {
				Debug.LogError("failed to receive rest of response header.");
				return new Dictionary<string, string>();
			}
			
			/*
				rest data exists and can be received.
			*/
			while (0 < sock.Available) {
				var responseHeaderLineBytes = ReadLineBytes(sock);
				var responseHeaderLine = Encoding.UTF8.GetString(responseHeaderLineBytes);
				
				if (!responseHeaderLine.Contains(":")) continue;
				  
				var splittedKeyValue = responseHeaderLine.Split(':');
				
				var key = splittedKeyValue[0].ToLower();
				var val = splittedKeyValue[1];
				
				responseHeaderDict[key] = val;
			}
			
			return responseHeaderDict;
		}
		
		public byte ReadFirstByte (Socket sock) {
			byte[] b = new byte[1];
			sock.Receive(b);
			return b[0];
		}

		public byte[] ReadLineBytes (Socket sock) {
			var buf = new byte[BUF_SIZE];
			byte[] b = new byte[1];
			
			int limit = sock.Available;

			int i = 0;
			while (true) {
				sock.Receive(b);
				if (b[0] == '\r') continue;
				if (b[0] == '\n') break;
				
				buf[i] = b[0];
				
				if (i == limit) {
					Debug.Log("limit by Available.");
					break;
				}

				if (i == buf.Length) {
					Debug.Log("too large line.");
					break;
				}

				i++;
			}
			var retByte = new byte[i];
			Array.Copy(buf, 0, retByte, 0, i);

			return retByte;
		}
		
		private static string GeneratePrivateBase64Key () {
			var src = new byte[16];
			randomGen.GetBytes(src);
			return Convert.ToBase64String(src);
		}
		
		public static byte[] NewMaskKey () {
			var maskingKeyBytes = new byte[4];
			randomGen.GetBytes(maskingKeyBytes);
			return maskingKeyBytes;
		}
		
		private Thread Updater (string loopId, Func<bool> OnUpdate) {
			var framePerSecond = 60;
			var mainThreadInterval = 1000f / framePerSecond;
			
			Action loopMethod = () => {
				try {
					double nextFrame = (double)System.Environment.TickCount;
					
					var before = 0.0;
					var tickCount = (double)System.Environment.TickCount;
					
					while (true) {
						tickCount = System.Environment.TickCount * 1.0;
						if (nextFrame - tickCount > 1) {
							Thread.Sleep((int)(nextFrame - tickCount)/2);
							/*
								waitを半分くらいにすると特定フレームで安定した。
							*/
							continue;
						}
						
						if (tickCount >= nextFrame + mainThreadInterval) {
							nextFrame += mainThreadInterval;
							continue;
						}
						
						// run action for update.
						var continuation = OnUpdate();
						if (!continuation) break;
						
						nextFrame += mainThreadInterval;
						before = tickCount; 
					}
					
					Debug.Log("loopId:" + loopId + " is finished.");
				} catch (Exception e) {
					Debug.LogError("loopId:" + loopId + " error:" + e);
				}
			};
			
			var thread = new Thread(new ThreadStart(loopMethod));
			thread.Start();
			return thread;
		}
		
		
		private void TCPConnect (string host, int port) {
			this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			this.socket.NoDelay = true;
            
			int timeout = 1000;// うーん怖い
			
            this.socket.SendTimeout = timeout;
			
			try {
				this.socket.Connect(host, port);
			} catch (Exception e) {
				Debug.Log("ERROR: WebuSocketClient: failed to connect to server @:" + host + ":" + port);
				Debug.Log("ERROR: reason:" + e);
			}

			if (!this.socket.Connected) {
				// failed to create connection. 
				this.socket.Close();
				this.socket = null;
				throw new Exception("failed to connect to server @:" + host + ":" + port);
			}
			
			Debug.Log("succeedet to connect. ただしTCP");
			
			// self.listening = True
			// while self.listening:
			// 	try:
			// 		# when teardown, causes close then "Software caused connection abort"
			// 		(conn, addr) = self.socket.accept()

			// 		identity = str(uuid.uuid4())

			// 		# genereate new client
			// 		client = WSClient(self, identity)
					
			// 		self.clientIds[identity] = client

			// 		threading.Thread(target = client.handle, args = (conn,addr)).start()
			// 	except socket.error as msg:
			// 		errorMsg = "SublimeSocket WebSocketServing crashed @ " + str(self.host) + ":" + str(self.port) + " reason:" + str(msg)
			// 		self.sublimeSocketServer.transferNoticed(errorMsg)
		}
		
		
		
		
	}
}
