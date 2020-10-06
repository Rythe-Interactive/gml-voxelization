using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Caster))]
public class CasterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
    }

    private void OnSceneGUI()
    {
        Transform transform = (target as Caster).transformer;

        if (Tools.current == Tool.Move)
            transform.position = Handles.PositionHandle(transform.position, transform.rotation);
        else if (Tools.current == Tool.Rotate)
            transform.rotation = Handles.RotationHandle(transform.rotation, transform.position);
        else if (Tools.current == Tool.Scale)
            transform.localScale = Handles.ScaleHandle(transform.localScale, transform.position, transform.rotation, HandleUtility.GetHandleSize(transform.position));
    }

    private void OnEnable()
    {
        (target as Caster).drawGizmos = true;
    }

    private void OnDisable()
    {
        (target as Caster).drawGizmos = false;
    }
}