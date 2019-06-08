﻿using System;
using System.Collections.Generic;
using System.Numerics;

namespace SoulsFormats
{
    public partial class FLVER
    {
        /// <summary>
        /// An individual chunk of a model.
        /// </summary>
        public class Mesh
        {
            /// <summary>
            /// Unknown.
            /// </summary>
            public bool Dynamic;

            /// <summary>
            /// Index of the material used by all triangles in this mesh.
            /// </summary>
            public int MaterialIndex;

            /// <summary>
            /// Apparently does nothing. Usually points to a dummy bone named after the model, possibly just for labelling.
            /// </summary>
            public int DefaultBoneIndex;

            /// <summary>
            /// Indexes of bones in the bone collection which may be used by vertices in this mesh.
            /// </summary>
            public List<int> BoneIndices;

            /// <summary>
            /// Triangles in this mesh.
            /// </summary>
            public List<FaceSet> FaceSets;

            /// <summary>
            /// Vertex buffers in this mesh.
            /// </summary>
            public List<VertexBuffer> VertexBuffers;

            /// <summary>
            /// Vertices in this mesh.
            /// </summary>
            public List<Vertex> Vertices;

            /// <summary>
            /// Minimum extent of the mesh.
            /// </summary>
            public Vector3 BoundingBoxMin;

            /// <summary>
            /// Maximum extent of the mesh.
            /// </summary>
            public Vector3 BoundingBoxMax;

            /// <summary>
            /// Unknown; only present in Sekiro.
            /// </summary>
            public Vector3 BoundingBoxUnk;

            /// <summary>
            /// Unknown.
            /// </summary>
            public int Unk1;

            private int[] faceSetIndices, vertexBufferIndices;

            /// <summary>
            /// Creates a new Mesh with default values.
            /// </summary>
            public Mesh()
            {
                Dynamic = false;
                MaterialIndex = 0;
                DefaultBoneIndex = -1;
                BoneIndices = new List<int>();
                FaceSets = new List<FaceSet>();
                VertexBuffers = new List<VertexBuffer>();
                Vertices = new List<Vertex>();
            }

            internal Mesh(BinaryReaderEx br, int version)
            {
                Dynamic = br.ReadBoolean();
                br.AssertByte(0);
                br.AssertByte(0);
                br.AssertByte(0);

                MaterialIndex = br.ReadInt32();
                br.AssertInt32(0);
                if (version <= 0x20010)
                    br.AssertInt32(0);
                DefaultBoneIndex = br.ReadInt32();

                int boneCount = br.ReadInt32();
                Unk1 = br.AssertInt32(0, 1, 10);
                if (version >= 0x20013)
                {
                    int boundingBoxOffset = br.ReadInt32();
                    br.StepIn(boundingBoxOffset);
                    {
                        BoundingBoxMin = br.ReadVector3();
                        BoundingBoxMax = br.ReadVector3();
                        if (version >= 0x2001A)
                            BoundingBoxUnk = br.ReadVector3();
                    }
                    br.StepOut();
                }
                int boneOffset = br.ReadInt32();
                BoneIndices = new List<int>(br.GetInt32s(boneOffset, boneCount));

                int faceSetCount = br.ReadInt32();
                int faceSetOffset = br.ReadInt32();
                faceSetIndices = br.GetInt32s(faceSetOffset, faceSetCount);

                int vertexBufferCount = br.AssertInt32(1, 2, 3);
                int vertexBufferOffset = br.ReadInt32();
                vertexBufferIndices = br.GetInt32s(vertexBufferOffset, vertexBufferCount);
            }

            internal void TakeFaceSets(Dictionary<int, FaceSet> faceSetDict)
            {
                FaceSets = new List<FaceSet>(faceSetIndices.Length);
                foreach (int i in faceSetIndices)
                {
                    if (!faceSetDict.ContainsKey(i))
                        throw new NotSupportedException("Face set not found or already taken: " + i);

                    FaceSets.Add(faceSetDict[i]);
                    faceSetDict.Remove(i);
                }
                faceSetIndices = null;
            }

