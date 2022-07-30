using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using System.IO;
using System.Text;

class ExportGameObjectToJson : EditorWindow
{
    [MenuItem("File/Export/GameObject To Json")]

    static void Init()
    {
        var window = EditorWindow.GetWindow<ExportGameObjectToJson>("Export GameObject To Json");
        window.Show();
    }

    void OnGUI()
    {
      if (GUILayout.Button("Export")) {
         Export();
      }
    }

    private static void Export()
    {
        var path = EditorUtility.SaveFilePanel(
            "Export GameObject To Json",
            "",
            "GameObjectTransforms.json",
            "json");
        if (path.Length == 0) {
            return;
        }

        try {
            var sb = new StringBuilder(10000);
            sb.Append("[");

            // ルートオブジェクトのうち、プレハブであるか、メッシュを持つゲームオブジェクトだけを出力する
            int count = 0;
            GameObject[] gameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject go in gameObjects) {
                string meshName;
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (prefab != null) {
                    meshName = prefab.name;
                } else {
                    MeshFilter m = go.GetComponent<MeshFilter>();
                    if (m != null && m.mesh != null) {
                        meshName = m.mesh.name;
                    } else {
                        continue;
                    }
                }
                var t = go.transform.position;
                var r = go.transform.eulerAngles;
                var s = go.transform.localScale;
                sb.AppendLine();
                sb.Append("  ");
                sb.Append("{ \"name\" : \"" + go.name + "\"");
                sb.Append(", \"mesh\" : \"" + meshName + "\"");
                sb.Append(", \"translate\" : [ " + t.x + ", " + t.y + ", " + t.z + " ]");
                sb.Append(", \"rotate\" : [ " + r.x + ", " + r.y + ", " + r.z + " ]");
                sb.Append(", \"scale\" : [ " + s.x + ", " + s.y + ", " + s.z + " ]");
                if (go.tag != "Untagged") {
                    sb.Append(", \"tag\" : \"" + go.tag + "\"");
                }
                sb.Append(" },");
                ++count;
            }
            // JSON準拠のため、末尾のカンマを消す
            if (count > 0) {
                sb.Remove(sb.Length - 1, 1);
            }
            sb.AppendLine();
            sb.AppendLine("]");

            System.IO.StreamWriter saveFile = new System.IO.StreamWriter(path, false);
            saveFile.Write(sb);
            saveFile.Flush();
            saveFile.Close();
            Debug.Log("Export " + count + " GameObject To " + path);
        }
        catch (System.Exception ex) {
            Debug.Log(ex.Message);
        }
    }
}
