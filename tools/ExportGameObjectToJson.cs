using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using System.IO;
using System.Text;

/**
* GameObjectの名前、トランスフォーム等をJSONファイルに出力するエディタ拡張
*
* プロジェクトのEditorフォルダ(無ければ適当な場所に作成)に配置して使う.
*/
class ExportGameObjectToJson : EditorWindow
{
    bool flipYZAxis = true; // XZ平面のマップを-90度回転して、XY平面として出力する

    [MenuItem("File/Export/GameObject To Json")]

    static void Init()
    {
        var window = EditorWindow.GetWindow<ExportGameObjectToJson>("Export GameObject To Json");
        window.Show();
    }

    void OnGUI()
    {
        flipYZAxis = GUILayout.Toggle(flipYZAxis, "Flip YZ Axis");
        if (GUILayout.Button("Export")) {
            Export(flipYZAxis);
        }
    }

    private static void Export(bool flipYZAxis)
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
                if (flipYZAxis) {
                    var tmp = -t.y;
                    t.y = t.z;
                    t.z = tmp;
                    var q = Quaternion.AngleAxis(-90, Vector3.right) * go.transform.rotation;
                    r = q.eulerAngles;
                }
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
