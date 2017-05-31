# WebuSocket

WebSocket Client implementation for C#.  
ver 0.7.6

## motivation

* async default.
* lightweight.
* thread free. 
* task free.
* runnable on C# 3.5 or later(includes Unity).

## usage

```C#
var webuSocket = new WebuSocket(
	"ws://somewhere:someport",
	1024 * 100,// default buffer size.
	() => {
		// connected.
	}, 
	(Queue<ArraySegment<byte>> datas) => {
		// data received.
		
		while (0 < datas.Count) {
			var data = datas.Dequeue();
			var bytes = new byte[data.Count];
			Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);
			
			// use "bytes".
		}
	}, 
	() => {
		// pinged.
	}, 
	closeReason => {
		// closed.
	}, 
	(errorReason, e) => {
		// error happedned.
	}, 
	customHeaderKeyValues // Dictionary<string, string> which can send with connecting signal.
);
```	

sample unity project is here  
[SampleProject](https://github.com/sassembla/WebuSocket/tree/master/SampleProject)

## implemented
* basic WebSocket API
* connect by ip
* connect by domain
* tls
* reconnection
* disconnect detection by ping-pong timeout
* basic auth

##not yet implemented
* http redirection(necessary?)
* tls 1.3
* else...

**contribution welcome!**

## license
see below.  
[LICENSE](./LICENSE)

