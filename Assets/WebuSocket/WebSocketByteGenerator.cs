using System.IO;
using UnityEngine;

namespace WebuSocket {
	public class WebSocketByteGenerator {
		// 特に状態を持たないので、staticでいいはず。
		public WebSocketByteGenerator () {
			
		}
		
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

		// # OPCODES USED FOR ERRORS:
		// # 1002: PROTOCOL_ERROR
		// # 1003: UNSUPPORTED_DATA_TYPE
		// # 1007: INVALID_PAYLOAD
		// # 1011: UNEXPECTED_CONDITION_ENCOUTERED_ON_SERVER
		
		private const byte OP_CONTINUATION = 0x0;
		private const byte OP_TEXT = 0x1;
		private const byte OP_BINARY = 0x2;
		private const byte OP_CLOSE = 0x8;
		private const byte OP_PING = 0x9;
		private const byte OP_PONG = 0xA;

				
		public static byte[] CloseData () {
			byte fin = 1;
			byte rsv1 = 0;
			byte rsv2 = 0;
			byte rsv3 = 0;
			byte opCode = OP_CLOSE;
			byte mask = 1;
			
			var length = 0;
			
			var baseBytes = new MemoryStream(); 
			baseBytes.WriteByte((byte)((fin << 7) | (rsv1 << 6) | (rsv2 << 5) | (rsv3 << 4) | opCode));
			baseBytes.WriteByte((byte)((mask << 7) | length));
			
			if (mask == 1) {
				var maskKey = WebuSocketClient.NewMaskKey();
				baseBytes.Write(maskKey, 0, maskKey.Length);
			}
			
			var data = baseBytes.ToArray();
			
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