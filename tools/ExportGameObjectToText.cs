using UnityEngine;
using UnityEditor;
using System.IO;

public class ExportGameObjectToText
{
    [MenuItem("File/Export/GameObject To Text")]
    private static void ExportToText()
    {
        var path = EditorUtility.SaveFilePanel(
            "Save GameObject Transform",
            "",
            "GameObjectTransforms.txt",
            "txt");
        if (path.Length == 0) {
            return;
        }

        try {
            System.IO.StreamWriter saveFile = new System.IO.StreamWriter(path, false);

            Transform[] transforms = GameObject.FindObjectsOfType(typeof(Transform)) as Transform[];
            foreach (Transform t in transforms) {
                if (t.parent != null) {
                    continue;
                }
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (prefab != null) {
                    saveFile.WriteLine("\"" + prefab.name + "\", " + t.position + ", " + t.eulerAngles + ", " + t.localScale);
                    continue;
                }
                MeshFilter m = t.gameObject.GetComponent<MeshFilter>();
                if (m != null && m.mesh != null) {
                    saveFile.WriteLine("\"" + m.mesh.name + "\", " + t.position + ", " + t.eulerAngles + ", " + t.localScale);
                    continue;
                }
            }
            saveFile.Flush();
            saveFile.Close();
            Debug.Log("Export GameObject To " + path);
        }
        catch (System.Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }
}
