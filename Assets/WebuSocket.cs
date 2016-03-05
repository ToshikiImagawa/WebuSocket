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
		private Socket socket;
		
		public WebuSocketClient (string protocol, string host, int port) {
			var url = protocol + "://" + host + ":" + port;
			
			// wsかwss、まずはws protocolだけサポートする。いきなり非同期でいいんじゃねーかな。
			Debug.LogError("url:" + url);
			
			Debug.LogError("非同期前提にする。なので、ここでconnectingにして返す。");
			state = ReadyState.Connecting;
			
			// threadを作って放置
			Updater(
				"WebuSocket",
				() => {
					switch (state) {
						case ReadyState.Connecting: {
							WebSocketHandshakeRequest(url);
							
							// TCPConnect(host, port);
							
							Debug.LogError("終わったらOpenとかに変わってるはず");
							state = ReadyState.Closed;
							break;
						}
					}
					
					return true;
				}
			);
		}
		
		
		
		private void WebSocketHandshakeRequest (string urlSource) {
			Debug.LogError("ダミーパラメータ一杯 urlSource:" + urlSource);
			
			var uri = new Uri(urlSource);
			
			var method = "GET";
			var host = uri.Host;
			var schm = uri.Scheme;
			var port = uri.Port;
			
			
			var agent = "testing_webuSocket_client";
			var base64Key = GeneratePrivateBase64Key();
			
			Debug.LogError("uri.DnsSafeHost:" + uri.DnsSafeHost);
			
			var requestHeaderParams = new Dictionary<string, string>{
				{"Host", (port == 80 && schm == "ws") || (port == 443 && schm == "wss") ? uri.DnsSafeHost : uri.Authority},
				{"Upgrade", "websocket"},
				{"Connection", "Upgrade"},
				{"Sec-WebSocket-Key", base64Key},
				{"Sec-WebSocket-Version", WEBSOCKET_VERSION},
				{"User-Agent", agent}
			};
			
			var output = new StringBuilder();
			output.AppendFormat("{0} {1} HTTP/{2}{3}", method, uri, "1.1", CRLF);

			foreach (var key in requestHeaderParams.Keys) output.AppendFormat("{0}: {1}{2}", key, requestHeaderParams[key], CRLF);

			output.Append (CRLF);

			var entity = string.Empty;
			output.Append(entity);
			
			Debug.LogError("output:" + output.ToString());
			
			
			var outputBytes = Encoding.UTF8.GetBytes(output.ToString().ToCharArray());
			
			// if (_proxyUri != null) {
			// 	_tcpClient = new TcpClient (_proxyUri.DnsSafeHost, _proxyUri.Port);
			// 	_stream = _tcpClient.GetStream ();
			// 	sendProxyConnectRequest ();
			// } else {
			
			var timeout = 1000;
			
			var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			sock.NoDelay = true;
			sock.SendTimeout = timeout;
			
			// }

			// if (_secure) {
			// 	var conf = SslConfiguration;
			// 	var host = conf.TargetHost;
			// 	if (host != _uri.DnsSafeHost)
			// 	throw new WebSocketException (
			// 		CloseStatusCode.TlsHandshakeFailure, "An invalid host name is specified.");

			// 	try {
			// 	var sslStream = new SslStream (
			// 		_stream,
			// 		false,
			// 		conf.ServerCertificateValidationCallback,
			// 		conf.ClientCertificateSelectionCallback);

			// 	sslStream.AuthenticateAsClient (
			// 		host,
			// 		conf.ClientCertificates,
			// 		conf.EnabledSslProtocols,
			// 		conf.CheckCertificateRevocation);

			// 	_stream = sslStream;
			// 	}
			// 	catch (Exception ex) {
			// 	throw new WebSocketException (CloseStatusCode.TlsHandshakeFailure, ex);
			// 	}
			// }
			Debug.LogError("host:" + host);
			try {
				// なるほど、ダイレクトにしか繋げない、、のか、、httpじゃないから、、
				// httpでつなぐためには、tcpで接続したあとに何かする必要がある感じがする。
				sock.Connect(host, port);
			} catch (Exception e) {
				Debug.LogError("failed to connect to host:" + host + " error:" + e);
			}
			
			if (!sock.Connected) {
				Debug.LogError("failed to connect.");
				sock.Close();
				sock = null;
				return;
			}
			
			var result = sock.Send(outputBytes);// これを送ると、っていう感じなので、送るものに工夫できれば良さげ。
			if (0 < result) {}// succeeded to send.
			else {
				Debug.LogError("failed to send data.");
				return;
			}
			
			// 改行コードまでは読んでいいような気がする。
			while (0 < sock.Available) {
				var res = ReadLineBytes(sock);
				Debug.LogError("res:" + Encoding.UTF8.GetString(res));
			}
			
			Debug.LogError("got resonse");
			
			// try {
			// 	var httpResponseHeaders = readHeaders (stream, _headersMaxLength)
			// 	var contentLen = http.Headers()["Content-Length"];
			// 	if (contentLen != null && contentLen.Length > 0)
			// 	http.EntityBodyData = readEntityBody (stream, contentLen);
			// }
			// catch (Exception ex) {
			// 	exception = ex;
			// }
			// finally {
			// 	timer.Change (-1, -1);
			// 	timer.Dispose ();
			// }

			// var msg = timeout
			// 			? "A timeout has occurred while reading an HTTP request/response."
			// 			: exception != null
			// 			? "An exception has occurred while reading an HTTP request/response."
			// 			: null;

			// if (msg != null)
			// 	throw new WebSocketException (msg, exception);

			// return http;
			// }
			
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
			var randomGen = new RNGCryptoServiceProvider();
			
			var src = new byte[16];
			randomGen.GetBytes(src);
			return Convert.ToBase64String(src);
		}
		
		private void Updater (string loopId, Func<bool> OnUpdate) {
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
							// XrossPeer.Log("wait:" + (int)(nextFrame - tickCount));
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
		
		
		private const string CRLF = "\r\n";
		private const int BUF_SIZE = 1024;
		private const string WEBSOCKET_VERSION = "13"; 
		private const string WEBSOCKET_MAGIC = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
		
		public enum ReadyState : int {
			Connecting,
			Open,
			Closing,
			Closed
		}
		
		private ReadyState state;
		
		private void HandShake () {
		// def handshake(self):
			var headers = new Dictionary<string, string>();
			
			// # Ignore first line with GET
			var line = ReadLineHeader();

			while (state == ReadyState.Connecting) {//self.hasStatus('CONNECTING'):
				if (64 < headers.Count) throw new Exception("Header is too long.");
					
				line = ReadLineHeader();
				
				// 接続中状態を維持してなければ離れる。
				if (state != ReadyState.Connecting) throw new Exception("Client is left.");
				
				if (line.Length == 0 || line.Length == BUF_SIZE) throw new Exception("Invalid line in header.");
				
				// detect read end.
				if (line == CRLF) break;

				// 前後の空白削ってる
				// line = line.strip()
				
				var kv = line.Split(':');// 一個以上:がある場合マズイ。
				
				if (kv.Length == 2) {
					var k = kv[0].ToLower();// ここでも前後の空白削るのやってる
					var v = kv[1];// ここでも前後の空白削るのやってる
					headers[k] = v;
				} else {
					throw new Exception("Invalid header key/value.");
				}
			}

			// validate exists or not.
			if (!headers.Any()) throw new Exception("Reading headers failed.");
			if (!headers.ContainsKey("sec-websocket-version")) throw new Exception("Missing parameter \"Sec-WebSocket-Version\".");
			if (!headers.ContainsKey("sec-websocket-key")) throw new Exception("Missing parameter \"Sec-WebSocket-Key\".");
			if (!headers.ContainsKey("host")) throw new Exception("Missing parameter \"Host\".");
			if (!headers.ContainsKey("origin")) throw new Exception("Missing parameter \"Origin\".");
			
			// validate protocol.
			if (headers["sec-websocket-version"] != WEBSOCKET_VERSION) throw new Exception("Wrong protocol version:" + WEBSOCKET_VERSION);
			
			
			var key = headers["sec-websocket-key"];
			Debug.LogError("ここまでで、keyが取得できているはず key:" + key);
			// var accept = base64.b64encode(
			// 		(
			// 			hashlib.sha1(
			// 				(key + WEBSOCKET_MAGIC).encode('utf-8')
			// 			)
			// 		).digest()
			// 	);
			// var decodedAccept = accept.decode('utf-8');

			// currentBytes = ('HTTP/1.1 101 Switching Protocols\r\n'
			// 	'Upgrade: websocket\r\n'
			// 	'Connection: Upgrade\r\n'
			// 	'Sec-WebSocket-Origin: %s\r\n'
			// 	'Sec-WebSocket-Location: ws://%s\r\n'
			// 	'Sec-WebSocket-Accept: %s\r\n'
			// 	'Sec-WebSocket-Version: %s\r\n'
			// 	'\r\n') % (headers['origin'], headers['host'], decodedAccept, headers['sec-websocket-version'])

			// handshakeMessage = '--- HANDSHAKE ---\r\n'
			// handshakeMessage = handshakeMessage + '-----------------\r\n'
			// handshakeMessage = handshakeMessage + currentBytes + '\r\n'
			// handshakeMessage = handshakeMessage + '-----------------\r\n'
			
			// bufferdBytes = bytes(currentBytes, 'utf-8')
			// self.send(bufferdBytes)
		}
		
		private byte[] Receive (int bufSize) {
			// def receive(self, bufsize):
			try {
				var bytes = new byte[100];//self.conn.recv(bufsize) //ここでConnectionが露出する。
				return bytes;
			} catch (Exception e) {
				Debug.LogError("Receive e:" + e);
				return new byte[0]; 
			}
		}
		
		private string ReadLineHeader () {
			var line = string.Empty;

			while (state == ReadyState.Connecting && line.Length < BUF_SIZE) {
				var c = Receive(1);// byte to charが必要、たぶんコードでいいはず。
				line = line + Encoding.UTF8.GetString(c);

				if (c[0] == '\n') {
					Debug.LogError(" _\\_ n_ であってた");
					break;
				}
			}
			return line;
		}
	}
}
