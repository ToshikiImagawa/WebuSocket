using UnityEngine;
using UnityEditor;

using System;
using System.IO;

/*
	このクラス自体がUnityに依存しているので、なんかうまい抽象化を考えないとな
	
*/
[InitializeOnLoad]
public class ServerInitializer {
	static ServerContext sContext;

	static DisqueConnectionController disqueConnectionCont;


	static ServerInitializer () {
		Init();
	}

	public static void Init () {
		sContext = new ServerContext();
		disqueConnectionCont = new DisqueConnectionController(SetupUpdater, "calivers_disque_client_context", DisqueConnectionController.DisqueDataMode.disque_binary.ToString());
		disqueConnectionCont.SetContext(sContext);
	}

	public static void SetupUpdater (string loopId, Func<bool> OnUpdate) {
		Action update = () => OnUpdate();
		var executor = new UnityEditorUpdateExecutor(update);
		EditorApplication.update += executor.Update;
	}


	// あとはこのへんにコマンドを追加するかね。停止と再起動、リセット。contextのリセットとかを呼べば良い感じ。
}
