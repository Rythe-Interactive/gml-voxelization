using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Caster : MonoBehaviour
{
    public Voxelizer voxelizer;
    public Transform transformer;
    public bool draw;
    public bool bruteForce;
    public uint G;
    public bool drawGthGenerationOnly;

    [HideInInspector]
    public bool drawGizmos;

    private void OnValidate()
    {
        if (voxelizer != null)
            if (G >= voxelizer.generationCount)
                G = voxelizer.generationCount;
    }

    private void OnDrawGizmos()
    {
        //bruteForce = true;

        if (!drawGizmos)
            return;

        if (!draw)
            return;

        if (voxelizer == null || voxelizer.data == null)
            return;

        transformer = voxelizer.meshFilter.transform;
        Gizmos.matrix = transformer.localToWorldMatrix;

        voxelizer.DrawGizmos();

        Gizmos.matrix = Matrix4x4.identity;

        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + (direction * 100000000));

        Gizmos.matrix = transformer.localToWorldMatrix;

        origin = transformer.worldToLocalMatrix * new Vector4(origin.x, origin.y, origin.z, 1);
        direction = transformer.worldToLocalMatrix * new Vector4(direction.x, direction.y, direction.z, 0);

        Ray ray = new Ray(origin, direction);

        Voxelizer.TreeNode root = voxelizer.data[voxelizer.root];

        Gizmos.color = new Color(1, 0, 0, 0.5f);

        if (bruteForce)
        {
            for (int i = 0; i < voxelizer.dataCount; i++)
                TraverseTree(voxelizer.data[i], ray);
        }
        else
            TraverseTree(root, ray);
    }

    void TraverseTree(Voxelizer.TreeNode node, Ray ray)
    {
        Vector3 extends = new Vector3(node.extends, node.extends, node.extends);
        Bounds box = new Bounds(node.origin, extends * 2f);
        if (box.IntersectRay(ray))
        {
            if (!drawGthGenerationOnly || G == voxelizer.generations[node.extends])
                Gizmos.DrawWireCube(node.origin, extends * 2f);

            if (node.extends * 2f > voxelizer.leafSize)
                if (!bruteForce)
                {
                    var children = node.children;
                    foreach (var childIdx in children)
                        if (childIdx != 0)
                            TraverseTree(voxelizer.data[childIdx - 1], ray);
                }
        }
    }
}
