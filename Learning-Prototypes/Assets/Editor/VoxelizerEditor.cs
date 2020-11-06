using UnityEngine;
using UnityEditor;
using System.Reflection;

[CustomEditor(typeof(Voxelizer))]
public class VoxelizerEditor : Editor
{
    bool coninuousRevoxelisation = false;
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
            vox.triangleCount = 0;
            vox.triangles = null;
            vox.dataCount = 0;
            vox.data = null;
            vox.model = null;
            vox.OnValidate();
            SceneView.RepaintAll();
        }

        coninuousRevoxelisation = GUILayout.Toggle(coninuousRevoxelisation, "Revoxelize continuously");
    }

    private void OnEnable()
    {
        Voxelizer vox = (target as Voxelizer);
        vox.drawGizmos = true;
        vox.meshFilter.GetComponent<MeshRenderer>().enabled = true;

        System.Type type = typeof(Tools);
        FieldInfo field = type.GetField("s_Hidden", BindingFlags.NonPublic | BindingFlags.Static);
        field.SetValue(null, true);
    }

    private void OnDisable()
    {
        Voxelizer vox = (target as Voxelizer);
        vox.drawGizmos = false;
        vox.meshFilter.GetComponent<MeshRenderer>().enabled = false;

        System.Type type = typeof(Tools);
        FieldInfo field = type.GetField("s_Hidden", BindingFlags.NonPublic | BindingFlags.Static);
        field.SetValue(null, false);
    }

    private void OnSceneGUI()
    {
        if (coninuousRevoxelisation)
        {
            (target as Voxelizer).Voxelize();
            HandleUtility.Repaint();
        }

        Transform transform = (target as Voxelizer).meshFilter.transform;

        if (Tools.current == Tool.Move)
            transform.position = Handles.PositionHandle(transform.position, transform.rotation);
        else if (Tools.current == Tool.Rotate)
            transform.rotation = Handles.RotationHandle(transform.rotation, transform.position);
        else if (Tools.current == Tool.Scale)
            transform.localScale = Handles.ScaleHandle(transform.localScale, transform.position, transform.rotation, HandleUtility.GetHandleSize(transform.position));
    }
}