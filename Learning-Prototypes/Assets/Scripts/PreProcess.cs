using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class PreProcess : ScriptableObject
{
    static void Preprocess(Mesh mesh)
    {
        EditorUtility.ClearProgressBar();

        Stopwatch clock = new Stopwatch();
        clock.Start();

        Vector3 center = new Vector3(0, 0, 0);
        Vector3 size = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        EditorUtility.DisplayProgressBar("Processing...", "Preprocessing mesh.", 0);

        Vector3[] vertices = mesh.vertices;

        for (int i = 0; i < mesh.vertexCount; i++)
        {
            EditorUtility.DisplayProgressBar("Processing...", "Preprocessing mesh.", ((float)i) / (mesh.vertexCount * 3f));
            center += vertices[i];
        }

        center /= mesh.vertexCount;

        Debug.Log("center " + center);

        for (int i = 0; i < mesh.vertexCount; i++)
        {
            EditorUtility.DisplayProgressBar("Processing...", "Preprocessing mesh.", ((float)i + mesh.vertexCount) / (mesh.vertexCount * 3f));

            vertices[i] -= center;
            size.x = Mathf.Max(size.x, Mathf.Abs(vertices[i].x));
            size.y = Mathf.Max(size.y, Mathf.Abs(vertices[i].y));
            size.z = Mathf.Max(size.z, Mathf.Abs(vertices[i].z));
        }

        mesh.vertices = vertices;
        mesh.bounds = new Bounds(new Vector3(0, 0, 0), size * 2f);
        mesh.UploadMeshData(false);
        mesh.MarkModified();
        clock.Stop();

        EditorUtility.DisplayDialog("Mesh Preprocessing", "Done! It took " + clock.Elapsed.TotalMilliseconds + "ms.", "OK", "");
        EditorUtility.ClearProgressBar();
    }

    [MenuItem("CONTEXT/Mesh/Preprocess")]
    static void MeshPreProc(MenuCommand command)
    {
        Preprocess((Mesh)command.context);
    }

    [MenuItem("CONTEXT/MeshFilter/Preprocess")]
    static void MeshFilterPreProc(MenuCommand command)
    {
        Preprocess(((MeshFilter)command.context).sharedMesh);
    }
}