using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Threading;

[InitializeOnLoad]
public class ServerInitializer {
	static ServerContext sContext;

	static DisqueConnectionController disqueConnectionCont;


	static ServerInitializer () {
		Init();
	}

	public static void Init () {
		sContext = new ServerContext();
		disqueConnectionCont = new DisqueConnectionController(SetupUpdater, "reflector_disque_client_context", DisqueConnectionController.DisqueDataMode.disque_binary.ToString());
		disqueConnectionCont.SetContext(sContext);
	}

	public static void SetupUpdater (string loopId, Func<bool> OnUpdate) {
		Action update = () => OnUpdate();
		var executor = new UnityEditorUpdateExecutor(update);
		EditorApplication.update += executor.Update;
	}
	
	public static void SetupUpdaterThread (string loopId, Func<bool> OnUpdate) {
		Action loopMethod = () => {
			try {
				while (true) {
					// run action for update.
					var continuation = OnUpdate();
					if (!continuation) break;
					
					Thread.Sleep(1);
				}
			} catch (Exception e) {
				Debug.LogError("error on ServerContext thread. error:" + e);
			}
		};
		
		var thread = new Thread(new ThreadStart(loopMethod));
		thread.Start();
	}


	// あとはこのへんにコマンドを追加するかね。停止と再起動、リセット。contextのリセットとかを呼べば良い感じ。
}
