// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using System.Collections;
using System.IO;
using System.Text;
using System.CodeDom;

namespace HoloToolkit.Unity.SpatialMapping
{
    /// <summary>
    /// SimpleMeshSerializer converts a UnityEngine.Mesh object to and from an array of bytes.
    /// This class saves minimal mesh data (vertices and triangle indices) in the following format:
    ///    File header: vertex count (32 bit integer), triangle count (32 bit integer)
    ///    Vertex list: vertex.x, vertex.y, vertex.z (all 32 bit float)
    ///    Triangle index list: 32 bit integers
    /// </summary>
    public class MeshToObjExporter {
 
    public static string MeshToString(MeshFilter mf) {
        Mesh m = mf.mesh;
        Material[] mats = mf.GetComponent<Renderer>().sharedMaterials;
 
        StringBuilder sb = new StringBuilder();
 
        sb.Append("g ").Append(mf.name).Append("\n");
        foreach(Vector3 v in m.vertices) {
            sb.Append(string.Format("v {0} {1} {2}\n",v.x,v.y,v.z));
        }
        sb.Append("\n");
        foreach(Vector3 v in m.normals) {
            sb.Append(string.Format("vn {0} {1} {2}\n",v.x,v.y,v.z));
        }
        sb.Append("\n");
        foreach(Vector3 v in m.uv) {
            sb.Append(string.Format("vt {0} {1}\n",v.x,v.y));
        }
        for (int material=0; material < m.subMeshCount; material ++) {
            sb.Append("\n");
            sb.Append("usemtl ").Append(mats[material].name).Append("\n");
            sb.Append("usemap ").Append(mats[material].name).Append("\n");
 
            int[] triangles = m.GetTriangles(material);
            for (int i=0;i<triangles.Length;i+=3) {
                sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", 
                    triangles[i]+1, triangles[i+1]+1, triangles[i+2]+1));
            }
        }
        return sb.ToString();
    }

    public static string MeshToString(Mesh m) {
        
        StringBuilder sb = new StringBuilder();
 
        foreach(Vector3 v in m.vertices) {
            sb.Append(string.Format("v {0} {1} {2}\n",v.x,v.y,v.z));
        }
        sb.Append("\n");
        foreach(Vector3 v in m.normals) {
            sb.Append(string.Format("vn {0} {1} {2}\n",v.x,v.y,v.z));
        }
        sb.Append("\n");
        foreach(Vector3 v in m.uv) {
            sb.Append(string.Format("vt {0} {1}\n",v.x,v.y));
        }
        for (int material=0; material < m.subMeshCount; material ++) {
            
            int[] triangles = m.GetTriangles(material);
            for (int i=0;i<triangles.Length;i+=3) {
                sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", 
                    triangles[i]+1, triangles[i+1]+1, triangles[i+2]+1));
            }
        }
        return sb.ToString();
    }
        /*
        private static string ToLiteral(string input)
        {
            using (var writer = new StringWriter())
            {
                using (var provider = CodeDomProvider.CreateProvider("CSharp"))
                {
                    provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
                    return writer.ToString();
                }
            }
        }
        */

        public static void MeshToFile(Mesh m, string filename) {
            string folderName = MeshSaver.MeshFolderName;

            string path = Path.Combine(folderName, filename + ".obj");
            FileStream fs = null;
            try
            {
                fs = new FileStream(path, FileMode.CreateNew);
                using (StreamWriter sw = new StreamWriter(fs))
                {

                    sw.Write(MeshToString(m));
                }
            }
            finally
            {
                if (fs != null)
                    fs.Dispose();
            }

    }

        public static void MeshToFile(MeshFilter mf, string filename) {
            string folderName = MeshSaver.MeshFolderName;

            string path = Path.Combine(folderName, filename + ".obj");
            FileStream fs = null;
            try
            {
                fs = new FileStream(path, FileMode.CreateNew);
                using (StreamWriter sw = new StreamWriter(fs))
                {

                    sw.Write(MeshToString(mf));
                }
            }
            finally
            {
                if (fs != null)
                    fs.Dispose();
            }

    }

       public static void WriteLog(string content, string filename) {
            string folderName = MeshSaver.MeshFolderName;

            string path = Path.Combine(folderName, filename + ".txt");
            FileStream fs = null;
            try
            {
                fs = new FileStream(path, FileMode.CreateNew);
                using (StreamWriter sw = new StreamWriter(fs))
                {

                    sw.Write(content);
                }
            }
            finally
            {
                if (fs != null)
                    fs.Dispose();
            }

    }
}
}