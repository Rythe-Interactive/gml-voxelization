using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Caster))]
public class CasterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
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