using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Caster : MonoBehaviour
{
    public Voxelizer voxelizer;
    public bool draw;

    private void OnDrawGizmos()
    {
        if(!draw)
            return;

        if(voxelizer == null || voxelizer.data == null)
            return;

        voxelizer.OnDrawGizmos();

        Ray ray = new Ray(transform.position, transform.forward);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(ray.origin, ray.origin + (ray.direction * 100000000));

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

        for(int i = 0; i < voxelizer.data.Length; i++)
            TraverseTree(voxelizer.data[i], ray);

        //TraverseTree(root, ray);
    }

    void TraverseTree(Voxelizer.TreeNode node, Ray ray)
    {
        Vector3 extends = new Vector3(node.extends, node.extends, node.extends);
        Bounds box = new Bounds(node.origin, extends);
        if (box.IntersectRay(ray))
        {
            Gizmos.DrawWireCube(node.origin, extends * 2f);            
        }

        //if (node.child0 != 0)
        //    TraverseTree(voxelizer.data[node.child0 - 1], ray);
        //if (node.child1 != 0)
        //    TraverseTree(voxelizer.data[node.child1 - 1], ray);
        //if (node.child2 != 0)
        //    TraverseTree(voxelizer.data[node.child2 - 1], ray);
        //if (node.child3 != 0)
        //    TraverseTree(voxelizer.data[node.child3 - 1], ray);
        //if (node.child4 != 0)
        //    TraverseTree(voxelizer.data[node.child4 - 1], ray);
        //if (node.child5 != 0)
        //    TraverseTree(voxelizer.data[node.child5 - 1], ray);
        //if (node.child6 != 0)
        //    TraverseTree(voxelizer.data[node.child6 - 1], ray);
        //if (node.child7 != 0)
        //    TraverseTree(voxelizer.data[node.child7 - 1], ray);
    }
}
