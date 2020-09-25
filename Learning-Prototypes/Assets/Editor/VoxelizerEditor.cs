using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Voxelizer))]
public class VoxelizerEditor : Editor
{
    bool rotate = false;
    float rotationSpeed = 1f;
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Voxelize"))
        {
            (target as Voxelizer).Voxelize();
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Clear"))
        {
            Voxelizer vox = (target as Voxelizer);
            vox.ClearLog();
            vox.generationCount = 0;
            vox.triangles.Clear();
            vox.data = null;

            SceneView.RepaintAll();
        }

        rotationSpeed = EditorGUILayout.FloatField("Rotation Speed", rotationSpeed);

        bool newRotate = GUILayout.Toggle(rotate, "Rotate");
        if(newRotate != rotate)
        {
            rotate = newRotate;
            SceneView.RepaintAll();
        }
    }

    public void OnSceneGUI()
    {
        if (rotate)
        {
            Voxelizer vox = (target as Voxelizer);
            Quaternion rot = vox.transform.rotation;

            rot *= Quaternion.AngleAxis(Time.deltaTime * rotationSpeed, Vector3.up);

            vox.transform.rotation = rot;
            //vox.Voxelize();
            SceneView.RepaintAll();
        }
    }
}