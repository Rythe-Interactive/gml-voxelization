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
        if (!drawGizmos)
            return;

        if (!draw)
            return;

        if (voxelizer == null || voxelizer.data == null)
            return;

        Gizmos.matrix = transformer.localToWorldMatrix;

        voxelizer.DrawGizmos();

        Gizmos.matrix = Matrix4x4.identity;

        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;

        Gizmos.color = Color.white;
        Gizmos.DrawLine(origin, origin + (direction * 100000000));

        Gizmos.matrix = transformer.localToWorldMatrix;

        origin = transformer.worldToLocalMatrix * new Vector4(origin.x, origin.y, origin.z, 1);
        direction = transformer.worldToLocalMatrix * new Vector4(direction.x, direction.y, direction.z, 0);

        Ray ray = new Ray(origin, direction);

        Voxelizer.TreeNode root = voxelizer.data[voxelizer.root];

        Gizmos.color = Color.red;

        if (bruteForce)
        {
            for (int i = 0; i < voxelizer.data.Length; i++)
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

            if (!bruteForce)
            {
                if (node.child0 != 0 && voxelizer.hierarchy[node.child0 - 1] != 0)
                    TraverseTree(voxelizer.data[voxelizer.hierarchy[node.child0 - 1] - 1], ray);
                if (node.child1 != 0 && voxelizer.hierarchy[node.child1 - 1] != 0)
                    TraverseTree(voxelizer.data[voxelizer.hierarchy[node.child1 - 1] - 1], ray);
                if (node.child2 != 0 && voxelizer.hierarchy[node.child2 - 1] != 0)
                    TraverseTree(voxelizer.data[voxelizer.hierarchy[node.child2 - 1] - 1], ray);
                if (node.child3 != 0 && voxelizer.hierarchy[node.child3 - 1] != 0)
                    TraverseTree(voxelizer.data[voxelizer.hierarchy[node.child3 - 1] - 1], ray);
                if (node.child4 != 0 && voxelizer.hierarchy[node.child4 - 1] != 0)
                    TraverseTree(voxelizer.data[voxelizer.hierarchy[node.child4 - 1] - 1], ray);
                if (node.child5 != 0 && voxelizer.hierarchy[node.child5 - 1] != 0)
                    TraverseTree(voxelizer.data[voxelizer.hierarchy[node.child5 - 1] - 1], ray);
                if (node.child6 != 0 && voxelizer.hierarchy[node.child6 - 1] != 0)
                    TraverseTree(voxelizer.data[voxelizer.hierarchy[node.child6 - 1] - 1], ray);
                if (node.child7 != 0 && voxelizer.hierarchy[node.child7 - 1] != 0)
                    TraverseTree(voxelizer.data[voxelizer.hierarchy[node.child7 - 1] - 1], ray);
            } }
    }
}
