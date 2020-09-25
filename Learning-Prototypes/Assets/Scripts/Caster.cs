using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Caster : MonoBehaviour
{
    public Voxelizer voxelizer;
    public Transform transformer;
    public bool draw;
    public bool bruteForce;
    private void OnDrawGizmos()
    {
        if (!draw)
            return;

        if (voxelizer == null || voxelizer.data == null)
            return;

        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;

        Gizmos.color = Color.white;
        Gizmos.DrawLine(origin, origin + (direction * 100000000));

        Gizmos.matrix = transformer.localToWorldMatrix;

        voxelizer.OnDrawGizmos();

        origin = transformer.worldToLocalMatrix * new Vector4(origin.x, origin.y, origin.z, 1);
        direction = transformer.worldToLocalMatrix * new Vector4(direction.x, direction.y, direction.z, 0);

        Ray ray = new Ray(origin, direction);

        Voxelizer.TreeNode root = voxelizer.data[voxelizer.root];

        //Debug.Log(root.child0 + ", " +
        //    root.child1 + ", " +
        //    root.child2 + ", " +
        //    root.child3 + ", " +
        //    root.child4 + ", " +
        //    root.child5 + ", " +
        //    root.child6 + ", " +
        //    root.child7);

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
        Bounds box = new Bounds(node.origin, extends);
        if (box.IntersectRay(ray))
        {
            Gizmos.DrawWireCube(node.origin, extends * 2f);
        }

        if (!bruteForce)
        {
            if (node.child0 != 0)
                TraverseTree(voxelizer.data[node.child0 - 1], ray);
            if (node.child1 != 0)
                TraverseTree(voxelizer.data[node.child1 - 1], ray);
            if (node.child2 != 0)
                TraverseTree(voxelizer.data[node.child2 - 1], ray);
            if (node.child3 != 0)
                TraverseTree(voxelizer.data[node.child3 - 1], ray);
            if (node.child4 != 0)
                TraverseTree(voxelizer.data[node.child4 - 1], ray);
            if (node.child5 != 0)
                TraverseTree(voxelizer.data[node.child5 - 1], ray);
            if (node.child6 != 0)
                TraverseTree(voxelizer.data[node.child6 - 1], ray);
            if (node.child7 != 0)
                TraverseTree(voxelizer.data[node.child7 - 1], ray);
        }
    }
}