            internal void TakeVertexBuffers(Dictionary<int, VertexBuffer> vertexBufferDict, List<BufferLayout> layouts)
            {
                VertexBuffers = new List<VertexBuffer>(vertexBufferIndices.Length);
                foreach (int i in vertexBufferIndices)
                {
                    if (!vertexBufferDict.ContainsKey(i))
                        throw new NotSupportedException("Vertex buffer not found or already taken: " + i);

                    VertexBuffers.Add(vertexBufferDict[i]);
                    vertexBufferDict.Remove(i);
                }
                vertexBufferIndices = null;

                // Make sure no semantics repeat that aren't known to
                var semantics = new List<BufferLayout.MemberSemantic>();
                foreach (VertexBuffer buffer in VertexBuffers)
                {
                    foreach (var member in layouts[buffer.LayoutIndex])
                    {
                        if (member.Semantic != BufferLayout.MemberSemantic.UV
                            && member.Semantic != BufferLayout.MemberSemantic.Tangent
                            && member.Semantic != BufferLayout.MemberSemantic.VertexColor
                            && member.Semantic != BufferLayout.MemberSemantic.Position
                            && member.Semantic != BufferLayout.MemberSemantic.Normal)
                        {
                            if (semantics.Contains(member.Semantic))
                                throw new NotImplementedException("Unexpected semantic list.");
                            semantics.Add(member.Semantic);
                        }
                    }
                }

                for (int i = 0; i < VertexBuffers.Count; i++)
                {
                    VertexBuffer buffer = VertexBuffers[i];
                    if (buffer.BufferIndex != i)
                        throw new FormatException("Unexpected vertex buffer indices.");

                    BufferLayout layout = layouts[buffer.LayoutIndex];
                    if (layout.Size != buffer.VertexSize)
                        throw new FormatException("Mismatched vertex sizes are not supported for split buffers.");
                }
            }

            internal void ReadVertices(BinaryReaderEx br, int dataOffset, List<BufferLayout> layouts, int version)
            {
                int vertexCount = VertexBuffers[0].VertexCount;
                Vertices = new List<Vertex>(vertexCount);
                for (int i = 0; i < vertexCount; i++)
                    Vertices.Add(new Vertex());

                foreach (VertexBuffer buffer in VertexBuffers)
                    buffer.ReadBuffer(br, layouts, Vertices, dataOffset, version);
            }

            internal void Write(BinaryWriterEx bw, int index, int version)
            {
                bw.WriteBoolean(Dynamic);
                bw.WriteByte(0);
                bw.WriteByte(0);
                bw.WriteByte(0);

                bw.WriteInt32(MaterialIndex);
                bw.WriteInt32(0);
                if (version <= 0x20010)
                    bw.WriteInt32(0);
                bw.WriteInt32(DefaultBoneIndex);

                bw.WriteInt32(BoneIndices.Count);
                bw.WriteInt32(Unk1);
                if (version >= 0x20013)
                    bw.ReserveInt32($"MeshBoundingBox{index}");
                bw.ReserveInt32($"MeshBoneIndices{index}");

                bw.WriteInt32(FaceSets.Count);
                bw.ReserveInt32($"MeshFaceSetIndices{index}");

                bw.WriteInt32(VertexBuffers.Count);
                bw.ReserveInt32($"MeshVertexBufferIndices{index}");
            }

            internal void WriteBoundingBox(BinaryWriterEx bw, int index, int version)
            {
                bw.FillInt32($"MeshBoundingBox{index}", (int)bw.Position);
                bw.WriteVector3(BoundingBoxMin);
                bw.WriteVector3(BoundingBoxMax);
                if (version >= 0x2001A)
                    bw.WriteVector3(BoundingBoxUnk);
            }

            internal void WriteBoneIndices(BinaryWriterEx bw, int index)
            {
                bw.FillInt32($"MeshBoneIndices{index}", (int)bw.Position);
                bw.WriteInt32s(BoneIndices.ToArray());
            }

            /// <summary>
            /// Returns a list of arrays of 3 vertices, each representing a triangle in the mesh.
            /// Faces are taken from the first FaceSet in the mesh with the given flags,
            /// using None by default for the highest detail mesh. If not found, the first FaceSet is used.
            /// </summary>
            public List<Vertex[]> GetFaces(FaceSet.FSFlags fsFlags = FaceSet.FSFlags.None)
            {
                FaceSet faceset = FaceSets.Find(fs => fs.Flags == fsFlags) ?? FaceSets[0];
                List<int[]> indices = faceset.GetFaces();
                var vertices = new List<Vertex[]>(indices.Count);
                foreach (int[] face in indices)
                    vertices.Add(new Vertex[] { Vertices[face[0]], Vertices[face[1]], Vertices[face[2]] });
                return vertices;
            }
        }
    }
}