using System.IO;
using UnityEngine;

namespace WebuSocket {
	public static class WebSocketByteGenerator {
		
		// #0                   1                   2                   3
		// #0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
		// #+-+-+-+-+-------+-+-------------+-------------------------------+
		// #|F|R|R|R| opcode|M| Payload len |    Extended payload length    |
		// #|I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
		// #|N|V|V|V|       |S|             |   (if payload len==126/127)   |
		// #| |1|2|3|       |K|             |                               |
		// #+-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
		// #|     Extended payload length continued, if payload len == 127  |
		// #+ - - - - - - - - - - - - - - - +-------------------------------+
		// #|                               | Masking-key, if MASK set to 1 |
		// #+-------------------------------+-------------------------------+
		// #| Masking-key (continued)       |          Payload Data         |
		// #+-------------------------------- - - - - - - - - - - - - - - - +
		// #:                     Payload Data continued ...                :
		// #+ - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
		// #|                     Payload Data continued ...                |

		private const byte OP_CONTINUATION = 0x0;
		private const byte OP_TEXT = 0x1;// 0001
		private const byte OP_BINARY = 0x2;// 0010
		private const byte OP_CLOSE = 0x8;// 1000
		private const byte OP_PING = 0x9;// 1001
		private const byte OP_PONG = 0xA;// 1010
		
		public static byte[] SendBinaryData (byte[] data) {
			byte fin = 1;
			byte rsv1 = 0;
			byte rsv2 = 0;
			byte rsv3 = 0;
			byte opCode = OP_BINARY;
			byte mask = 1;
			
			// この時点で、入りきるかどうかの判断ができるはず。で、streamだから長さが適当でもいいのか。なるほど。fragmentにする意味はなんだろう、、長さがわからん場合か。却下。
			uint length = (uint)data.Length;
			
			byte dataLength7 = 0;
			int dataLength16 = 0;
			uint dataLength64 = 0;
			
			if (length < 126) {
				dataLength7 = (byte)length;
			} else if (length < 65536) {
				dataLength7 = 126;
				dataLength16 = (int)length;
			} else {
				dataLength7 = 127;
				dataLength64 = length;
			}
			
			var dataStream = new MemoryStream(); 
			dataStream.WriteByte((byte)((fin << 7) | (rsv1 << 6) | (rsv2 << 5) | (rsv3 << 4) | opCode));
			dataStream.WriteByte((byte)((mask << 7) | dataLength7));
			
			if (0 < dataLength16) {
				// dataLength16 to 2byte.
				// dataStream.WriteByte();
			}
			if (0 < dataLength64) {
				// dataLength64 to 8byte.
				// dataStream.WriteByte();
			}
			
			// should mask control frame.
			var maskKey = WebuSocketClient.NewMaskKey();
			dataStream.Write(maskKey, 0, maskKey.Length);
			
			// mask data.
			var maskedData = data.Masked(maskKey);
			
			
			return dataStream.ToArray();
		}
				
		public static byte[] CloseData () {
			byte fin = 1;
			byte rsv1 = 0;
			byte rsv2 = 0;
			byte rsv3 = 0;
			byte opCode = OP_CLOSE;
			byte mask = 1;
			
			var dataLength = 0;
			
			var dataStream = new MemoryStream(); 
			dataStream.WriteByte((byte)((fin << 7) | (rsv1 << 6) | (rsv2 << 5) | (rsv3 << 4) | opCode));
			dataStream.WriteByte((byte)((mask << 7) | dataLength));
			
			// should mask control frame.
			var maskKey = WebuSocketClient.NewMaskKey();
			dataStream.Write(maskKey, 0, maskKey.Length);
			
			var data = dataStream.ToArray();
			
			// // # add length param
			// if (length < 126) {
			// 	var currentByteArray = baseBytes/*基礎ブロック左8bytes*/ + /*その右*/(mask << 7) | length;
			// }
			// // elif length < (1 << 16): # 65536
			// // 	currentByteArray = basebytes + bytes(chr((mask << 7) | 0x7e), 'utf-8') + struct.pack('!H', length)
			// // elif length < (1 << 63): # 9223372036854775808
			// // 	currentByteArray = basebytes + bytes(chr((mask << 7) | 0x7f), 'utf-8') + struct.pack('!Q', length)
			// // else:
			// 	// raise ValueError('Frame too large')
				
			// // このへんでデータのマスク処理が必要なはず。
			// var result = currentByteArray + bytedData;
			
			return data;
		}
		
		private static byte[] Masked (this byte[] data, byte[] maskKey) {
			for (var i = 0; i < data.Length; i++) data[i] ^= maskKey[i%4];
			return data;
		}
		
		// ## Mask datas
		// def mask(self, mask_key, bytes):
		// 	# new byte[i] = old byte[i] XOR mask_key[i%4]
		// 	m = array.array('B', mask_key)
		// 	j = array.array('B', bytes)
		// 	for i in xrange(len(j)):
		// 		j[i] ^= m[i % 4]
		// 	return j.tostring()
		// }
	}
}