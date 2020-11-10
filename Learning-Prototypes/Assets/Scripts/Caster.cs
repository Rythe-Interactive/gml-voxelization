using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class Caster : MonoBehaviour
{
    public Voxelizer voxelizer;
    public Transform transformer;
    public bool drawRays;
    public bool drawBoxes;

    [HideInInspector]
    public bool drawGizmos;

    uint closesttriangle;
    float smallestDist;

    [Range(1, 500)]
    public int rayCount = 1;
    [Range(0, 10)]
    public float spread = 1;
    public List<Vector3> dirs;

    private void OnValidate()
    {
        GetDirs();
    }

    public void GetDirs()
    {
        dirs = new List<Vector3>();
        for (int i = 0; i < rayCount; i++)
        {
            dirs.Add(new Vector3(Random.Range(-spread, spread), Random.Range(-spread, spread), 11).normalized);
        }
    }

    private void OnDrawGizmos()
    {
        //bruteForce = true;

        if (!drawGizmos)
            return;

        if (!drawRays && !drawBoxes)
            return;

        if (voxelizer == null || voxelizer.data == null)
            return;

        transformer = voxelizer.meshFilter.transform;
        Gizmos.matrix = transformer.localToWorldMatrix;

        voxelizer.DrawGizmos();

        Gizmos.matrix = transformer.localToWorldMatrix;

        Vector3 origin = transformer.InverseTransformPoint(transform.position);

        if (rayCount != dirs.Count)
            GetDirs();

        for (int i = 0; i < rayCount; i++)
        {
            Vector3 direction = transformer.worldToLocalMatrix * transform.localToWorldMatrix * new Vector4(dirs[i].x, dirs[i].y, dirs[i].z, 0);
            CastRay(new Ray(origin, direction));
        }
    }

    void CastRay(Ray ray)
    {
        Voxelizer.TreeNode root = voxelizer.data[voxelizer.root];

        Gizmos.color = new Color(1, 0, 0, 0.1f);
        closesttriangle = 0;
        smallestDist = 1000000000000;
        TraverseTree(root, ray);

        if (closesttriangle != 0)
        {
            Gizmos.color = Color.green;
            var verts = voxelizer.triangles[closesttriangle - 1].vertices;
            for (int j = 0; j < verts.Length; j++)
                Gizmos.DrawLine(verts[j], verts[(j + 1) % verts.Length]);

            Vector3 normal = voxelizer.triangles[closesttriangle - 1].normal;
            Vector3 position = ray.GetPoint(smallestDist);

            if (Vector3.Dot(normal, (ray.origin - position).normalized) < 0)
                normal = -normal;

            Vector3 newRayDir = Vector3.Reflect(ray.direction, normal);
            Gizmos.color = new Color(0, 1, 1, 0.1f);
            Gizmos.DrawLine(position, position + (newRayDir * 1000000000000));
        }

        Gizmos.color = new Color(1, 1, 1, 0.1f);
        if (drawRays)
            Gizmos.DrawLine(ray.origin, ray.origin + (ray.direction * smallestDist));
    }

    void TraverseTree(Voxelizer.TreeNode node, Ray ray)
    {
        Vector3 extends = new Vector3(node.extends, node.extends, node.extends);
        Bounds box = new Bounds(node.origin, extends * 2f);
        if (box.IntersectRay(ray))
        {
            Gizmos.color = new Color(1, 0, 0, 0.5f);

            if (drawBoxes)
                Gizmos.DrawWireCube(node.origin, extends * 2f);

            if (node.extends * 2f > voxelizer.leafSize)
            {
                var children = node.children;
                foreach (var childIdx in children)
                    if (childIdx != 0)
                        TraverseTree(voxelizer.data[childIdx - 1], ray);
            }
            else if (drawRays)
            {
                Gizmos.color = new Color(1, 0, 1, 1);

                var children = node.children;
                foreach (var tridx in children)
                {
                    if (tridx != 0)
                    {
                        var verts = voxelizer.triangles[tridx - 1].vertices;
                        float dist = Intersect(ray, verts);
                        if (dist >= 0)
                        {
                            if (dist < smallestDist)
                            {
                                smallestDist = dist;
                                closesttriangle = tridx;
                            }

                            for (int j = 0; j < verts.Length; j++)
                                Gizmos.DrawLine(verts[j], verts[(j + 1) % verts.Length]);
                        }
                    }
                }
            }
        }
    }

    static float Intersect(Ray ray, Vector3[] verts)
    {
        // Vectors from verts[0] to verts[1]/verts[2] (edges)
        Vector3 e1, e2;
        Vector3 p, q, t;
        float det, invDet, u, v;
        //Find vectors for two edges sharing vertex/point verts[0]
        e1 = verts[1] - verts[0];
        e2 = verts[2] - verts[0];
        // calculating determinant
        p = Vector3.Cross(ray.direction, e2);
        //Calculate determinat
        det = Vector3.Dot(e1, p);
        //if determinant is near zero, ray lies in plane of triangle otherwise not
        if (det > -Mathf.Epsilon && det < Mathf.Epsilon) { return -1; }
        invDet = 1.0f / det;
        //calculate distance from verts[0] to ray origin
        t = ray.origin - verts[0];
        //Calculate u parameter
        u = Vector3.Dot(t, p) * invDet;
        //Check for ray hit
        if (u < 0 || u > 1) { return -1; }
        //Prepare to test v parameter
        q = Vector3.Cross(t, e1);
        //Calculate v parameter
        v = Vector3.Dot(ray.direction, q) * invDet;
        //Check for ray hit
        if (v < 0 || u + v > 1) { return -1; }
        float dist = Vector3.Dot(e2, q) * invDet;
        if (dist > Mathf.Epsilon)
        {
            //ray does intersect
            return dist;
        }
        // No hit at all
        return -1;
    }
}
